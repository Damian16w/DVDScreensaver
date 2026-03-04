using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Linq;

class DvdScreensaver
{
    [DllImport("user32.dll")]
    static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

    [DllImport("user32.dll")]
    static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll")]
    static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

    [DllImport("user32.dll")]
    static extern int GetSystemMetrics(int nIndex);

    [DllImport("user32.dll")]
    static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    static extern bool IsZoomed(IntPtr hWnd);

    const int SW_RESTORE = 9;
    const int SW_MINIMIZE = 6;

    // Delegate for window enumeration
    delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    // Rectangle structure
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    // Holds information about a window
    class WindowInfo
    {
        public IntPtr Handle { get; set; }
        public string? Title { get; set; } = "";
        public RECT Rect { get; set; }
        public int Width => Rect.Right - Rect.Left;
        public int Height => Rect.Bottom - Rect.Top;
    }

    static bool running = true;
    const int BounceWidth = 800;
    const int BounceHeight = 600;
    const int MaxNotepads = 3;
    const int SW_MAXIMIZE = 3;

    /// <summary>
    /// Entry point. Initializes and starts the screensaver animation.
    /// </summary>
    static void Main()
    {
        var settings = ScreensaverSettings.Instance;

        // Show/edit settings if UseTimer is true
        if (settings.UseTimer)
        {
            Console.WriteLine("Screensaver Settings:");
            Console.WriteLine($"1. Speed Multiplier: {settings.SpeedMultiplier}");
            Console.WriteLine($"2. Max Windows: {settings.MaxWindows}");
            Console.WriteLine($"3. Timer Duration (seconds): {settings.TimerDurationSeconds}");
            Console.WriteLine("Press Enter to keep value, or type a new value and press Enter.");

            Console.Write("Speed Multiplier: ");
            var input = Console.ReadLine();
            if (!string.IsNullOrWhiteSpace(input) && double.TryParse(input, out double sm))
                settings.SpeedMultiplier = sm;

            Console.Write("Max Windows: ");
            input = Console.ReadLine();
            if (!string.IsNullOrWhiteSpace(input) && int.TryParse(input, out int mw))
                settings.MaxWindows = mw;

            Console.Write("Timer Duration (seconds): ");
            input = Console.ReadLine();
            if (!string.IsNullOrWhiteSpace(input) && int.TryParse(input, out int td))
                settings.TimerDurationSeconds = td;
        }

        Console.WriteLine("DVD Window Screensaver - C# Version");
        Console.WriteLine("Press Ctrl+C to stop\n");

        Console.CancelKeyPress += (sender, e) =>
        {
            running = false;
            e.Cancel = true;
        };

        var windows = GetWindows();
        if (windows.Count == 0)
        {
            Console.WriteLine("No suitable windows found!");
            return;
        }

        // Pick random windows to bounce if UseTimer is true, else use foreground window
        List<WindowInfo> bounceWindows;
        if (settings.UseTimer && windows.Count > 0)
        {
            var rand = new Random();
            bounceWindows = windows.OrderBy(x => rand.Next()).Take(settings.MaxWindows).ToList();
        }
        else
        {
            IntPtr topHandle = GetForegroundWindow();
            var targetWindow = windows.FirstOrDefault(w => w.Handle == topHandle);
            if (targetWindow == null)
            {
                Console.WriteLine("No suitable top window found!");
                return;
            }
            bounceWindows = new List<WindowInfo> { targetWindow };
        }

        foreach (var win in bounceWindows)
        {
            ShowWindow(win.Handle, SW_RESTORE);
            System.Threading.Thread.Sleep(200);
            int screenWidth = GetSystemMetrics(0);
            int screenHeight = GetSystemMetrics(1);
            var rand = new Random();
            int startX = rand.Next(0, Math.Max(1, screenWidth - BounceWidth));
            int startY = rand.Next(0, Math.Max(1, screenHeight - BounceHeight));
            MoveWindow(win.Handle, startX, startY, BounceWidth, BounceHeight, true);
            var rect = win.Rect;
            rect.Left = startX;
            rect.Top = startY;
            rect.Right = rect.Left + BounceWidth;
            rect.Bottom = rect.Top + BounceHeight;
            win.Rect = rect;
            Console.WriteLine($"Bouncing window: {win.Title}");
        }

        DvdAnimationSwitchOnActive(BounceWidth, BounceHeight);
    }

    /// <summary>
    /// Returns a list of all visible windows with their info.
    /// </summary>
    static List<WindowInfo> GetWindows()
    {
        var windows = new List<WindowInfo>();

        EnumWindows(delegate (IntPtr hWnd, IntPtr lParam)
        {
            int length = GetWindowTextLength(hWnd);
            if (length > 0 && IsWindowVisible(hWnd))
            {
                // Filter out "Program Manager" (the desktop window)
                var sb = new System.Text.StringBuilder(length + 1);
                GetWindowText(hWnd, sb, sb.Capacity);
                string title = sb.ToString();
                if (title == "Program Manager")
                    return true;

                GetWindowRect(hWnd, out RECT rect);

                windows.Add(new WindowInfo
                {
                    Handle = hWnd,
                    Title = title,
                    Rect = rect
                });
            }
            return true;
        }, IntPtr.Zero);

        return windows;
    }

    /// <summary>
    /// Handles the bouncing animation and window switching logic.
    /// </summary>
    static void DvdAnimationSwitchOnActive(int width, int height)
    {
        int screenWidth = GetSystemMetrics(0);
        int screenHeight = GetSystemMetrics(1);

        // Set screenX and screenY to 0 for single monitor support
        int screenX = 0;
        int screenY = 0;

        const int baseSpeed = 5;

        var rand = new Random();
        var windowStates = new Dictionary<IntPtr, (int x, int y, int dx, int dy, int speed)>();
        var lastHandles = new Queue<IntPtr>();

        // Settings
        var settings = ScreensaverSettings.Instance;
        double speedMultiplier = settings.SpeedMultiplier;
        int maxWindows = settings.MaxWindows;
        bool useTimer = settings.UseTimer;
        int timerDuration = settings.TimerDurationSeconds;
        DateTime? timerStart = useTimer ? DateTime.Now : null;

        while (running)
        {
            // Timer check
            if (useTimer && timerStart.HasValue && (DateTime.Now - timerStart.Value).TotalSeconds >= timerDuration)
            {
                running = false;
                break;
            }

            IntPtr currentHandle = GetForegroundWindow();
            var windows = GetWindows();
            var window = windows.FirstOrDefault(w => w.Handle == currentHandle);

            bool shouldSwitch = false;
            if (window != null && (lastHandles.Count == 0 || currentHandle != lastHandles.Last()))
            {
                if (IsZoomed(window.Handle))
                {
                    ShowWindow(window.Handle, SW_RESTORE);
                    System.Threading.Thread.Sleep(100);
                    SetForegroundWindow(window.Handle);
                    System.Threading.Thread.Sleep(100);
                    shouldSwitch = true;
                }
                else if (!IsIconic(window.Handle))
                {
                    shouldSwitch = true;
                }
            }

            if (shouldSwitch)
            {
                if (!lastHandles.Contains(currentHandle))
                    lastHandles.Enqueue(currentHandle);
                while (lastHandles.Count > maxWindows)
                    lastHandles.Dequeue();

                int x, y, dx, dy, speed;
                if (IsIconic(window.Handle))
                {
                    ShowWindow(window.Handle, SW_RESTORE);
                    System.Threading.Thread.Sleep(100);
                    SetForegroundWindow(window.Handle);
                    System.Threading.Thread.Sleep(100);
                    GetWindowRect(window.Handle, out RECT restoredRect);
                    x = restoredRect.Left;
                    y = restoredRect.Top;
                }
                else
                {
                    x = rand.Next(0, Math.Max(1, screenWidth - width));
                    y = rand.Next(0, Math.Max(1, screenHeight - height));
                }
                int effectiveSpeed = (int)(baseSpeed * speedMultiplier);
                dx = rand.Next(0, 2) == 0 ? effectiveSpeed : -effectiveSpeed;
                dy = rand.Next(0, 2) == 0 ? effectiveSpeed : -effectiveSpeed;
                speed = effectiveSpeed;
                MoveWindow(window.Handle, x, y, width, height, true);
                windowStates[currentHandle] = (x, y, dx, dy, speed);

                Console.WriteLine($"Switched to bouncing window: {window.Title}");
            }
            else if (lastHandles.Count == 0 && window != null)
            {
                int x, y, dx, dy, speed;
                if (IsIconic(window.Handle))
                {
                    ShowWindow(window.Handle, SW_RESTORE);
                    System.Threading.Thread.Sleep(100);
                    SetForegroundWindow(window.Handle);
                    System.Threading.Thread.Sleep(100);
                    GetWindowRect(window.Handle, out RECT restoredRect);
                    x = restoredRect.Left;
                    y = restoredRect.Top;
                }
                else
                {
                    x = rand.Next(0, Math.Max(1, screenWidth - width));
                    y = rand.Next(0, Math.Max(1, screenHeight - height));
                }
                int effectiveSpeed = (int)(baseSpeed * speedMultiplier);
                dx = rand.Next(0, 2) == 0 ? effectiveSpeed : -effectiveSpeed;
                dy = rand.Next(0, 2) == 0 ? effectiveSpeed : -effectiveSpeed;
                speed = effectiveSpeed;
                MoveWindow(window.Handle, x, y, width, height, true);
                lastHandles.Enqueue(window.Handle);
                windowStates[window.Handle] = (x, y, dx, dy, speed);

                Console.WriteLine($"Started bouncing window: {window.Title}");
            }

            foreach (var handle in lastHandles.ToArray())
            {
                if (!windowStates.ContainsKey(handle))
                    continue;

                var state = windowStates[handle];
                int x = state.x + state.dx;
                int y = state.y + state.dy;
                int dx = state.dx;
                int dy = state.dy;
                int speed = state.speed;

                bool atLeft = x <= screenX;
                bool atRight = x + width >= screenX + screenWidth;
                bool atTop = y <= screenY;
                bool atBottom = y + height >= screenY + screenHeight;
                bool hitCorner = (atLeft || atRight) && (atTop || atBottom);

                // Clamp position to stay within bounds
                if (atLeft)
                {
                    x = screenX;
                    dx = Math.Abs(dx);
                }
                if (atRight)
                {
                    x = screenX + screenWidth - width;
                    dx = -Math.Abs(dx);
                }
                if (atTop)
                {
                    y = screenY;
                    dy = Math.Abs(dy);
                }
                if (atBottom)
                {
                    y = screenY + screenHeight - height;
                    dy = -Math.Abs(dy);
                }

                if (hitCorner)
                {
                    speed += (int)(2 * speedMultiplier);
                    dx = dx < 0 ? -speed : speed;
                    dy = dy < 0 ? -speed : speed;
                }

                MoveWindow(handle, x, y, width, height, true);
                windowStates[handle] = (x, y, dx, dy, speed);
            }

            int sleepMs = (int)(20 / speedMultiplier);
            if (sleepMs < 1) sleepMs = 1;
            System.Threading.Thread.Sleep(sleepMs);
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

    [DllImport("user32.dll", SetLastError = true)]
    static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string lpszWindow);

    [DllImport("user32.dll", SetLastError = true)]
    static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, string lParam);

    const uint WM_SETTEXT = 0x000C;


}

//  kijk je kan ook gewoon werken terwijl effect het dot