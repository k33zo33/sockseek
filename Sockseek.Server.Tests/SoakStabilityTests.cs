using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sockseek.Api;
using Sockseek.Core.Settings;
using Sockseek.Server;

namespace Tests.Server;

[TestClass]
public sealed class SoakStabilityTests
{
    private const string RunSoakEnvVar = "SOCKSEEK_RUN_SOAK";
    private const string SoakMinutesEnvVar = "SOCKSEEK_SOAK_MINUTES";
    private const string SoakItemsEnvVar = "SOCKSEEK_SOAK_ITEMS_PER_CYCLE";
    private const string SoakCycleDelayMsEnvVar = "SOCKSEEK_SOAK_CYCLE_DELAY_MS";
    private const string ManagedBudgetMiBEnvVar = "SOCKSEEK_SOAK_MAX_MANAGED_GROWTH_MIB";
    private const string PrivateBudgetMiBEnvVar = "SOCKSEEK_SOAK_MAX_PRIVATE_GROWTH_MIB";
    private const string SoakReportPathEnvVar = "SOCKSEEK_SOAK_REPORT_PATH";
    private const string SoakCommitEnvVar = "SOCKSEEK_SOAK_COMMIT";
    private const int DefaultSoakMinutes = 480;
    private const int DefaultItemsPerCycle = 100;
    private const int DefaultCycleDelayMs = 30_000;
    private const int DefaultMaxManagedGrowthMiB = 256;
    private const int DefaultMaxPrivateGrowthMiB = 512;

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [TestCategory("Soak")]
    [Timeout(9 * 60 * 60 * 1000)]
    public async Task RepeatedWorkflowSoak_DoesNotExceedMemoryGrowthBudget()
    {
        if (!ShouldRunSoak())
        {
            TestContext.WriteLine(
                $"Skipped opt-in soak test. Set {RunSoakEnvVar}=1 to run the beta memory stability gate.");
            return;
        }

        var duration = TimeSpan.FromMinutes(ReadPositiveInt(SoakMinutesEnvVar, DefaultSoakMinutes));
        var itemsPerCycle = ReadPositiveInt(SoakItemsEnvVar, DefaultItemsPerCycle);
        var cycleDelay = TimeSpan.FromMilliseconds(ReadNonNegativeInt(SoakCycleDelayMsEnvVar, DefaultCycleDelayMs));
        var maxManagedGrowthBytes = MiB(ReadPositiveInt(ManagedBudgetMiBEnvVar, DefaultMaxManagedGrowthMiB));
        var maxPrivateGrowthBytes = MiB(ReadPositiveInt(PrivateBudgetMiBEnvVar, DefaultMaxPrivateGrowthMiB));
        var reportPath = Environment.GetEnvironmentVariable(SoakReportPathEnvVar);
        var commit = Environment.GetEnvironmentVariable(SoakCommitEnvVar) ?? "unknown";

        string workDir = Path.Combine(Path.GetTempPath(), "Sockseek-soak-work-" + Guid.NewGuid());
        string musicRoot = Path.Combine(workDir, "music");
        string inputDir = Path.Combine(workDir, "input");
        string outputDir = Path.Combine(Path.GetTempPath(), "Sockseek-soak-out-" + Guid.NewGuid());
        Directory.CreateDirectory(musicRoot);
        Directory.CreateDirectory(inputDir);
        Directory.CreateDirectory(outputDir);

        using var cts = new CancellationTokenSource();
        Task runTask = Task.CompletedTask;

        try
        {
            var supervisor = CreateSupervisor(musicRoot, outputDir);
            runTask = supervisor.RunAsync(cts.Token);

            var start = CaptureMemory();
            var peak = start;
            var startedAtUtc = DateTimeOffset.UtcNow;
            var deadline = startedAtUtc + duration;
            var cycle = 0;

            TestContext.WriteLine(
                $"Starting soak for {duration}. Items/cycle={itemsPerCycle}. Cycle delay={cycleDelay}. Managed budget={FormatBytes(maxManagedGrowthBytes)}, private budget={FormatBytes(maxPrivateGrowthBytes)}.");

            while (DateTimeOffset.UtcNow < deadline)
            {
                cycle++;
                string csvPath = Path.Combine(inputDir, $"soak-{cycle:D6}.csv");
                File.WriteAllLines(csvPath, BuildCsvLines(cycle, itemsPerCycle));

                var root = await supervisor.SubmitExtractJobAsync(
                    new SubmitExtractJobRequestDto(
                        csvPath,
                        InputType: "CSV",
                        AutoStartExtractedResult: true),
                    CancellationToken.None);

                await WaitForWorkflowInactiveAsync(supervisor, root.WorkflowId);

                if (cycle % 10 == 0)
                {
                    var sample = CaptureMemory();
                    peak = MemorySample.Max(peak, sample);
                    TestContext.WriteLine(
                        $"cycle={cycle}, managed={FormatBytes(sample.ManagedHeapBytes)}, private={FormatBytes(sample.PrivateBytes)}, workflows={supervisor.StateStore.GetWorkflows().Count}");
                }

                if (cycleDelay > TimeSpan.Zero && DateTimeOffset.UtcNow + cycleDelay < deadline)
                    await Task.Delay(cycleDelay, CancellationToken.None);
            }

            var end = CaptureMemory();
            var completedAtUtc = DateTimeOffset.UtcNow;
            peak = MemorySample.Max(peak, end);
            var managedGrowth = peak.ManagedHeapBytes - start.ManagedHeapBytes;
            var privateGrowth = peak.PrivateBytes - start.PrivateBytes;
            var retainedWorkflowCount = supervisor.StateStore.GetWorkflows().Count;

            TestContext.WriteLine(
                $"Completed {cycle} soak cycles. Start managed={FormatBytes(start.ManagedHeapBytes)}, peak managed={FormatBytes(peak.ManagedHeapBytes)}, managed growth={FormatBytes(managedGrowth)}. Start private={FormatBytes(start.PrivateBytes)}, peak private={FormatBytes(peak.PrivateBytes)}, private growth={FormatBytes(privateGrowth)}.");

            WriteReportIfRequested(
                reportPath,
                new SoakStabilityReport(
                    Commit: commit,
                    StartedAtUtc: startedAtUtc,
                    CompletedAtUtc: completedAtUtc,
                    RequestedDuration: duration.ToString(),
                    ActualDuration: (completedAtUtc - startedAtUtc).ToString(),
                    CycleCount: cycle,
                    RetainedWorkflowCount: retainedWorkflowCount,
                    ItemsPerCycle: itemsPerCycle,
                    CycleDelayMilliseconds: (int)cycleDelay.TotalMilliseconds,
                    StartManagedHeapBytes: start.ManagedHeapBytes,
                    PeakManagedHeapBytes: peak.ManagedHeapBytes,
                    ManagedHeapGrowthBytes: managedGrowth,
                    MaxManagedHeapGrowthBytes: maxManagedGrowthBytes,
                    StartPrivateBytes: start.PrivateBytes,
                    PeakPrivateBytes: peak.PrivateBytes,
                    PrivateGrowthBytes: privateGrowth,
                    MaxPrivateGrowthBytes: maxPrivateGrowthBytes));

            Assert.IsTrue(
                managedGrowth <= maxManagedGrowthBytes,
                $"Managed heap growth exceeded budget. Expected <= {FormatBytes(maxManagedGrowthBytes)}, actual {FormatBytes(managedGrowth)}.");
            Assert.IsTrue(
                privateGrowth <= maxPrivateGrowthBytes,
                $"Private memory growth exceeded budget. Expected <= {FormatBytes(maxPrivateGrowthBytes)}, actual {FormatBytes(privateGrowth)}.");
        }
        finally
        {
            cts.Cancel();
            await runTask;
            if (Directory.Exists(workDir))
                Directory.Delete(workDir, true);
            if (Directory.Exists(outputDir))
                Directory.Delete(outputDir, true);
        }
    }

    private static EngineSupervisor CreateSupervisor(string musicRoot, string outputDir)
    {
        var options = Options.Create(new ServerOptions
        {
            Engine = new EngineSettings
            {
                MockFilesDir = musicRoot,
                MockFilesReadTags = false,
                ConcurrentJobs = 20,
                ConcurrentSearches = 2,
                SearchesPerTime = 10_000,
                SearchRenewTime = 1,
            },
            DefaultDownload = new DownloadSettings
            {
                Output =
                {
                    ParentDir = outputDir,
                    NameFormat = "{filename}",
                },
            },
            Profiles = ProfileCatalog.Empty,
        });

        return new EngineSupervisor(options);
    }

    private async Task WaitForWorkflowInactiveAsync(EngineSupervisor supervisor, Guid workflowId)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        WorkflowSummaryDto? lastSummary = null;

        while (!timeout.IsCancellationRequested)
        {
            lastSummary = supervisor.StateStore.GetWorkflowSummary(workflowId);
            if (lastSummary?.ActiveJobCount == 0)
                return;

            try
            {
                await Task.Delay(25, timeout.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        string last = lastSummary == null
            ? "<missing>"
            : $"state={lastSummary.State} active={lastSummary.ActiveJobCount} completed={lastSummary.CompletedJobCount} failed={lastSummary.FailedJobCount}";
        Assert.Fail($"Timed out waiting for workflow to become inactive. Last summary: {last}.");
    }

    private static IEnumerable<string> BuildCsvLines(int cycle, int count)
    {
        yield return "artist,title,album";

        foreach (int i in Enumerable.Range(1, count))
            yield return $"Soak Artist {cycle:D6},Soak Track {cycle:D6}-{i:D5},Soak Album";
    }

    private static MemorySample CaptureMemory()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        using var process = Process.GetCurrentProcess();
        process.Refresh();
        return new MemorySample(
            GC.GetTotalMemory(forceFullCollection: false),
            process.PrivateMemorySize64);
    }

    private static bool ShouldRunSoak()
        => string.Equals(Environment.GetEnvironmentVariable(RunSoakEnvVar), "1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Environment.GetEnvironmentVariable(RunSoakEnvVar), "true", StringComparison.OrdinalIgnoreCase);

    private static int ReadPositiveInt(string envVar, int fallback)
        => int.TryParse(Environment.GetEnvironmentVariable(envVar), out int parsed) && parsed > 0
            ? parsed
            : fallback;

    private static int ReadNonNegativeInt(string envVar, int fallback)
        => int.TryParse(Environment.GetEnvironmentVariable(envVar), out int parsed) && parsed >= 0
            ? parsed
            : fallback;

    private static long MiB(int value) => value * 1024L * 1024L;

    private static string FormatBytes(long bytes)
        => bytes < 1024
            ? $"{bytes} B"
            : bytes < 1024 * 1024
                ? $"{bytes / 1024.0:F1} KiB"
                : $"{bytes / (1024.0 * 1024.0):F2} MiB";

    private void WriteReportIfRequested(string? reportPath, SoakStabilityReport report)
    {
        if (string.IsNullOrWhiteSpace(reportPath))
            return;

        var fullPath = Path.GetFullPath(reportPath);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        var options = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(fullPath, JsonSerializer.Serialize(report, options));
        TestContext.WriteLine($"Wrote soak stability report to {fullPath}.");
    }

    private sealed record MemorySample(long ManagedHeapBytes, long PrivateBytes)
    {
        public static MemorySample Max(MemorySample left, MemorySample right)
            => new(
                Math.Max(left.ManagedHeapBytes, right.ManagedHeapBytes),
                Math.Max(left.PrivateBytes, right.PrivateBytes));
    }

    private sealed record SoakStabilityReport(
        string Commit,
        DateTimeOffset StartedAtUtc,
        DateTimeOffset CompletedAtUtc,
        string RequestedDuration,
        string ActualDuration,
        int CycleCount,
        int RetainedWorkflowCount,
        int ItemsPerCycle,
        int CycleDelayMilliseconds,
        long StartManagedHeapBytes,
        long PeakManagedHeapBytes,
        long ManagedHeapGrowthBytes,
        long MaxManagedHeapGrowthBytes,
        long StartPrivateBytes,
        long PeakPrivateBytes,
        long PrivateGrowthBytes,
        long MaxPrivateGrowthBytes);
}
