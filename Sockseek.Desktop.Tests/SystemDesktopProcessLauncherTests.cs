using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Sockseek.Desktop.Tests;

[TestClass]
public class SystemDesktopProcessLauncherTests
{
    [TestMethod]
    public async Task LaunchAsync_ProcessWritesStdoutAndStderr_ReadOutputLinesReturnsBothStreams()
    {
        var launcher = new SystemDesktopProcessLauncher();
        var request = new DesktopDaemonLaunchRequest(
            "bash",
            "-lc \"echo stdout-line; echo stderr-line 1>&2\"",
            "/tmp",
            new Dictionary<string, string?>());

        await using var session = await launcher.LaunchAsync(request);
        var lines = new List<string>();

        await foreach (var line in session.ReadOutputLinesAsync())
            lines.Add(line);

        CollectionAssert.Contains(lines, "stdout-line");
        CollectionAssert.Contains(lines, "stderr-line");
    }
}
