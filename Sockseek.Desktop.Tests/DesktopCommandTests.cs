using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Sockseek.Desktop.Tests;

[TestClass]
public sealed class DesktopCommandTests
{
    [DataTestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task ExecuteAsync_RecoverableTransportFailure_DoesNotEscape(bool parameterized)
    {
        if (parameterized)
        {
            var command = new DesktopAsyncParameterCommand<int>(_ => throw new HttpRequestException("daemon unavailable"));
            await command.ExecuteAsync(1);
            return;
        }

        var parameterless = new DesktopAsyncCommand(() => throw new TaskCanceledException("daemon timeout"));
        await parameterless.ExecuteAsync();
    }

    [TestMethod]
    public async Task ExecuteAsync_ProgrammingFailure_RemainsVisible()
    {
        var command = new DesktopAsyncCommand(() => throw new InvalidOperationException("bug"));

        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => command.ExecuteAsync());
    }
}
