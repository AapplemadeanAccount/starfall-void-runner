using System;
using System.Diagnostics;

namespace Starfall
{
    // Headless checks for the game logic, framebuffer, starfield and effects.
    // Run with:  Starfall.exe --selftest
    public static class SelfTest
    {
        static int passed;
        static int failed;

        static void Check(string name, bool ok, string detail = "")
        {
            if (ok)
            {
                passed++;
                Console.WriteLine("  [PASS] " + name);
            }
            else
            {
                failed++;
                Console.WriteLine("  [FAIL] " + name + (detail.Length > 0 ? "  -> " + detail : ""));
            }
        }

        static void Section(string title)
        {
            Console.WriteLine();
            Console.WriteLine("[" + title + "]");
        }

        public static int Run()
        {
            Audio.Muted = true;

            Console.WriteLine("STARFALL // self-test");
            Console.WriteLine("=====================");

            FrameBufferTests();
            StarfieldTests();
            GameplayTests();
            RenderTests();

            Console.WriteLine();
            Console.WriteLine("RESULT: " + passed + " passed, " + failed + " failed");
            return failed == 0 ? 0 : 1;
        }

        static void FrameBufferTests()
        {
            Section("framebuffer / ANSI");

            Screen screen = new Screen(40, 12);
            screen.Clear(0);
            screen.WriteDirect(2, 2, "HELLO", Palette.Cyan);

            string first = screen.RenderDiff();
            Check("first frame paints the whole buffer", first.Length > 500 && first.Contains("\u001b[38;5;"),
                "bytes=" + first.Length);

            string second = screen.RenderDiff();
            Check("unchanged frame is suppressed by the diff renderer", second.Length == 0,
                "bytes=" + second.Length);

            screen.Direct(2, 2, 'J', Palette.Red);
            string third = screen.RenderDiff();
            Check("changed cell emits cursor move and new colour",
                third.Contains("3;3H") && third.Contains("38;5;196"), third);

            Check("plain-text export has the right size", screen.ToPlain().Length == 41 * 12,
                "len=" + screen.ToPlain().Length);

            string html = screen.ToHtml("test");
            Check("HTML capture contains palette colours",
                html.Contains("<span style=\"color:#") && html.Contains("test"), "html bytes=" + html.Length);
        }

        static void StarfieldTests()
        {
            Section("starfield / nebula");

            Starfield a = new Starfield(90, 30, new Random(3));
            Starfield b = new Starfield(90, 30, new Random(3));
            Check("star count scales with the window", a.StarCount >= 60, "stars=" + a.StarCount);

            float[] beforeA = RecordY(a);
            float[] beforeB = RecordY(b);
            a.Update(1f / 60f, 1f);
            b.Update(1f / 60f, 3.4f);

            float moveA = ForwardTravel(beforeA, a);
            float moveB = ForwardTravel(beforeB, b);
            Check("stars scroll down the screen", moveA > 0.01f, "travel=" + moveA.ToString("0.000"));
            Check("warp accelerates the starfield", moveB > moveA * 1.6f,
                "normal=" + moveA.ToString("0.00") + " warp=" + moveB.ToString("0.00"));

            Starfield c = new Starfield(80, 25, new Random(5));
            Screen starScreen = new Screen(80, 25);
            c.Draw(starScreen, 3.4f);
            string warpFrame = starScreen.ToPlain();
            Check("hyperspace streaks are drawn", c.StreakCells > 100 && warpFrame.Contains("|"),
                "streak cells=" + c.StreakCells);

            Nebula nebula = new Nebula(80, 24);
            Screen nebScreen = new Screen(80, 24);
            nebula.Update(2f, 1f);
            nebula.Draw(nebScreen);
            string nebHtml = nebScreen.ToHtml("nebula");
            Check("nebula paints background gas colours",
                nebHtml.Contains("background:#") && nebHtml.Length > 4000,
                "html bytes=" + nebHtml.Length);
        }

        static void GameplayTests()
        {
            Section("game flow");

            Game g = new Game(100, 36, 42);
            g.PersistHighScore = false;
            InputState idle = new InputState();

            Check("boots on the title screen", g.State == GameState.Title);

            InputState start = new InputState();
            start.Start = true;
            g.Update(1f / 60f, start);
            start.ClearEdges();
            Check("ENTER launches a run",
                g.State == GameState.Playing && g.Player.Hull == 100 && g.Wave == 1);

            float x0 = g.Player.X;
            InputState right = new InputState();
            right.Right = true;
            for (int i = 0; i < 45; i++) g.Update(1f / 60f, right);
            Check("the ship responds to steering", g.Player.X > x0 + 1.5f,
                "x=" + g.Player.X.ToString("0.0"));

            for (int i = 0; i < 600; i++) g.Update(1f / 60f, right);
            Check("the ship stays inside the playfield",
                g.Player.X <= g.Width - 4f + 0.01f && g.Player.X >= 3.9f,
                "x=" + g.Player.X.ToString("0.0"));

            // shooting
            g.StartGame();
            InputState fire = new InputState();
            fire.Fire = true;
            for (int i = 0; i < 20; i++) g.Update(1f / 60f, fire);
            int playerBullets = 0;
            for (int i = 0; i < g.Bullets.Count; i++) if (g.Bullets[i].IsPlayer) playerBullets++;
            Check("holding fire shoots upward", playerBullets > 0 && g.ShotsFired > 0,
                "bullets=" + playerBullets + " shots=" + g.ShotsFired);

            // kill scoring
            g.StartGame();
            g.Enemies.Clear();
            g.Bullets.Clear();
            g.Particles.Clear();
            g.Shockwaves.Clear();
            g.Player.Invuln = 999f;
            Enemy rock = g.CreateEnemy(EnemyKind.Asteroid, g.Player.X, g.Player.Y - 9f);
            rock.HP = 1;
            rock.MaxHP = 1;
            rock.VY = 0;
            rock.VX = 0;
            g.Enemies.Add(rock);
            int scoreBefore = g.Score;

            Bullet shot = new Bullet();
            shot.IsPlayer = true;
            shot.X = g.Player.X;
            shot.Y = g.Player.Y - 5f;
            shot.VY = -120f;
            shot.Glyph = '|';
            shot.Color = Palette.White;
            shot.Life = 2f;
            g.Bullets.Add(shot);

            for (int i = 0; i < 10; i++) g.Update(1f / 60f, idle);
            Check("a shot destroys an asteroid", !rock.Alive);
            Check("kills award score and start a chain", g.Score > scoreBefore && g.Chain >= 1,
                "score=" + g.Score + " chain=" + g.Chain);
            Check("kills spawn debris and a shockwave",
                g.Particles.Count > 0 && g.Shockwaves.Count > 0,
                "particles=" + g.Particles.Count);

            // chain multiplier
            Game cg = new Game(100, 36, 8);
            cg.PersistHighScore = false;
            cg.StartGame();
            cg.Enemies.Clear();
            cg.PowerUps.Clear();
            cg.Player.Invuln = 999f;
            for (int i = 0; i < 3; i++)
            {
                Enemy e = cg.CreateEnemy(EnemyKind.Asteroid, 20 + i * 10, 10);
                e.ScoreValue = 100;
                cg.Enemies.Add(e);
                cg.DestroyEnemy(e);
            }
            Check("chain multiplier rewards fast kills", cg.Score == 400 && cg.Chain == 3,
                "score=" + cg.Score + " chain=" + cg.Chain);

            // shield + hull
            g.StartGame();
            g.Enemies.Clear();
            g.Bullets.Clear();
            g.Player.Invuln = 0;
            g.Player.Hull = 100;
            g.Player.Shield = 1;
            AddEnemyBulletAtPlayer(g, 12);
            g.Update(1f / 60f, idle);
            Check("shield absorbs the first hit", g.Player.Hull == 100 && g.Player.Shield == 0,
                "hull=" + g.Player.Hull + " shield=" + g.Player.Shield);

            g.Player.Invuln = 0;
            g.Player.Shield = 0;
            AddEnemyBulletAtPlayer(g, 12);
            g.Update(1f / 60f, idle);
            Check("unshielded hits damage the hull", g.Player.Hull == 88,
                "hull=" + g.Player.Hull);
            Check("damage grants brief invulnerability", g.Player.Invuln > 0.5f);

            // death
            g.Player.Invuln = 0;
            g.Player.Shield = 0;
            g.Player.Hull = 5;
            AddEnemyBulletAtPlayer(g, 12);
            g.Update(1f / 60f, idle);
            Check("hull zero ends the run", g.State == GameState.GameOver && g.Player.Hull == 0,
                "state=" + g.State);

            // powerups
            g.StartGame();
            g.Enemies.Clear();
            g.Bullets.Clear();
            g.Player.WeaponLevel = 1;
            PowerUp pu = new PowerUp();
            pu.X = g.Player.X;
            pu.Y = g.Player.Y;
            pu.Kind = 'T';
            pu.VY = 0;
            g.PowerUps.Add(pu);
            g.Update(1f / 60f, idle);
            Check("T power-up upgrades the gun", g.Player.WeaponLevel == 2 && !pu.Alive,
                "gun=" + g.Player.WeaponLevel);

            // bosses and waves
            g.StartGame();
            g.Enemies.Clear();
            g.PowerUps.Clear();
            g.Wave = 5;
            g.SpawnBoss();
            Check("a Dreadnought spawns on wave 5",
                g.BossActive && g.Boss != null && g.Boss.MaxHP == 120,
                "hp=" + (g.Boss != null ? g.Boss.MaxHP : 0));

            Enemy boss = g.Boss;
            int scoreAtBoss = g.Score;
            g.DestroyEnemy(boss);
            Check("killing the boss pays out and clears the alarm",
                !g.BossActive && g.Score > scoreAtBoss + 2000, "score=" + g.Score);
            Check("the boss drops three power-ups", g.PowerUps.Count >= 3,
                "drops=" + g.PowerUps.Count);

            g.StartGame();
            g.Enemies.Clear();
            g.Bullets.Clear();
            g.Wave = 10;
            g.SpawnBoss();
            g.Boss.Y = g.Boss.TargetY;
            g.Boss.FireTimer = 0;
            g.Boss.RadialTimer = 0;
            g.Update(1f / 60f, idle);
            Check("the Dreadnought fires bullet patterns", CountEnemyBullets(g) >= 5,
                "bullets=" + CountEnemyBullets(g));

            g.StartGame();
            g.Enemies.Clear();
            g.BossActive = false;
            g.Boss = null;
            g.Wave = 4;
            g.WaveTimer = 0.01f;
            g.Update(1f / 60f, idle);
            Check("waves escalate and a boss enters every fifth",
                g.Wave == 5 && g.BossActive, "wave=" + g.Wave);

            // long autopilot run
            Game sim = new Game(100, 36, 99);
            sim.PersistHighScore = false;
            sim.StartGame();
            InputState bot = new InputState();
            int maxParticles = 0;
            int totalKills = 0;
            float maxScore = 0;
            for (int f = 0; f < 1800; f++)
            {
                Bot.Think(sim, bot, f / 60f);
                sim.Update(1f / 60f, bot);
                bot.ClearEdges();
                if (sim.Particles.Count > maxParticles) maxParticles = sim.Particles.Count;
                if (sim.Kills > totalKills) totalKills = sim.Kills;
                if (sim.Score > maxScore) maxScore = sim.Score;
                if (sim.State == GameState.GameOver) sim.StartGame();
            }
            Check("30 s autopilot run completes", true);
            Check("autopilot destroys enemies and scores",
                totalKills > 0 && maxScore > 0, "kills=" + totalKills + " score=" + maxScore);
            Check("particle system stays bounded", maxParticles <= 1800,
                "peak particles=" + maxParticles);
            Check("all entity positions stay finite", AllFinite(sim));

            // deterministic simulation
            Game d1 = new Game(100, 36, 7);
            d1.PersistHighScore = false;
            d1.StartGame();
            Game d2 = new Game(100, 36, 7);
            d2.PersistHighScore = false;
            d2.StartGame();
            InputState b1 = new InputState();
            InputState b2 = new InputState();
            for (int f = 0; f < 900; f++)
            {
                Bot.Think(d1, b1, f / 60f);
                Bot.Think(d2, b2, f / 60f);
                d1.Update(1f / 60f, b1);
                d2.Update(1f / 60f, b2);
                b1.ClearEdges();
                b2.ClearEdges();
            }
            Check("same seed + same input is deterministic",
                d1.Score == d2.Score && d1.Kills == d2.Kills &&
                Math.Abs(d1.Player.X - d2.Player.X) < 0.0001f &&
                d1.Bullets.Count == d2.Bullets.Count,
                "score " + d1.Score + "/" + d2.Score + " kills " + d1.Kills + "/" + d2.Kills);
        }

        static void RenderTests()
        {
            Section("rendering / performance");

            Game g = new Game(110, 40, 123);
            g.PersistHighScore = false;
            g.StartGame();
            InputState idle = new InputState();
            Screen screen = new Screen(110, 40);

            g.Draw(screen);
            string frame = screen.ToPlain();
            Check("frame is fully painted", frame.Length == 111 * 40, "len=" + frame.Length);
            Check("HUD shows score, hull and wave",
                frame.Contains("SCORE") && frame.Contains("HULL") && frame.Contains("WAVE"));

            g.Update(1f / 60f, idle);
            g.Draw(screen);
            string ansi = screen.RenderDiff();
            Check("ANSI diff keeps redraws small", ansi.Length > 0 && ansi.Length < 110 * 40 * 12,
                "bytes=" + ansi.Length);

            // warp visuals
            Game wg = new Game(110, 40, 1);
            wg.PersistHighScore = false;
            wg.StartGame();
            InputState warp = new InputState();
            warp.Warp = true;
            warp.Fire = true;
            for (int i = 0; i < 40; i++) wg.Update(1f / 60f, warp);
            wg.Draw(screen);
            Check("warp drains the meter and creates streaks",
                wg.Player.WarpEnergy < 0.95f && wg.Stars.StreakCells > 80,
                "energy=" + wg.Player.WarpEnergy.ToString("0.00") + " streaks=" + wg.Stars.StreakCells);

            // title screen content
            Game tg = new Game(110, 40, 5);
            tg.PersistHighScore = false;
            InputState idleTitle = new InputState();
            for (int i = 0; i < 30; i++) tg.Update(1f / 60f, idleTitle);
            tg.Draw(screen);
            string title = screen.ToPlain();
            Check("title screen renders the logo and controls",
                title.Contains("V O I D") && title.Contains("PRESS ENTER") && title.Contains("MOVE") &&
                title.Contains("#####"),
                "title bytes=" + title.Length);

            // render cost
            Stopwatch sw = Stopwatch.StartNew();
            const int renders = 300;
            for (int i = 0; i < renders; i++)
            {
                g.Update(1f / 60f, idle);
                g.Draw(screen);
                screen.RenderDiff();
            }
            sw.Stop();
            double ms = sw.Elapsed.TotalMilliseconds / renders;
            Check("software render budget below 25 ms/frame", ms < 25.0,
                ms.ToString("0.00") + " ms/frame");
            Console.WriteLine("         (measured " + ms.ToString("0.00") + " ms per update+draw+diff at 110x40)");
        }

        static void AddEnemyBulletAtPlayer(Game g, int damage)
        {
            Bullet b = new Bullet();
            b.IsPlayer = false;
            b.X = g.Player.X;
            b.Y = g.Player.Y;
            b.VX = 0;
            b.VY = 0;
            b.Glyph = 'v';
            b.Color = Palette.RedBright;
            b.Damage = damage;
            b.Life = 2f;
            g.Bullets.Add(b);
        }

        static int CountEnemyBullets(Game g)
        {
            int n = 0;
            for (int i = 0; i < g.Bullets.Count; i++)
                if (!g.Bullets[i].IsPlayer && g.Bullets[i].Alive) n++;
            return n;
        }

        static float[] RecordY(Starfield sf)
        {
            float[] ys = new float[sf.Items.Count];
            for (int i = 0; i < ys.Length; i++) ys[i] = sf.Items[i].Y;
            return ys;
        }

        static float ForwardTravel(float[] before, Starfield sf)
        {
            float sum = 0;
            for (int i = 0; i < sf.Items.Count && i < before.Length; i++)
            {
                float d = sf.Items[i].Y - before[i];
                if (d > 0) sum += d;
            }
            return sum;
        }

        static bool AllFinite(Game g)
        {
            if (!Finite(g.Player.X) || !Finite(g.Player.Y)) return false;
            for (int i = 0; i < g.Bullets.Count; i++)
                if (!Finite(g.Bullets[i].X) || !Finite(g.Bullets[i].Y)) return false;
            for (int i = 0; i < g.Enemies.Count; i++)
                if (!Finite(g.Enemies[i].X) || !Finite(g.Enemies[i].Y)) return false;
            for (int i = 0; i < g.Particles.Count; i++)
                if (!Finite(g.Particles[i].X) || !Finite(g.Particles[i].Y)) return false;
            return true;
        }

        static bool Finite(float v)
        {
            return !float.IsNaN(v) && !float.IsInfinity(v);
        }
    }
}
