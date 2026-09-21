using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Sockseek.Desktop.Tests;

[TestClass]
public sealed class DesktopMediaKeyBridgeTests
{
    [DataTestMethod]
    [DataRow(1, nameof(DesktopPlayerInput.TogglePlayPause))]
    [DataRow(2, nameof(DesktopPlayerInput.Previous))]
    [DataRow(3, nameof(DesktopPlayerInput.Next))]
    [DataRow(4, nameof(DesktopPlayerInput.ToggleMute))]
    public void WindowsBridge_TryMapHotKeyId_MapsSupportedInputs(int id, string expectedInput)
    {
        var mapped = WindowsDesktopMediaKeyBridge.TryMapHotKeyId(id, out var input);

        Assert.IsTrue(mapped);
        Assert.AreEqual(expectedInput, input.ToString());
    }

    [TestMethod]
    public void WindowsBridge_TryMapHotKeyId_RejectsUnknownInput()
        => Assert.IsFalse(WindowsDesktopMediaKeyBridge.TryMapHotKeyId(99, out _));

    [TestMethod]
    public void Router_ForwardsBridgeInputUntilDisposed()
    {
        using var bridge = new FakeMediaKeyBridge();
        var received = new List<DesktopPlayerInput>();
        using var router = new DesktopMediaKeyBridgeRouter(bridge, received.Add);

        bridge.Raise(DesktopPlayerInput.Next);
        router.Dispose();
        bridge.Raise(DesktopPlayerInput.Previous);

        CollectionAssert.AreEqual(new[] { DesktopPlayerInput.Next }, received.ToArray());
        Assert.IsTrue(bridge.IsDisposed);
    }

    private sealed class FakeMediaKeyBridge : IDesktopMediaKeyBridge
    {
        public event Action<DesktopPlayerInput>? PlayerInputReceived;

        public bool IsDisposed { get; private set; }

        public void Raise(DesktopPlayerInput input)
            => PlayerInputReceived?.Invoke(input);

        public void Dispose()
            => IsDisposed = true;
    }
}
