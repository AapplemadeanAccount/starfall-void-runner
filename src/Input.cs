using System;
using System.Runtime.InteropServices;

namespace Starfall
{
    public class InputState
    {
        public bool Left;
        public bool Right;
        public bool Up;
        public bool Down;
        public bool Fire;
        public bool Warp;

        // one-shot events
        public bool Start;
        public bool Restart;
        public bool Pause;
        public bool Mute;
        public bool Quit;
        public bool Help;

        public void ClearEdges()
        {
            Start = false;
            Restart = false;
            Pause = false;
            Mute = false;
            Quit = false;
            Help = false;
        }
    }

    public class InputPoller
    {
        [DllImport("user32.dll")]
        static extern short GetAsyncKeyState(int vKey);

        bool hasConsole = true;

        public bool HasConsole { get { return hasConsole; } }

        public void Poll(InputState s)
        {
            // Held state comes from Win32 so movement is smooth even though a
            // console has no key-up events.
            s.Left = Down(0x25) || Down(0x41);   // left / A
            s.Right = Down(0x27) || Down(0x44);  // right / D
            s.Up = Down(0x26) || Down(0x57);     // up / W
            s.Down = Down(0x28) || Down(0x53);   // down / S
            s.Fire = Down(0x20) || Down(0x5A) || Down(0x4A); // space / Z / J
            s.Warp = Down(0x10);                 // shift

            if (!hasConsole) return;

            try
            {
                while (Console.KeyAvailable)
                {
                    ConsoleKeyInfo k = Console.ReadKey(true);
                    switch (k.Key)
                    {
                        case ConsoleKey.Enter:
                            s.Start = true;
                            break;
                        case ConsoleKey.R:
                            s.Restart = true;
                            break;
                        case ConsoleKey.P:
                            s.Pause = true;
                            break;
                        case ConsoleKey.M:
                            s.Mute = true;
                            break;
                        case ConsoleKey.Escape:
                            s.Quit = true;
                            break;
                        case ConsoleKey.Q:
                            if ((k.Modifiers & ConsoleModifiers.Control) != 0) s.Quit = true;
                            break;
                        case ConsoleKey.H:
                            s.Help = true;
                            break;
                    }
                }
            }
            catch
            {
                hasConsole = false;
            }
        }

        static bool Down(int vk)
        {
            try
            {
                return (GetAsyncKeyState(vk) & 0x8000) != 0;
            }
            catch
            {
                return false;
            }
        }
    }
}
