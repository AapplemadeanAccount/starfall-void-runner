using System;

namespace Starfall
{
    // Lightweight autopilot: chases the nearest target, grabs power-ups and
    // slides away from incoming fire. Used by --record and the long-run test.
    public static class Bot
    {
        public static void Think(Game g, InputState input, float time)
        {
            Player p = g.Player;
            input.Left = false;
            input.Right = false;
            input.Up = false;
            input.Down = false;
            input.Fire = true;
            input.Warp = false;

            float targetX = p.X;
            float best = float.MaxValue;

            for (int i = 0; i < g.Enemies.Count; i++)
            {
                Enemy e = g.Enemies[i];
                if (!e.Alive) continue;
                float dx = Math.Abs(e.X - p.X);
                float dy = Math.Abs(e.Y - p.Y);
                float d = dx + dy * 0.55f;
                if (e.Y > p.Y - 2f) d += 60f;
                if (d < best)
                {
                    best = d;
                    targetX = e.X;
                }
            }

            for (int i = 0; i < g.PowerUps.Count; i++)
            {
                PowerUp pu = g.PowerUps[i];
                if (!pu.Alive) continue;
                if (pu.Y > p.Y - 14f && pu.Y < p.Y + 4f)
                {
                    targetX = pu.X;
                    best = -1f;
                }
            }

            bool dodging = false;
            float dangerX = 0;
            for (int i = 0; i < g.Bullets.Count; i++)
            {
                Bullet b = g.Bullets[i];
                if (b.IsPlayer || !b.Alive) continue;
                if (b.Y < p.Y + 1f && b.Y > p.Y - 14f && Math.Abs(b.X - p.X) < 4.5f)
                {
                    dodging = true;
                    dangerX = b.X;
                }
            }
            if (dodging) targetX = p.X + (dangerX >= p.X ? -9f : 9f);

            float diff = targetX - p.X;
            if (diff < -1.4f) input.Left = true;
            else if (diff > 1.4f) input.Right = true;

            input.Warp = p.WarpEnergy > 0.55f && (time % 9f) < 2.4f;
        }
    }
}
