using System;
using System.Collections.Generic;

namespace Starfall
{
    public class Star
    {
        public float X;
        public float Y;
        public float Z;        // 0 = far, 1 = near
        public float Phase;
        public byte Color;
        public byte DimColor;
    }

    // Three-ish layers of parallax stars plus hyperspace streaks.
    public class Starfield
    {
        public int Width { get; private set; }
        public int Height { get; private set; }

        readonly List<Star> stars = new List<Star>();
        readonly Random rng;
        public int StarCount { get { return stars.Count; } }
        public int StreakCells { get; private set; }
        public List<Star> Items { get { return stars; } }

        static readonly byte[] TintWhite = { 237, 240, 244, 248, 252, 231, 231 };
        static readonly byte[] TintBlue = { 17, 60, 61, 67, 110, 153, 195, 231 };
        static readonly byte[] TintGold = { 94, 130, 137, 173, 179, 215, 223 };
        static readonly byte[] TintRose = { 88, 131, 167, 174, 210, 217 };

        public Starfield(int width, int height, Random rng)
        {
            this.rng = rng;
            Width = width;
            Height = height;
            Populate();
        }

        void Populate()
        {
            int target = Math.Max(40, (Width * Height) / 34);
            while (stars.Count < target) stars.Add(MakeStar(true));
            if (stars.Count > target) stars.RemoveRange(target, stars.Count - target);
        }

        Star MakeStar(bool anywhere)
        {
            Star s = new Star();
            s.X = (float)rng.NextDouble() * Width;
            s.Y = anywhere ? (float)rng.NextDouble() * Height : -(float)rng.NextDouble() * 3f;
            s.Z = (float)rng.NextDouble();
            s.Phase = (float)(rng.NextDouble() * Math.PI * 2);
            double t = rng.NextDouble();
            byte[] tint;
            if (t < 0.58) tint = TintWhite;
            else if (t < 0.84) tint = TintBlue;
            else if (t < 0.95) tint = TintGold;
            else tint = TintRose;
            int bi = 1 + (int)(s.Z * (tint.Length - 2));
            s.Color = tint[Math.Min(tint.Length - 1, bi)];
            s.DimColor = tint[Math.Max(0, bi - 2)];
            return s;
        }

        public void Resize(int width, int height)
        {
            Width = width;
            Height = height;
            foreach (Star s in stars)
            {
                if (s.X > width - 1) s.X = (float)rng.NextDouble() * width;
                if (s.Y > height - 1) s.Y = (float)rng.NextDouble() * height;
            }
            Populate();
        }

        public void Update(float dt, float warp)
        {
            float factor = 0.55f + 0.45f * warp;
            for (int i = 0; i < stars.Count; i++)
            {
                Star s = stars[i];
                float speed = (5f + s.Z * 40f) * factor;
                s.Y += speed * dt;
                s.X += (float)Math.Sin(s.Phase) * s.Z * 2.5f * dt;

                if (s.Y > Height + 2f)
                {
                    stars[i] = MakeStar(false);
                    stars[i].X = (float)rng.NextDouble() * Width;
                }
                else if (s.X < -1f)
                {
                    s.X = Width + 1f;
                }
                else if (s.X > Width + 1f)
                {
                    s.X = -1f;
                }
            }
        }

        public void Draw(Screen s, float warp)
        {
            StreakCells = 0;
            for (int i = 0; i < stars.Count; i++)
            {
                Star st = stars[i];
                int ix = (int)(st.X + 0.5f);
                int iy = (int)(st.Y + 0.5f);

                char c;
                if (st.Z < 0.3f) c = '.';
                else if (st.Z < 0.68f) c = ':';
                else c = '*';

                byte col = st.Color;
                // subtle twinkle on a few stars
                if (Math.Sin(st.Phase + st.Y * 0.07f) < -0.985) col = st.DimColor;

                float streak = (warp - 1f) * 9f * (0.25f + st.Z * 0.9f);
                if (streak >= 1f)
                {
                    int len = (int)streak;
                    if (len > 14) len = 14;
                    for (int k = 1; k <= len; k++)
                    {
                        int yy = iy - k;
                        if (yy < 0) break;
                        char sc;
                        byte scol;
                        if (k <= 2) { sc = '|'; scol = Palette.Cyan; }
                        else if (k <= 6) { sc = ':'; scol = Palette.Blue; }
                        else { sc = '.'; scol = Palette.BlueDark; }
                        s.Set(ix, yy, sc, scol);
                        StreakCells++;
                    }
                }

                s.Set(ix, iy, c, col);
            }
        }
    }

    // Slow procedural gas clouds, drawn as coloured background cells.
    public class Nebula
    {
        public int Width { get; private set; }
        public int Height { get; private set; }
        float time;

        static readonly byte[] BlueShades = { 17, 17, 17, 18, 24 };
        static readonly byte[] PurpleShades = { 17, 17, 52, 52, 53 };
        static readonly byte[] TealShades = { 17, 17, 17, 23, 30 };

        public Nebula(int width, int height)
        {
            Width = width;
            Height = height;
        }

        public void Resize(int width, int height)
        {
            Width = width;
            Height = height;
        }

        public void Update(float dt, float warp)
        {
            time += dt * (0.8f + warp * 0.35f);
        }

        public void Draw(Screen s)
        {
            float scroll = time * 2.0f;
            for (int y = 0; y < Height; y++)
            {
                float ny = (y - scroll) * 0.085f;
                for (int x = 0; x < Width; x++)
                {
                    float nx = x * 0.042f;

                    float v = 0.55f * (float)Math.Sin(nx * 2.3f + time * 0.10f + Math.Sin(ny * 1.7f + time * 0.05f))
                            + 0.30f * (float)Math.Sin(ny * 2.1f - time * 0.07f + nx * 0.9f)
                            + 0.22f * (float)Math.Sin((nx + ny) * 1.7f + time * 0.03f);

                    float n = 0.5f + v * 0.47f;

                    // per-cell hash dithering breaks up flat colour bands
                    uint h = (uint)(x * 73856093) ^ (uint)(y * 19349663);
                    n += ((h & 255) / 255f - 0.5f) * 0.10f;

                    if (n < 0.675f) continue;

                    float f = (n - 0.675f) / 0.325f;
                    int idx = (int)(f * 5f);
                    if (idx > 4) idx = 4;
                    if (idx == 0 && (h & 1) == 1) continue;

                    float hue = (float)Math.Sin(nx * 0.75f + ny * 0.42f + time * 0.02f)
                            + 0.5f * (float)Math.Sin(ny * 0.2f - time * 0.013f);
                    byte shade;
                    if (hue > 0.55f) shade = PurpleShades[idx];
                    else if (hue < -0.75f) shade = TealShades[idx];
                    else shade = BlueShades[idx];

                    s.Direct(x, y, ' ', Palette.White, shade);
                }
            }
        }
    }
}
