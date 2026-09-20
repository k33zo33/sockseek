using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sockseek.Application.Playback;
using Sockseek.Core.Settings;
using Sockseek.Player;
using Sockseek.Server;

namespace Tests.Server;

[TestClass]
public sealed class PlaybackCompositionTests
{
    [TestMethod]
    public async Task ServerHost_RegistersLocalPlaybackSourceResolverAndCoordinator()
    {
        using var temp = TemporaryDirectory.Create();
        await using var app = ServerHost.Build([], new ServerOptions
        {
            DatabasePath = Path.Combine(temp.Path, "sockseek.db"),
            DatabaseBackupDir = Path.Combine(temp.Path, "backups"),
            Engine = new EngineSettings
            {
                MockFilesDir = temp.Path,
            },
            DefaultDownload = new DownloadSettings
            {
                Output =
                {
                    ParentDir = temp.Path,
                },
            },
            Profiles = ProfileCatalog.Empty,
            SessionToken = "composition-test-token",
        }, "http://127.0.0.1:0");

        using var scope = app.Services.CreateScope();
        using var otherScope = app.Services.CreateScope();

        Assert.IsNotNull(scope.ServiceProvider.GetRequiredService<IPlaybackSourceResolver>());
        var coordinator = scope.ServiceProvider.GetRequiredService<PlaybackCoordinator>();

        Assert.IsNotNull(coordinator);
        Assert.AreSame(coordinator, otherScope.ServiceProvider.GetRequiredService<PlaybackCoordinator>());
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        private TemporaryDirectory(string path)
            => Path = path;

        public string Path { get; }

        public static TemporaryDirectory Create()
        {
            string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "sockseek-playback-composition-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return new TemporaryDirectory(path);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }
}
