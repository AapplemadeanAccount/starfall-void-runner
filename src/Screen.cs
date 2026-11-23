using System;
using System.Text;

namespace Starfall
{
    // Xterm-256 colour indices used by the game.
    public static class Palette
    {
        public const byte Black = 0;
        public const byte White = 231;
        public const byte Silver = 252;
        public const byte Gray = 245;
        public const byte DimGray = 240;
        public const byte Dark = 234;
        public const byte Darker = 233;

        public const byte CyanBright = 51;
        public const byte Cyan = 45;
        public const byte Teal = 37;
        public const byte Blue = 33;
        public const byte BlueDeep = 27;
        public const byte BlueDark = 21;
        public const byte BlueNavy = 19;
        public const byte BlueInk = 17;

        public const byte Red = 196;
        public const byte RedBright = 203;
        public const byte RedDeep = 160;
        public const byte Rose = 204;

        public const byte Orange = 214;
        public const byte OrangeDeep = 208;
        public const byte Amber = 202;
        public const byte Copper = 166;

        public const byte Yellow = 226;
        public const byte Gold = 220;
        public const byte Olive = 184;

        public const byte Magenta = 201;
        public const byte Pink = 207;
        public const byte Purple = 165;
        public const byte PurpleDeep = 127;

        public const byte Green = 46;
        public const byte GreenMid = 118;
        public const byte GreenDeep = 100;

        public static readonly byte[] Fire = { 231, 226, 220, 214, 208, 202, 166, 94, 52 };
        public static readonly byte[] Ice = { 231, 195, 159, 123, 87, 51, 45, 39, 33 };
        public static readonly byte[] Toxic = { 231, 226, 190, 154, 118, 82, 46, 40, 34 };
        public static readonly byte[] Blood = { 231, 224, 210, 203, 196, 160, 124, 88 };
        public static readonly byte[] PurpleRamp = { 231, 219, 213, 207, 201, 165, 127, 90 };
    }

    // A character + colour framebuffer that emits only the cells that changed.
    // Output is raw ANSI (VT100 / xterm-256) written to the console.
    public class Screen
    {
        public int Width { get; private set; }
        public int Height { get; private set; }

        char[] chars;
        byte[] fg;
        byte[] bg;
        char[] prevChars;
        byte[] prevFg;
        byte[] prevBg;

        public int ShakeX;
        public int ShakeY;

        readonly StringBuilder sb = new StringBuilder(1 << 18);
        int lastFg = -1;
        int lastBg = -1;

        public int CellsChanged;

        public Screen(int width, int height)
        {
            Resize(width, height);
        }

        public void Resize(int width, int height)
        {
            if (width < 20) width = 20;
            if (height < 8) height = 8;

            Width = width;
            Height = height;
            int n = width * height;

            chars = new char[n];
            fg = new byte[n];
            bg = new byte[n];
            prevChars = new char[n];
            prevFg = new byte[n];
            prevBg = new byte[n];

            for (int i = 0; i < n; i++)
            {
                chars[i] = ' ';
                fg[i] = Palette.White;
                bg[i] = 0;
                prevChars[i] = '\0';
            }
            ShakeX = 0;
            ShakeY = 0;
        }

        public void Clear(byte clearBg = 0)
        {
            for (int i = 0; i < chars.Length; i++)
            {
                chars[i] = ' ';
                fg[i] = Palette.White;
                bg[i] = clearBg;
            }
        }

        public void Set(int x, int y, char c, byte fgColor, byte bgColor = 0)
        {
            Direct(x + ShakeX, y + ShakeY, c, fgColor, bgColor);
        }

        public void Direct(int x, int y, char c, byte fgColor, byte bgColor = 0)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height) return;
            int i = y * Width + x;
            chars[i] = c;
            fg[i] = fgColor;
            bg[i] = bgColor;
        }

        public void Write(int x, int y, string text, byte fgColor, byte bgColor = 0)
        {
            WriteDirect(x + ShakeX, y + ShakeY, text, fgColor, bgColor);
        }

        public void WriteDirect(int x, int y, string text, byte fgColor, byte bgColor = 0)
        {
            if (text == null) return;
            for (int i = 0; i < text.Length; i++)
                Direct(x + i, y, text[i], fgColor, bgColor);
        }

        public void WriteCentered(int y, string text, byte fgColor, byte bgColor = 0)
        {
            if (text == null) return;
            WriteDirect((Width - text.Length) / 2, y, text, fgColor, bgColor);
        }

        public void FillRect(int x, int y, int w, int h, byte fgColor, byte bgColor)
        {
            for (int yy = y; yy < y + h; yy++)
                for (int xx = x; xx < x + w; xx++)
                    Direct(xx, yy, ' ', fgColor, bgColor);
        }

        public string ToPlain()
        {
            StringBuilder b = new StringBuilder((Width + 1) * Height);
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    char c = chars[y * Width + x];
                    b.Append(c == '\0' ? ' ' : c);
                }
                b.Append('\n');
            }
            return b.ToString();
        }

        // Convert the current frame to a standalone HTML page (used by --record
        // so the visuals can be checked in a browser as well as a terminal).
        public string ToHtml(string title)
        {
            StringBuilder b = new StringBuilder(1 << 20);
            b.Append("<!doctype html><html><head><meta charset=\"utf-8\"><title>");
            b.Append(HtmlEscape(title));
            b.Append("</title><style>");
            b.Append("body{background:#05050a;color:#ddd;font-family:Consolas,'Cascadia Mono',monospace;margin:24px}");
            b.Append("h1{font:600 16px system-ui;letter-spacing:.2em;color:#7ad7ff;margin:0 0 12px}");
            b.Append("pre{font-size:13px;line-height:13px;letter-spacing:0;background:#000;padding:14px;border:1px solid #1d2a3a;border-radius:8px;display:inline-block;margin:0}");
            b.Append(".frame span{white-space:pre}");
            b.Append("small{color:#5b6b7f;font:12px system-ui}</style></head><body>");
            b.Append("<h1>").Append(HtmlEscape(title)).Append("</h1><pre class=\"frame\">");

            for (int y = 0; y < Height; y++)
            {
                int x = 0;
                while (x < Width)
                {
                    int i = y * Width + x;
                    char c = chars[i];
                    byte f = fg[i];
                    byte bcol = bg[i];

                    int run = 1;
                    while (x + run < Width)
                    {
                        int k = y * Width + x + run;
                        if (chars[k] != c || fg[k] != f || bg[k] != bcol) break;
                        run++;
                    }

                    b.Append("<span style=\"color:");
                    b.Append(RgbHex(f));
                    if (bcol != 0)
                    {
                        b.Append(";background:");
                        b.Append(RgbHex(bcol));
                    }
                    b.Append("\">");
                    for (int r = 0; r < run; r++)
                        b.Append(HtmlEscape(((c == '\0') ? " " : c.ToString())));
                    b.Append("</span>");
                    x += run;
                }
                b.Append('\n');
            }

            b.Append("</pre><br><small>Rendered from the live ANSI framebuffer &#183; ");
            b.Append(Width).Append(" x ").Append(Height).Append(" cells</small></body></html>");
            return b.ToString();
        }

        static string HtmlEscape(string s)
        {
            return s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
        }

        static string RgbHex(byte idx)
        {
            int r, g, b;
            XtermRgb(idx, out r, out g, out b);
            return "#" + r.ToString("x2") + g.ToString("x2") + b.ToString("x2");
        }

        static void XtermRgb(byte idx, out int r, out int g, out int b)
        {
            int[] base16 = {
                0x00,0x00,0x00, 0x80,0x00,0x00, 0x00,0x80,0x00, 0x80,0x80,0x00,
                0x00,0x00,0x80, 0x80,0x00,0x80, 0x00,0x80,0x80, 0xc0,0xc0,0xc0,
                0x80,0x80,0x80, 0xff,0x00,0x00, 0x00,0xff,0x00, 0xff,0xff,0x00,
                0x00,0x00,0xff, 0xff,0x00,0xff, 0x00,0xff,0xff, 0xff,0xff,0xff
            };
            int i = idx;
            if (i < 16)
            {
                r = base16[i * 3];
                g = base16[i * 3 + 1];
                b = base16[i * 3 + 2];
            }
            else if (i < 232)
            {
                int q = i - 16;
                int[] steps = { 0, 95, 135, 175, 215, 255 };
                r = steps[q / 36];
                g = steps[(q / 6) % 6];
                b = steps[q % 6];
            }
            else
            {
                int v = 8 + (i - 232) * 10;
                r = v; g = v; b = v;
            }
        }

        // Returns the ANSI payload for all changed cells. Cursor moves are absolute,
        // so this is safe to write as one block.
        public string RenderDiff()
        {
            sb.Length = 0;
            CellsChanged = 0;

            for (int y = 0; y < Height; y++)
            {
                int row = y * Width;
                int x = 0;
                while (x < Width)
                {
                    int i = row + x;
                    if (chars[i] == prevChars[i] && fg[i] == prevFg[i] && bg[i] == prevBg[i])
                    {
                        x++;
                        continue;
                    }

                    int start = x;
                    byte cFg = fg[i];
                    byte cBg = bg[i];
                    int end = x;
                    while (end < Width)
                    {
                        int k = row + end;
                        if (chars[k] == prevChars[k] && fg[k] == prevFg[k] && bg[k] == prevBg[k]) break;
                        if (fg[k] != cFg || bg[k] != cBg) break;
                        end++;
                    }

                    sb.Append("\u001b[").Append(y + 1).Append(';').Append(start + 1).Append('H');
                    if ((int)cFg != lastFg)
                    {
                        sb.Append("\u001b[38;5;").Append((int)cFg).Append('m');
                        lastFg = cFg;
                    }
                    if ((int)cBg != lastBg)
                    {
                        sb.Append("\u001b[48;5;").Append((int)cBg).Append('m');
                        lastBg = cBg;
                    }
                    for (int k = start; k < end; k++)
                    {
                        char c = chars[row + k];
                        sb.Append(c == '\0' ? ' ' : c);
                    }

                    CellsChanged += end - start;
                    for (int k = start; k < end; k++)
                    {
                        int p = row + k;
                        prevChars[p] = chars[p];
                        prevFg[p] = cFg;
                        prevBg[p] = cBg;
                    }
                    x = end;
                }
            }
            return sb.ToString();
        }

        // After an external clear-screen the terminal no longer matches prev buffers.
        public void Invalidate()
        {
            for (int i = 0; i < prevChars.Length; i++)
            {
                prevChars[i] = '\0';
                prevFg[i] = 0;
                prevBg[i] = 0;
            }
            lastFg = -1;
            lastBg = -1;
        }
    }
}
