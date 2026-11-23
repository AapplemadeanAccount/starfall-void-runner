using System;
using System.IO;

namespace Starfall
{
    public static class Recorder
    {
        public static int Run(string[] args)
        {
            Audio.Muted = true;

            string dir = args.Length > 1 ? args[1] : "preview";
            int frames = 1500;
            if (args.Length > 2) int.TryParse(args[2], out frames);
            if (frames < 60) frames = 60;

            int W = 110;
            int H = 40;
            if (args.Length > 3) int.TryParse(args[3], out W);
            if (args.Length > 4) int.TryParse(args[4], out H);
            if (W < 40) W = 40;
            if (H < 16) H = 16;

            Directory.CreateDirectory(dir);

            Screen screen = new Screen(W, H);
            Game game = new Game(W, H, 2026);
            game.PersistHighScore = false;
            InputState input = new InputState();
            game.Fps = 60f;

            Console.WriteLine("STARFALL // headless record (" + W + "x" + H + ")");

            // let the starfield / nebula drift a little before the title shot
            for (int i = 0; i < 150; i++)
            {
                game.Update(1f / 60f, input);
                input.ClearEdges();
            }
            Capture(screen, game, dir, "01-title");

            input.Start = true;
            game.Update(1f / 60f, input);
            input.ClearEdges();

            int captured = 1;
            int peakEnemies = 0;
            int peakParticles = 0;
            int peakBullets = 0;
            int deaths = 0;
            int maxWave = 1;
            int maxScore = 0;
            int bossCount = 0;
            bool wasBoss = false;
            bool bossForced = false;

            for (int f = 1; f <= frames; f++)
            {
                Bot.Think(game, input, f / 60f);
                if (f >= 885 && f <= 915) input.Warp = true;   // guarantee a warp capture

                game.Update(1f / 60f, input);
                input.ClearEdges();

                if (game.State == GameState.GameOver)
                {
                    deaths++;
                    game.StartGame();
                }

                if (game.BossActive && !wasBoss) bossCount++;
                wasBoss = game.BossActive;
                if (game.Wave > maxWave) maxWave = game.Wave;
                if (game.Score > maxScore) maxScore = game.Score;

                if (!bossForced && f >= 720)
                {
                    game.Wave = 5;
                    game.SpawnBoss();
                    bossForced = true;
                    game.Player.Invuln = 3f;
                }

                if (game.Enemies.Count > peakEnemies) peakEnemies = game.Enemies.Count;
                if (game.Particles.Count > peakParticles) peakParticles = game.Particles.Count;
                if (game.Bullets.Count > peakBullets) peakBullets = game.Bullets.Count;

                string tag = null;
                if (f == 150) tag = "02-first-contact";
                else if (f == 330) tag = "03-chain-reaction";
                else if (f == 540) tag = "04-deep-field";
                else if (f == 800 && bossForced) tag = "05-dreadnought";
                else if (f == 900) tag = "06-warp-run";

                if (tag != null)
                {
                    Capture(screen, game, dir, tag);
                    captured++;
                }
            }

            // capture the game-over presentation for the docs
            if (game.State == GameState.Playing)
            {
                game.Player.Invuln = 0;
                game.Player.Hull = 1;
                game.DamagePlayer(50, game.Player.X, game.Player.Y);
            }
            for (int i = 0; i < 45; i++)
            {
                game.Update(1f / 60f, input);
                input.ClearEdges();
            }
            if (game.State == GameState.GameOver)
            {
                Capture(screen, game, dir, "07-game-over");
                captured++;
            }

            game.Draw(screen);
            Console.WriteLine("  frames simulated : " + frames + "  (" + (frames / 60f).ToString("0.0") + "s of game time)");
            Console.WriteLine("  score            : " + game.Score);
            Console.WriteLine("  wave             : " + game.Wave);
            Console.WriteLine("  kills            : " + game.Kills);
            Console.WriteLine("  shots fired      : " + game.ShotsFired);
            Console.WriteLine("  peak enemies     : " + peakEnemies);
            Console.WriteLine("  peak bullets     : " + peakBullets);
            Console.WriteLine("  peak particles   : " + peakParticles);
            Console.WriteLine("  deaths / bosses  : " + deaths + " / " + bossCount + "   (peak wave " + maxWave + ", peak score " + maxScore + ")");
            Console.WriteLine("  starfield        : " + game.Stars.StarCount + " stars, " + game.Stars.StreakCells + " warp streak cells");
            Console.WriteLine("  captures         : " + captured);
            Console.WriteLine("  output directory : " + Path.GetFullPath(dir));
            return 0;
        }

        static void Capture(Screen screen, Game game, string dir, string tag)
        {
            game.Draw(screen);
            string plain = screen.ToPlain();
            File.WriteAllText(Path.Combine(dir, tag + ".txt"), plain);
            File.WriteAllText(Path.Combine(dir, tag + ".html"),
                screen.ToHtml("STARFALL // " + tag.Replace('-', ' ')));
            File.WriteAllText(Path.Combine(dir, tag + ".ansi"),
                "\u001b[2J\u001b[H" + screen.RenderDiff());

            Console.WriteLine("  capture " + tag.PadRight(20) +
                " score=" + game.Score +
                " wave=" + game.Wave +
                " enemies=" + game.Enemies.Count +
                " bullets=" + game.Bullets.Count +
                " particles=" + game.Particles.Count);
        }
    }
}
