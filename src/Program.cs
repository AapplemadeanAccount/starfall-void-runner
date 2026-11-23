using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace Starfall
{
    public static class Program
    {
        [DllImport("kernel32.dll")]
        static extern IntPtr GetStdHandle(int nStdHandle);

        [DllImport("kernel32.dll")]
        static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

        [DllImport("kernel32.dll")]
        static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);

        const int STD_INPUT_HANDLE = -10;
        const int STD_OUTPUT_HANDLE = -11;
        const uint ENABLE_VIRTUAL_TERMINAL_PROCESSING = 0x0004;
        const uint ENABLE_QUICK_EDIT_MODE = 0x0040;
        const uint ENABLE_EXTENDED_FLAGS = 0x0080;

        static volatile bool quitRequested;

        public static int Main(string[] args)
        {
            try
            {
                if (args.Length > 0)
                {
                    string a = args[0].ToLowerInvariant();
                    if (a == "--selftest") return SelfTest.Run();
                    if (a == "--record") return Recorder.Run(args);
                    if (a == "--help" || a == "-h" || a == "/?")
                    {
                        PrintHelp();
                        return 0;
                    }
                }

                return RunGame();
            }
            catch (Exception ex)
            {
                RestoreConsole(true);
                Console.WriteLine();
                Console.WriteLine("STARFALL hit a fatal error: " + ex.Message);
                Console.WriteLine(ex.StackTrace);
                return 1;
            }
        }

        static int RunGame()
        {
            Audio.Init();
            EnableAnsi();
            TrySetupWindow();

            try { Console.Title = "STARFALL // .NET 4.8 console shooter"; }
            catch { }
            try { Console.CursorVisible = false; }
            catch { }

            Console.CancelKeyPress += OnCancel;

            int width = GetWidth();
            int height = GetHeight();

            Screen screen = new Screen(width, height);
            Game game = new Game(width, height, Environment.TickCount);
            InputPoller poller = new InputPoller();
            InputState input = new InputState();

            try { Console.Out.Write("\u001b[2J\u001b[H"); }
            catch { }
            screen.Invalidate();

            Stopwatch clock = Stopwatch.StartNew();
            double last = clock.Elapsed.TotalSeconds;
            double accumulator = 0;
            double renderAccumulator = 0;
            double fpsWindow = 0;
            int rendered = 0;
            const double Step = 1.0 / 60.0;

            while (!quitRequested)
            {
                double now = clock.Elapsed.TotalSeconds;
                double dt = now - last;
                last = now;
                if (dt > 0.25) dt = 0.25;

                poller.Poll(input);
                if (input.Mute) Audio.ToggleMute();
                if (input.Quit) break;

                int nw = GetWidth();
                int nh = GetHeight();
                if (nw != screen.Width || nh != screen.Height)
                {
                    screen.Resize(nw, nh);
                    game.Resize(nw, nh);
                    try { Console.Out.Write("\u001b[2J\u001b[H"); }
                    catch { }
                    screen.Invalidate();
                }

                accumulator += dt;
                int iterations = 0;
                while (accumulator >= Step && iterations < 5)
                {
                    game.Update((float)Step, input);
                    input.ClearEdges();
                    accumulator -= Step;
                    iterations++;
                }

                renderAccumulator += dt;
                if (renderAccumulator >= 1.0 / 30.0)
                {
                    renderAccumulator = 0;
                    game.Draw(screen);
                    string payload = screen.RenderDiff();
                    if (payload.Length > 0)
                    {
                        try { Console.Out.Write(payload); }
                        catch { }
                    }
                    rendered++;
                }

                fpsWindow += dt;
                if (fpsWindow >= 1.0)
                {
                    game.Fps = (float)(rendered / fpsWindow);
                    rendered = 0;
                    fpsWindow = 0;
                }

                Thread.Sleep(1);
            }

            game.SaveBest();
            RestoreConsole(false);
            TryLeaveGameScreen(height);
            return 0;
        }

        static void OnCancel(object sender, ConsoleCancelEventArgs e)
        {
            e.Cancel = true;
            quitRequested = true;
        }

        static void EnableAnsi()
        {
            try
            {
                IntPtr stdout = GetStdHandle(STD_OUTPUT_HANDLE);
                uint mode;
                if (GetConsoleMode(stdout, out mode))
                    SetConsoleMode(stdout, mode | ENABLE_VIRTUAL_TERMINAL_PROCESSING);

                IntPtr stdin = GetStdHandle(STD_INPUT_HANDLE);
                if (GetConsoleMode(stdin, out mode))
                    SetConsoleMode(stdin, (mode | ENABLE_EXTENDED_FLAGS) & ~ENABLE_QUICK_EDIT_MODE);
            }
            catch { }
        }

        static void TrySetupWindow()
        {
            try
            {
                int w = Math.Min(112, Console.LargestWindowWidth);
                int h = Math.Min(40, Console.LargestWindowHeight);
                if (w < 60) w = Math.Max(60, Console.WindowWidth);
                if (h < 22) h = Math.Max(22, Console.WindowHeight);

                try { Console.SetBufferSize(Math.Max(w, Console.BufferWidth), Math.Max(h, Console.BufferHeight)); }
                catch { }
                try { Console.SetWindowSize(w, h); }
                catch { }
                try { Console.SetBufferSize(Console.WindowWidth, Console.WindowHeight); }
                catch { }
            }
            catch { }
        }

        static int GetWidth()
        {
            try
            {
                int w = Console.WindowWidth;
                return w > 0 ? w : 100;
            }
            catch { return 100; }
        }

        static int GetHeight()
        {
            try
            {
                int h = Console.WindowHeight;
                return h > 0 ? h : 36;
            }
            catch { return 36; }
        }

        static void RestoreConsole(bool clear)
        {
            try
            {
                if (clear) Console.Out.Write("\u001b[0m\u001b[2J\u001b[H\u001b[?25h");
                else Console.Out.Write("\u001b[0m\u001b[?25h");
            }
            catch { }
            try { Console.CursorVisible = true; }
            catch { }
        }

        static void TryLeaveGameScreen(int height)
        {
            try
            {
                Console.Out.Write("\u001b[" + height + ";1H\r\n");
            }
            catch { }
        }

        static void PrintHelp()
        {
            Console.WriteLine("STARFALL // Void Runner");
            Console.WriteLine("A .NET 4.8 console space shooter. No WinForms, no external packages.");
            Console.WriteLine();
            Console.WriteLine("USAGE");
            Console.WriteLine("  Starfall.exe              play the game");
            Console.WriteLine("  Starfall.exe --selftest   run the built-in automated checks");
            Console.WriteLine("  Starfall.exe --record DIR [frames]");
            Console.WriteLine("                            headless autopilot demo; writes text and HTML");
            Console.WriteLine("                            frame captures into DIR");
            Console.WriteLine();
            Console.WriteLine("CONTROLS");
            Console.WriteLine("  A / D or arrows   move       SPACE / Z   fire");
            Console.WriteLine("  W / S            slide up/down      SHIFT   warp");
            Console.WriteLine("  P pause   M sound   ESC quit");
        }
    }
}
