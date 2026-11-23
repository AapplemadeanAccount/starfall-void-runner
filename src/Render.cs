using System;
using System.Collections.Generic;

namespace Starfall
{
    // Small 5x5 bitmap font used for the big title / game-over lettering.
    public static class BigFont
    {
        static readonly Dictionary<char, string[]> Glyphs = new Dictionary<char, string[]>
        {
            { 'A', new[] { ".###.", "#...#", "#####", "#...#", "#...#" } },
            { 'C', new[] { ".###.", "#....", "#....", "#....", ".###." } },
            { 'D', new[] { "####.", "#...#", "#...#", "#...#", "####." } },
            { 'E', new[] { "#####", "#....", "####.", "#....", "#####" } },
            { 'F', new[] { "#####", "#....", "####.", "#....", "#...." } },
            { 'G', new[] { ".###.", "#....", "#..##", "#...#", ".###." } },
            { 'H', new[] { "#...#", "#...#", "#####", "#...#", "#...#" } },
            { 'I', new[] { "#####", "..#..", "..#..", "..#..", "#####" } },
            { 'L', new[] { "#....", "#....", "#....", "#....", "#####" } },
            { 'M', new[] { "#...#", "##.##", "#.#.#", "#...#", "#...#" } },
            { 'N', new[] { "#...#", "##..#", "#.#.#", "#..##", "#...#" } },
            { 'O', new[] { ".###.", "#...#", "#...#", "#...#", ".###." } },
            { 'P', new[] { "####.", "#...#", "####.", "#....", "#...." } },
            { 'R', new[] { "####.", "#...#", "####.", "#..#.", "#...#" } },
            { 'S', new[] { ".####", "#....", ".###.", "....#", "####." } },
            { 'T', new[] { "#####", "..#..", "..#..", "..#..", "..#.." } },
            { 'U', new[] { "#...#", "#...#", "#...#", "#...#", ".###." } },
            { 'V', new[] { "#...#", "#...#", "#...#", ".#.#.", "..#.." } },
            { 'W', new[] { "#...#", "#...#", "#.#.#", "##.##", "#...#" } },
            { 'Y', new[] { "#...#", ".#.#.", "..#..", "..#..", "..#.." } },
            { ' ', new[] { ".....", ".....", ".....", ".....", "....." } },
            { '!', new[] { "..#..", "..#..", "..#..", ".....", "..#.." } },
            { '-', new[] { ".....", ".....", "#####", ".....", "....." } },
            { '.', new[] { ".....", ".....", ".....", ".....", "..#.." } },
        };

        public static int Width(string text, int scale)
        {
            return text.Length * 6 * scale - scale;
        }

        public static int Height(int scale)
        {
            return 5 * scale;
        }

        // centerX is the centre cell of the text; top is the first row.
        public static void Draw(Screen s, string text, int centerX, int top, int scale, byte[] rowColors, byte shadow)
        {
            if (text == null || text.Length == 0) return;
            text = text.ToUpperInvariant();
            int w = Width(text, scale);
            int x0 = centerX - w / 2;

            // drop shadow first, then solid colour blocks so the lettering reads
            // cleanly in any console font.
            DrawPass(s, text, x0, top + 1, scale, null, shadow, false);
            DrawPass(s, text, x0, top, scale, rowColors, 0, true);
        }

        static void DrawPass(Screen s, string text, int x0, int top, int scale, byte[] rowColors, byte fixedColor, bool filled)
        {
            for (int i = 0; i < text.Length; i++)
            {
                string[] g;
                if (!Glyphs.TryGetValue(text[i], out g)) continue;
                int gx = x0 + i * 6 * scale;
                for (int row = 0; row < 5; row++)
                {
                    byte color = (rowColors != null && row < rowColors.Length) ? rowColors[row] : fixedColor;
                    byte bg = filled ? color : (byte)0;
                    for (int col = 0; col < 5; col++)
                    {
                        if (g[row][col] != '#') continue;
                        for (int dy = 0; dy < scale; dy++)
                            for (int dx = 0; dx < scale; dx++)
                                s.Direct(gx + col * scale + dx, top + row * scale + dy, '#', color, bg);
                    }
                }
            }
        }
    }

    public static class Draw
    {
        public static void Sprite(Screen s, int x, int y, string[] rows, byte color)
        {
            for (int r = 0; r < rows.Length; r++)
            {
                string line = rows[r];
                for (int c = 0; c < line.Length; c++)
                {
                    char ch = line[c];
                    if (ch == ' ') continue;
                    s.Set(x + c, y + r, ch, color);
                }
            }
        }

        public static void SpriteCentered(Screen s, float centerX, float centerY, string[] rows, byte color)
        {
            int w = 0;
            for (int i = 0; i < rows.Length; i++) if (rows[i].Length > w) w = rows[i].Length;
            int x = (int)(centerX + 0.5f) - w / 2;
            int y = (int)(centerY + 0.5f) - rows.Length / 2;
            Sprite(s, x, y, rows, color);
        }

        public static void Panel(Screen s, int x, int y, int w, int h, byte border, byte fill)
        {
            if (w < 4 || h < 3) return;
            for (int yy = y; yy < y + h; yy++)
                for (int xx = x; xx < x + w; xx++)
                {
                    bool edge = yy == y || yy == y + h - 1 || xx == x || xx == x + w - 1;
                    char c = ' ';
                    if (edge)
                    {
                        if (yy == y || yy == y + h - 1) c = '=';
                        else c = '|';
                    }
                    s.Direct(xx, yy, c, edge ? border : Palette.White, edge ? fill : fill);
                }
            s.Direct(x, y, '+', border, fill);
            s.Direct(x + w - 1, y, '+', border, fill);
            s.Direct(x, y + h - 1, '+', border, fill);
            s.Direct(x + w - 1, y + h - 1, '+', border, fill);
        }

        public static string Bar(int value, int max, int width, char on, char off)
        {
            if (width < 1) width = 1;
            int filled = (int)Math.Round((double)value / Math.Max(1, max) * width);
            if (filled < 0) filled = 0;
            if (filled > width) filled = width;
            return new string(on, filled) + new string(off, width - filled);
        }

        public static string Pad(string s, int width)
        {
            if (s == null) s = string.Empty;
            if (s.Length > width) return s.Substring(0, width);
            return s + new string(' ', width - s.Length);
        }
    }
}
