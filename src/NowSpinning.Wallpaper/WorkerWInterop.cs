using System.Runtime.InteropServices;
using System.IO;

namespace NowSpinning.Wallpaper;

internal static partial class WorkerWInterop
{
    private const uint SpawnWorkerMessage = 0x052C;
    private const int GwlExStyle = -20;
    private const int GwlStyle = -16;
    private const long WsChild = 0x40000000L;
    private const long WsVisible = 0x10000000L;
    private const long WsClipSiblings = 0x04000000L;
    private const long WsClipChildren = 0x02000000L;
    private const long WsPopup = 0x80000000L;
    private const long WsExTransparent = 0x00000020L;
    private const long WsExToolWindow = 0x00000080L;
    private const long WsExNoActivate = 0x08000000L;
    private const long WsExTopMost = 0x00000008L;
    private const long WsExAppWindow = 0x00040000L;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpShowWindow = 0x0040;

    public static bool AttachBehindDesktopIcons(nint windowHandle, int x, int y, int width, int height)
    {
        var hierarchy = FindDesktopHierarchy();
        if (hierarchy.WallpaperWorker == nint.Zero || hierarchy.IconList == nint.Zero)
        {
            WriteDiagnostics(hierarchy, windowHandle, "attachment rejected: desktop hierarchy incomplete");
            return false;
        }

        _ = SetParent(windowHandle, hierarchy.WallpaperWorker);
        var styles = GetWindowLongPtr(windowHandle, GwlStyle).ToInt64();
        styles = (styles | WsChild | WsVisible | WsClipSiblings | WsClipChildren) & ~WsPopup;
        _ = SetWindowLongPtr(windowHandle, GwlStyle, new nint(styles));
        var exStyles = GetWindowLongPtr(windowHandle, GwlExStyle).ToInt64();
        exStyles = (exStyles | WsExTransparent | WsExToolWindow | WsExNoActivate) & ~(WsExTopMost | WsExAppWindow);
        _ = SetWindowLongPtr(windowHandle, GwlExStyle, new nint(exStyles));
        // The WorkerW also contains Windows' static wallpaper composition child. Keep
        // NowSpinning above that child, while the entire WorkerW remains below the
        // separate SHELLDLL_DefView/SysListView32 icon host.
        var attached = GetParent(windowHandle) == hierarchy.WallpaperWorker &&
            SetWindowPos(windowHandle, nint.Zero, x, y, width, height, SwpNoActivate | SwpShowWindow);
        WriteDiagnostics(hierarchy, windowHandle, attached ? "attached" : "attachment failed");
        return attached;
    }

    public static void SetClickThrough(nint windowHandle, bool enabled)
    {
        var styles = GetWindowLongPtr(windowHandle, GwlExStyle).ToInt64();
        var next = enabled ? styles | WsExTransparent : styles & ~WsExTransparent;
        SetWindowLongPtr(windowHandle, GwlExStyle, new nint(next));
    }

    public static bool IsAltPressed() => (GetAsyncKeyState(0x12) & 0x8000) != 0;

    public static bool TryGetCursorPosition(out System.Drawing.Point point)
    {
        if (GetCursorPos(out var nativePoint))
        {
            point = new(nativePoint.X, nativePoint.Y);
            return true;
        }
        point = default;
        return false;
    }

    public static bool IsFullscreenApplicationActive()
    {
        var foreground = GetForegroundWindow();
        if (foreground == nint.Zero || foreground == FindWindow("Progman", null) || IsIconic(foreground)) return false;
        if (!GetWindowRect(foreground, out var rectangle)) return false;
        var screen = System.Windows.Forms.Screen.FromHandle(foreground).Bounds;
        return rectangle.Left <= screen.Left && rectangle.Top <= screen.Top &&
            rectangle.Right >= screen.Right && rectangle.Bottom >= screen.Bottom;
    }

    private static DesktopHierarchy FindDesktopHierarchy()
    {
        var progman = FindWindow("Progman", null);
        if (progman == nint.Zero) return default;
        // Windows 10 accepts the classic message; current Windows 11 shells require the
        // undocumented 0xD handshake before exposing/reordering background WorkerWs.
        _ = SendMessageTimeout(progman, SpawnWorkerMessage, new nint(0xD), nint.Zero, 0, 1000, out _);
        _ = SendMessageTimeout(progman, SpawnWorkerMessage, new nint(0xD), new nint(1), 0, 1000, out _);
        nint iconWorker = nint.Zero;
        nint shellView = nint.Zero;
        nint iconList = nint.Zero;
        nint wallpaperWorker = nint.Zero;

        EnumWindows((topHandle, _) =>
        {
            var candidate = FindWindowEx(topHandle, nint.Zero, "SHELLDLL_DefView", null);
            if (candidate == nint.Zero) return true;
            iconWorker = topHandle;
            shellView = candidate;
            iconList = FindWindowEx(shellView, nint.Zero, "SysListView32", "FolderView");
            if (iconList == nint.Zero) iconList = FindWindowEx(shellView, nint.Zero, "SysListView32", null);
            wallpaperWorker = FindWindowEx(nint.Zero, topHandle, "WorkerW", null);
            return false;
        }, nint.Zero);

        // Current Windows 11 can keep SHELLDLL_DefView under Progman and create the
        // wallpaper WorkerW as another *child* of Progman instead of a top-level peer.
        // That child is the composited background surface; parenting directly to
        // Progman leaves us hidden beneath the opaque desktop view.
        if (wallpaperWorker == nint.Zero && iconWorker == progman)
            wallpaperWorker = FindWindowEx(progman, nint.Zero, "WorkerW", null);
        return new(progman, iconWorker, shellView, iconList, wallpaperWorker);
    }

    private static void WriteDiagnostics(DesktopHierarchy hierarchy, nint wallpaper, string result)
    {
        try
        {
            var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NowSpinning", "Logs");
            Directory.CreateDirectory(directory);
            File.AppendAllText(Path.Combine(directory, "wallpaper.log"),
                $"{DateTimeOffset.Now:O} {result}; Progman=0x{hierarchy.Progman:X}; IconWorkerW=0x{hierarchy.IconWorker:X}; SHELLDLL_DefView=0x{hierarchy.ShellView:X}; SysListView32=0x{hierarchy.IconList:X}; WallpaperWorkerW=0x{hierarchy.WallpaperWorker:X}; Wallpaper=0x{wallpaper:X}{Environment.NewLine}");
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private readonly record struct DesktopHierarchy(nint Progman, nint IconWorker, nint ShellView, nint IconList, nint WallpaperWorker);

    [LibraryImport("user32.dll", EntryPoint = "FindWindowW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial nint FindWindow(string className, string? windowName);

    [LibraryImport("user32.dll", EntryPoint = "FindWindowExW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial nint FindWindowEx(nint parent, nint after, string className, string? windowName);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool EnumWindows(EnumWindowsProc callback, nint parameter);

    [LibraryImport("user32.dll", SetLastError = true)]
    private static partial nint SetParent(nint child, nint parent);

    [LibraryImport("user32.dll")]
    private static partial nint GetParent(nint child);

    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static partial nint GetWindowLongPtr(nint window, int index);

    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static partial nint SetWindowLongPtr(nint window, int index, nint value);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetWindowPos(nint window, nint insertAfter, int x, int y, int width, int height, uint flags);

    [LibraryImport("user32.dll", EntryPoint = "SendMessageTimeoutW")]
    private static partial nint SendMessageTimeout(nint window, uint message, nint wParam, nint lParam, uint flags, uint timeout, out nint result);

    [LibraryImport("user32.dll")]
    private static partial short GetAsyncKeyState(int virtualKey);

    [LibraryImport("user32.dll")]
    private static partial nint GetForegroundWindow();

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetWindowRect(nint window, out NativeRectangle rectangle);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool IsIconic(nint window);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetCursorPos(out NativePoint point);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRectangle { public int Left; public int Top; public int Right; public int Bottom; }
    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint { public int X; public int Y; }

    private delegate bool EnumWindowsProc(nint window, nint parameter);
}
