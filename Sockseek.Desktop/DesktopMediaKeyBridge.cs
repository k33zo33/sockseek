using System.Runtime.InteropServices;

namespace Sockseek.Desktop;

internal interface IDesktopMediaKeyBridge : IDisposable
{
    event Action<DesktopPlayerInput>? PlayerInputReceived;
}

internal sealed class DesktopMediaKeyBridgeRouter : IDisposable
{
    private readonly IDesktopMediaKeyBridge bridge;
    private readonly Action<DesktopPlayerInput> dispatch;
    private bool disposed;

    public DesktopMediaKeyBridgeRouter(
        IDesktopMediaKeyBridge bridge,
        Action<DesktopPlayerInput> dispatch)
    {
        this.bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        this.dispatch = dispatch ?? throw new ArgumentNullException(nameof(dispatch));
        this.bridge.PlayerInputReceived += HandlePlayerInputReceived;
    }

    public void Dispose()
    {
        if (disposed)
            return;

        bridge.PlayerInputReceived -= HandlePlayerInputReceived;
        bridge.Dispose();
        disposed = true;
    }

    private void HandlePlayerInputReceived(DesktopPlayerInput input)
    {
        if (!disposed)
            dispatch(input);
    }
}

internal static class DesktopMediaKeyBridge
{
    public static IDesktopMediaKeyBridge CreateDefault()
        => OperatingSystem.IsWindows()
            ? new WindowsDesktopMediaKeyBridge()
            : new NoOpDesktopMediaKeyBridge();
}

internal sealed class NoOpDesktopMediaKeyBridge : IDesktopMediaKeyBridge
{
    public event Action<DesktopPlayerInput>? PlayerInputReceived
    {
        add { }
        remove { }
    }

    public void Dispose()
    {
    }
}

internal sealed class WindowsDesktopMediaKeyBridge : IDesktopMediaKeyBridge
{
    private const int PlayPauseHotKeyId = 1;
    private const int PreviousHotKeyId = 2;
    private const int NextHotKeyId = 3;
    private const int MuteHotKeyId = 4;
    private const int VkMediaNextTrack = 0xB0;
    private const int VkMediaPreviousTrack = 0xB1;
    private const int VkMediaPlayPause = 0xB3;
    private const int VkVolumeMute = 0xAD;
    private const int WmHotKey = 0x0312;
    private const int WmClose = 0x0010;
    private const int WmDestroy = 0x0002;
    private const uint ModNoRepeat = 0x4000;
    private static readonly IntPtr HwndMessage = new(-3);

    private readonly ManualResetEventSlim ready = new(false);
    private readonly Thread messageThread;
    private readonly Win32MessageWindow window = new();
    private IntPtr hwnd;
    private bool disposed;

    public WindowsDesktopMediaKeyBridge()
    {
        messageThread = new Thread(RunMessageLoop)
        {
            IsBackground = true,
            Name = "Sockseek Windows media key bridge",
        };
        messageThread.Start();
        ready.Wait(TimeSpan.FromSeconds(2));
    }

    public event Action<DesktopPlayerInput>? PlayerInputReceived;

    internal static bool TryMapHotKeyId(int id, out DesktopPlayerInput input)
    {
        input = id switch
        {
            PreviousHotKeyId => DesktopPlayerInput.Previous,
            PlayPauseHotKeyId => DesktopPlayerInput.TogglePlayPause,
            NextHotKeyId => DesktopPlayerInput.Next,
            MuteHotKeyId => DesktopPlayerInput.ToggleMute,
            _ => default,
        };

        return id is PreviousHotKeyId or PlayPauseHotKeyId or NextHotKeyId or MuteHotKeyId;
    }

    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;
        var currentHwnd = hwnd;
        if (currentHwnd != IntPtr.Zero)
            _ = NativeMethods.PostMessage(currentHwnd, WmClose, IntPtr.Zero, IntPtr.Zero);

        if (messageThread.IsAlive && !messageThread.Join(TimeSpan.FromSeconds(2)))
            NativeMethods.PostQuitMessage(0);

        ready.Dispose();
    }

    private void RunMessageLoop()
    {
        try
        {
            hwnd = window.Create(HandleWindowMessage);
            if (hwnd == IntPtr.Zero)
                return;

            RegisterHotKeys(hwnd);
            ready.Set();

            while (NativeMethods.GetMessage(out var message, IntPtr.Zero, 0, 0) > 0)
            {
                NativeMethods.TranslateMessage(ref message);
                NativeMethods.DispatchMessage(ref message);
            }
        }
        finally
        {
            ready.Set();
            if (hwnd != IntPtr.Zero)
            {
                UnregisterHotKeys(hwnd);
                hwnd = IntPtr.Zero;
            }
        }
    }

    private IntPtr HandleWindowMessage(IntPtr windowHandle, uint message, IntPtr wParam, IntPtr lParam)
    {
        _ = lParam;
        if (message == WmHotKey && TryMapHotKeyId(wParam.ToInt32(), out var input))
        {
            PlayerInputReceived?.Invoke(input);
            return IntPtr.Zero;
        }

        if (message == WmClose)
        {
            NativeMethods.DestroyWindow(windowHandle);
            return IntPtr.Zero;
        }

        if (message == WmDestroy)
        {
            NativeMethods.PostQuitMessage(0);
            return IntPtr.Zero;
        }

        return NativeMethods.DefWindowProc(windowHandle, message, wParam, lParam);
    }

    private static void RegisterHotKeys(IntPtr windowHandle)
    {
        _ = NativeMethods.RegisterHotKey(windowHandle, PlayPauseHotKeyId, ModNoRepeat, VkMediaPlayPause);
        _ = NativeMethods.RegisterHotKey(windowHandle, PreviousHotKeyId, ModNoRepeat, VkMediaPreviousTrack);
        _ = NativeMethods.RegisterHotKey(windowHandle, NextHotKeyId, ModNoRepeat, VkMediaNextTrack);
        _ = NativeMethods.RegisterHotKey(windowHandle, MuteHotKeyId, ModNoRepeat, VkVolumeMute);
    }

    private static void UnregisterHotKeys(IntPtr windowHandle)
    {
        _ = NativeMethods.UnregisterHotKey(windowHandle, PlayPauseHotKeyId);
        _ = NativeMethods.UnregisterHotKey(windowHandle, PreviousHotKeyId);
        _ = NativeMethods.UnregisterHotKey(windowHandle, NextHotKeyId);
        _ = NativeMethods.UnregisterHotKey(windowHandle, MuteHotKeyId);
    }

    private sealed class Win32MessageWindow
    {
        private readonly NativeMethods.WindowProcedure procedure;
        private readonly string className = "SockseekMediaKeyBridge_" + Guid.NewGuid().ToString("N");

        public Win32MessageWindow()
            => procedure = Dispatch;

        private Func<IntPtr, uint, IntPtr, IntPtr, IntPtr>? handler;

        public IntPtr Create(Func<IntPtr, uint, IntPtr, IntPtr, IntPtr> messageHandler)
        {
            handler = messageHandler;
            var instance = NativeMethods.GetModuleHandle(null);
            var windowClass = new NativeMethods.WindowClass
            {
                lpfnWndProc = procedure,
                hInstance = instance,
                lpszClassName = className,
            };

            var atom = NativeMethods.RegisterClass(ref windowClass);
            if (atom == 0)
                return IntPtr.Zero;

            return NativeMethods.CreateWindowEx(
                0,
                className,
                className,
                0,
                0,
                0,
                0,
                0,
                HwndMessage,
                IntPtr.Zero,
                instance,
                IntPtr.Zero);
        }

        private IntPtr Dispatch(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam)
            => handler?.Invoke(hwnd, message, wParam, lParam)
                ?? NativeMethods.DefWindowProc(hwnd, message, wParam, lParam);
    }

    private static class NativeMethods
    {
        public delegate IntPtr WindowProcedure(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct WindowClass
        {
            public uint style;
            public WindowProcedure lpfnWndProc;
            public int cbClsExtra;
            public int cbWndExtra;
            public IntPtr hInstance;
            public IntPtr hIcon;
            public IntPtr hCursor;
            public IntPtr hbrBackground;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string? lpszMenuName;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string lpszClassName;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct Message
        {
            public IntPtr hwnd;
            public uint message;
            public IntPtr wParam;
            public IntPtr lParam;
            public uint time;
            public int ptX;
            public int ptY;
        }

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern ushort RegisterClass(ref WindowClass windowClass);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern IntPtr CreateWindowEx(
            uint exStyle,
            string className,
            string windowName,
            uint style,
            int x,
            int y,
            int width,
            int height,
            IntPtr parent,
            IntPtr menu,
            IntPtr instance,
            IntPtr param);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DestroyWindow(IntPtr hwnd);

        [DllImport("user32.dll")]
        public static extern IntPtr DefWindowProc(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, int virtualKey);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool UnregisterHotKey(IntPtr hwnd, int id);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool PostMessage(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern void PostQuitMessage(int exitCode);

        [DllImport("user32.dll")]
        public static extern int GetMessage(out Message message, IntPtr hwnd, uint minFilter, uint maxFilter);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool TranslateMessage(ref Message message);

        [DllImport("user32.dll")]
        public static extern IntPtr DispatchMessage(ref Message message);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr GetModuleHandle(string? moduleName);
    }
}
