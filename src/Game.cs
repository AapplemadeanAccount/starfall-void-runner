using System;
using System.Collections.Generic;
using System.IO;

namespace Starfall
{
    public enum GameState
    {
        Title,
        Playing,
        Paused,
        GameOver
    }

    public static class HighScores
    {
        public static string FilePath
        {
            get
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Starfall");
                return Path.Combine(dir, "best.txt");
            }
        }

        public static int Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    int v;
                    if (int.TryParse(File.ReadAllText(FilePath).Trim(), out v)) return Math.Max(0, v);
                }
            }
            catch { }
            return 0;
        }

        public static void Save(int score)
        {
            try
            {
                string dir = Path.GetDirectoryName(FilePath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(FilePath, score.ToString());
            }
            catch { }
        }
    }

    public class Game
    {
        public GameState State = GameState.Title;

        public int Width { get; private set; }
        public int Height { get; private set; }

        public readonly Random Rng;
        public Starfield Stars { get; private set; }
        public Nebula Nebula { get; private set; }
        public Player Player = new Player();

        public readonly List<Bullet> Bullets = new List<Bullet>();
        public readonly List<Enemy> Enemies = new List<Enemy>();
        public readonly List<Particle> Particles = new List<Particle>();
        public readonly List<PowerUp> PowerUps = new List<PowerUp>();
        public readonly List<Shockwave> Shockwaves = new List<Shockwave>();
        public readonly List<FloatText> FloatTexts = new List<FloatText>();

        public int Score;
        public int Best;
        public int Wave = 1;
        public int Kills;
        public int ShotsFired;
        public int Chain;
        public float ChainTimer;

        public float Time;
        public float WaveTimer;
        public float SpawnTimer;
        public float BannerTimer;
        public string BannerText = "";
        public byte BannerColor = Palette.Cyan;
        public bool BannerBig;

        public bool BossActive;
        public Enemy Boss;

        public float ShakeAmount;
        public int ShakeX;
        public int ShakeY;

        public float WarpVisual = 1f;
        public float MuzzleFlash;
        public float GameOverTime;
        public float TitleTime;
        public float Fps;
        public bool PersistHighScore = true;
        public bool NewHighScore;

        public Game(int width, int height, int seed)
        {
            Width = width;
            Height = height;
            Rng = new Random(seed);
            Stars = new Starfield(width, height, Rng);
            Nebula = new Nebula(width, height);
            Best = HighScores.Load();
            ResetToTitle();
        }

        public void Resize(int width, int height)
        {
            if (width == Width && height == Height) return;
            Width = width;
            Height = height;
            Stars.Resize(width, height);
            Nebula.Resize(width, height);
            Player.X = Clamp(Player.X, 3f, Math.Max(3f, width - 4f));
            Player.Y = Clamp(Player.Y, 3f, Height - 4f);
        }

        void ResetToTitle()
        {
            State = GameState.Title;
            Bullets.Clear();
            Enemies.Clear();
            Particles.Clear();
            PowerUps.Clear();
            Shockwaves.Clear();
            FloatTexts.Clear();
            Player.Reset(Width / 2, Height - 6);
            Score = 0;
            Wave = 1;
            Kills = 0;
            ShotsFired = 0;
            Chain = 0;
            ChainTimer = 0;
            BossActive = false;
            Boss = null;
            BannerTimer = 0;
            ShakeAmount = 0;
            ShakeX = 0;
            ShakeY = 0;
            TitleTime = 0;
            NewHighScore = false;
        }

        public void StartGame()
        {
            Bullets.Clear();
            Enemies.Clear();
            Particles.Clear();
            PowerUps.Clear();
            Shockwaves.Clear();
            FloatTexts.Clear();

            Player.Reset(Width / 2, Height - 5);
            Score = 0;
            Wave = 1;
            Kills = 0;
            ShotsFired = 0;
            Chain = 0;
            ChainTimer = 0;
            BossActive = false;
            Boss = null;
            ShakeAmount = 0;
            ShakeX = 0;
            ShakeY = 0;
            WaveTimer = 14f;
            SpawnTimer = 1.0f;
            BannerText = "WAVE 01";
            BannerColor = Palette.Cyan;
            BannerTimer = 2.4f;
            BannerBig = false;
            WarpVisual = 1f;
            MuzzleFlash = 0;
            GameOverTime = 0;
            NewHighScore = false;
            State = GameState.Playing;
            SpawnWaveBurst(3);
            Audio.WaveStart();
        }

        public void SaveBest()
        {
            if (!PersistHighScore) return;
            int best = Math.Max(Best, Score);
            if (best > 0) HighScores.Save(best);
        }

        public void TogglePause()
        {
            if (State == GameState.Playing) State = GameState.Paused;
            else if (State == GameState.Paused) State = GameState.Playing;
        }

        // ---------------------------------------------------------------- update

        public void Update(float dt, InputState input)
        {
            if (dt <= 0) return;
            if (dt > 0.1f) dt = 0.1f;

            Time += dt;
            TitleTime += dt;

            bool warping = State == GameState.Playing && input.Warp && Player.WarpEnergy > 0.01f;
            float warpTarget = warping ? 3.4f : 1f;
            WarpVisual += (warpTarget - WarpVisual) * Math.Min(1f, dt * 5f);

            Nebula.Update(dt, WarpVisual);
            Stars.Update(dt, WarpVisual);
            UpdateEffects(dt);

            if (MuzzleFlash > 0) MuzzleFlash -= dt;

            switch (State)
            {
                case GameState.Title:
                    Player.EnginePhase += dt;
                    if (input.Start) StartGame();
                    break;

                case GameState.Playing:
                    if (input.Pause) { State = GameState.Paused; break; }
                    UpdatePlaying(dt, input);
                    break;

                case GameState.Paused:
                    if (input.Pause) State = GameState.Playing;
                    break;

                case GameState.GameOver:
                    GameOverTime += dt;
                    if (input.Start || input.Restart) StartGame();
                    break;
            }
        }

        void UpdatePlaying(float dt, InputState input)
        {
            if (BannerTimer > 0) BannerTimer -= dt;
            UpdateWave(dt);
            UpdatePlayer(dt, input);
            UpdateSpawner(dt);
            UpdateBullets(dt);
            UpdateEnemies(dt);
            UpdatePowerUps(dt);
            Collide();
            Cleanup();
            UpdateChain(dt);
        }

        void UpdateWave(float dt)
        {
            if (BossActive) return;
            WaveTimer -= dt;
            if (WaveTimer > 0) return;

            Wave++;
            WaveTimer = 17f + Math.Min(8f, Wave * 0.35f);
            BannerText = "WAVE " + Wave.ToString("00");
            BannerColor = Palette.Cyan;
            BannerTimer = 2.2f;
            BannerBig = false;
            Audio.WaveStart();

            if (Wave % 5 == 0) SpawnBoss();
            else SpawnWaveBurst(2 + Math.Min(4, Wave / 2));
        }

        void SpawnWaveBurst(int count)
        {
            for (int i = 0; i < count; i++)
            {
                float x = 7f + (float)Rng.NextDouble() * Math.Max(1f, Width - 14f);
                Enemies.Add(CreateEnemy(EnemyKind.Asteroid, x, -2.5f - i * 3.5f));
            }
        }

        void UpdatePlayer(float dt, InputState input)
        {
            Player p = Player;

            float mx = (input.Right ? 1f : 0f) - (input.Left ? 1f : 0f);
            float my = (input.Down ? 1f : 0f) - (input.Up ? 1f : 0f);
            bool boosting = input.Warp && p.WarpEnergy > 0.01f;
            float speed = boosting ? 78f : 60f;

            p.VX += (mx * speed - p.VX) * Math.Min(1f, dt * 13f);
            p.VY += (my * speed * 0.8f - p.VY) * Math.Min(1f, dt * 13f);
            p.X += p.VX * dt;
            p.Y += p.VY * dt;

            float minX = 3.5f;
            float maxX = Math.Max(minX, Width - 4.5f);
            float minY = Height * 0.28f;
            float maxY = Math.Max(minY, Height - 4.5f);
            p.X = Clamp(p.X, minX, maxX);
            p.Y = Clamp(p.Y, minY, maxY);
            if (p.X == minX || p.X == maxX) p.VX = 0;
            if (p.Y == minY || p.Y == maxY) p.VY = 0;

            if (p.Invuln > 0) p.Invuln -= dt;
            if (p.ShieldFlash > 0) p.ShieldFlash -= dt;
            if (p.HitFlash > 0) p.HitFlash -= dt;
            p.EnginePhase += dt * (boosting ? 2.6f : 1.4f);

            if (boosting)
            {
                p.WarpEnergy -= dt * 0.22f;
                if (p.WarpEnergy < 0) p.WarpEnergy = 0;
            }
            else
            {
                p.WarpEnergy += dt * 0.11f;
                if (p.WarpEnergy > 1f) p.WarpEnergy = 1f;
            }

            if (p.FireCooldown > 0) p.FireCooldown -= dt;
            if (input.Fire && p.FireCooldown <= 0) Shoot();

            if (p.FireCooldown < -1f) p.FireCooldown = 0;
        }

        void Shoot()
        {
            Player p = Player;
            float noseY = p.Y - 2f;
            float rate = 0.145f - (p.WeaponLevel - 1) * 0.006f;
            int lvl = p.WeaponLevel;
            if (lvl <= 1)
            {
                AddPlayerBullet(p.X, noseY, 0, -118, Palette.CyanBright);
            }
            else if (lvl == 2)
            {
                AddPlayerBullet(p.X - 1.5f, noseY, 0, -118, Palette.CyanBright);
                AddPlayerBullet(p.X + 1.5f, noseY, 0, -118, Palette.CyanBright);
            }
            else if (lvl == 3)
            {
                AddPlayerBullet(p.X, noseY - 0.5f, 0, -124, Palette.White);
                AddPlayerBullet(p.X - 1.8f, noseY, 0, -116, Palette.CyanBright);
                AddPlayerBullet(p.X + 1.8f, noseY, 0, -116, Palette.CyanBright);
            }
            else
            {
                AddPlayerBullet(p.X, noseY - 0.5f, 0, -126, Palette.White);
                AddPlayerBullet(p.X - 1.8f, noseY, 0, -118, Palette.CyanBright);
                AddPlayerBullet(p.X + 1.8f, noseY, 0, -118, Palette.CyanBright);
                AddPlayerBullet(p.X - 2.6f, noseY + 0.5f, -34, -108, Palette.Cyan);
                AddPlayerBullet(p.X + 2.6f, noseY + 0.5f, 34, -108, Palette.Cyan);
            }

            p.FireCooldown = rate;
            MuzzleFlash = 0.07f;
            ShotsFired++;
            SpawnSparks(p.X, noseY, 2, Palette.Ice, 18f);
            Audio.Shoot();
            if (Bullets.Count > 160) Bullets.RemoveRange(0, Bullets.Count - 160);
        }

        void AddPlayerBullet(float x, float y, float vx, float vy, byte color)
        {
            Bullet b = new Bullet();
            b.X = x;
            b.Y = y;
            b.VX = vx;
            b.VY = vy;
            b.IsPlayer = true;
            b.Glyph = '|';
            b.Color = color;
            b.Damage = 1;
            b.Life = 3f;
            Bullets.Add(b);
        }

        void UpdateSpawner(float dt)
        {
            if (BossActive) return;

            SpawnTimer -= dt;
            if (SpawnTimer > 0) return;

            SpawnRandomEnemy();
            if (Rng.NextDouble() < 0.35) SpawnRandomEnemy();
            float baseInterval = Math.Max(0.26f, 0.90f - Wave * 0.045f);
            SpawnTimer = baseInterval * (0.55f + (float)Rng.NextDouble() * 0.9f);
        }

        void SpawnRandomEnemy()
        {
            if (Enemies.Count >= 30) return;
            float x = 5f + (float)Rng.NextDouble() * Math.Max(1f, Width - 10f);
            double r = Rng.NextDouble();
            double hunterChance = Wave >= 2 ? Math.Min(0.28, 0.08 + Wave * 0.018) : 0.0;
            double droneChance = 0.40;

            EnemyKind kind;
            if (r < hunterChance) kind = EnemyKind.Hunter;
            else if (r < hunterChance + droneChance) kind = EnemyKind.Drone;
            else kind = EnemyKind.Asteroid;

            Enemies.Add(CreateEnemy(kind, x, -3f));
        }

        public Enemy CreateEnemy(EnemyKind kind, float x, float y)
        {
            Enemy e = new Enemy();
            e.Kind = kind;
            e.X = x;
            e.Y = y;
            e.Age = 0;
            e.Phase = (float)(Rng.NextDouble() * Math.PI * 2);

            switch (kind)
            {
                case EnemyKind.Asteroid:
                {
                    int variant = Rng.Next(Enemy.Asteroids.Length);
                    e.Sprite = Enemy.Asteroids[variant];
                    e.W = 7;
                    e.H = 3;
                    e.HP = 2 + (Wave >= 7 ? 1 : 0) + (Rng.NextDouble() < 0.35 ? 1 : 0);
                    e.Color = new byte[] { 244, 137, 180, 101, 248 }[Rng.Next(5)];
                    e.VY = 16f + Wave * 1.5f + (float)Rng.NextDouble() * 9f;
                    e.VX = ((float)Rng.NextDouble() - 0.5f) * 26f;
                    e.ScoreValue = 50;
                    e.DropChance = 0.10f;
                    break;
                }
                case EnemyKind.Drone:
                {
                    e.Sprite = Enemy.DroneSprite;
                    e.W = 5;
                    e.H = 3;
                    e.HP = 2 + Wave / 5;
                    e.Color = Palette.RedBright;
                    e.VY = 24f + Wave * 0.9f;
                    e.BaseX = x;
                    e.TargetY = Height * 0.10f + (float)Rng.NextDouble() * Height * 0.22f;
                    e.FireTimer = 0.9f + (float)Rng.NextDouble();
                    e.ScoreValue = 100;
                    e.DropChance = 0.14f;
                    break;
                }
                case EnemyKind.Hunter:
                {
                    e.Sprite = Enemy.HunterSprite;
                    e.W = 5;
                    e.H = 3;
                    e.HP = 1 + Wave / 7;
                    e.Color = Palette.Magenta;
                    e.VY = 38f + Wave * 1.2f;
                    e.VX = ((float)Rng.NextDouble() - 0.5f) * 30f;
                    e.FireTimer = 1.1f + (float)Rng.NextDouble() * 1.2f;
                    e.ScoreValue = 150;
                    e.DropChance = 0.16f;
                    break;
                }
                default:
                    e.Sprite = Enemy.BossSprite;
                    e.W = 15;
                    e.H = 5;
                    break;
            }

            e.MaxHP = e.HP;
            e.W = e.Sprite[0].Length;
            e.H = e.Sprite.Length;
            return e;
        }

        public void SpawnBoss()
        {
            Enemy b = CreateEnemy(EnemyKind.Boss, Width / 2f, -7f);
            b.HP = 60 + Wave * 12;
            b.MaxHP = b.HP;
            b.Color = Palette.Purple;
            b.Sprite = Enemy.BossSprite;
            b.TargetY = Math.Min(11.5f, Math.Max(7.0f, Height * 0.30f));
            b.ScoreValue = 2500 + Wave * 250;
            b.FireTimer = 2.2f;
            b.RadialTimer = 4.0f;
            Enemies.Add(b);
            Boss = b;
            BossActive = true;

            Enemy droneA = CreateEnemy(EnemyKind.Drone, Width * 0.2f, -3f);
            Enemies.Add(droneA);
            Enemy droneB = CreateEnemy(EnemyKind.Drone, Width * 0.8f, -6f);
            Enemies.Add(droneB);

            BannerText = "DREADNOUGHT";
            BannerColor = Palette.Magenta;
            BannerTimer = 3.2f;
            BannerBig = true;
            ShakeAmount = Math.Max(ShakeAmount, 5f);
            Audio.BossAlarm();
        }

        void UpdateBullets(float dt)
        {
            for (int i = 0; i < Bullets.Count; i++)
            {
                Bullet b = Bullets[i];
                if (!b.Alive) continue;
                b.X += b.VX * dt;
                b.Y += b.VY * dt;
                b.Life -= dt;
                if (b.Life <= 0 || b.Y < -3 || b.Y > Height + 3 || b.X < -3 || b.X > Width + 3)
                    b.Alive = false;
            }
        }

        void UpdateEnemies(float dt)
        {
            for (int i = 0; i < Enemies.Count; i++)
            {
                Enemy e = Enemies[i];
                if (!e.Alive) continue;
                e.Age += dt;
                if (e.HitFlash > 0) e.HitFlash -= dt;

                switch (e.Kind)
                {
                    case EnemyKind.Asteroid:
                        e.X += (e.VX + (float)Math.Sin(e.Age * 1.3f + e.Phase) * 9f) * dt;
                        e.Y += e.VY * dt;
                        if (e.Y > Height + 5 || e.X < -9 || e.X > Width + 9) e.Alive = false;
                        break;

                    case EnemyKind.Drone:
                        if (e.Y < e.TargetY)
                        {
                            e.Y += e.VY * dt;
                        }
                        else
                        {
                            e.Y += 5f * dt;
                            e.X = e.BaseX + (float)Math.Sin(e.Age * 1.2f + e.Phase) * 7f;
                        }
                        if (e.Y > 1f && e.Y < Height * 0.8f)
                        {
                            e.FireTimer -= dt;
                            if (e.FireTimer <= 0)
                            {
                                FireAimed(e.X, e.Y + 2f, 52f, Palette.RedBright, 'v', 12);
                                e.FireTimer = Math.Max(0.85f, 2.0f - Wave * 0.04f) + (float)Rng.NextDouble() * 0.8f;
                            }
                        }
                        if (e.Y > Height + 5) e.Alive = false;
                        break;

                    case EnemyKind.Hunter:
                        float dir = Math.Sign(Player.X - e.X);
                        e.VX = Clamp(e.VX + dir * 150f * dt, -62f, 62f);
                        e.X += e.VX * dt;
                        e.Y += e.VY * dt;
                        e.FireTimer -= dt;
                        if (e.FireTimer <= 0 && e.Y > 1f && e.Y < Height * 0.72f)
                        {
                            FireAimed(e.X, e.Y + 2f, 70f, Palette.Magenta, '!', 14);
                            e.FireTimer = 2.2f + (float)Rng.NextDouble() * 1.0f;
                        }
                        if (e.Y > Height + 5 || e.X < -9 || e.X > Width + 9) e.Alive = false;
                        break;

                    case EnemyKind.Boss:
                        UpdateBoss(e, dt);
                        break;
                }
            }
        }

        void UpdateBoss(Enemy e, float dt)
        {
            if (e.Y < e.TargetY)
            {
                e.Y += 13f * dt;
                return;
            }

            e.X = Width / 2f + (float)Math.Sin(e.Age * 0.55f) * (Width * 0.28f);
            e.X = Clamp(e.X, 10f, Math.Max(10f, Width - 10f));
            e.Y = e.TargetY + (float)Math.Sin(e.Age * 0.9f) * 1.1f;

            bool enraged = e.HP < e.MaxHP * 0.4f;
            e.FireTimer -= dt;
            e.RadialTimer -= dt;

            if (e.FireTimer <= 0)
            {
                FireBossSpread(e, enraged);
                e.FireTimer = enraged ? 1.05f : 1.65f;
            }
            if (e.RadialTimer <= 0)
            {
                FireBossRadial(e, enraged);
                e.RadialTimer = enraged ? 2.7f : 4.4f;
            }
        }

        void FireBossSpread(Enemy e, bool enraged)
        {
            float baseAngle = (float)Math.Atan2(Player.Y - e.Y, Player.X - e.X);
            int n = enraged ? 5 : 3;
            float spread = enraged ? 0.30f : 0.19f;
            for (int i = 0; i < n; i++)
            {
                float a = baseAngle + (i - (n - 1) / 2f) * spread;
                AddEnemyBullet(e.X, e.Y + 2f, (float)Math.Cos(a) * 52f, (float)Math.Sin(a) * 52f,
                    Palette.Magenta, 'o', 12);
            }
            Audio.EnemyShoot();
        }

        void FireBossRadial(Enemy e, bool enraged)
        {
            int n = enraged ? 16 : 10;
            for (int i = 0; i < n; i++)
            {
                float a = (float)(Math.PI * 2.0 * i / n) + e.Age * 0.7f;
                AddEnemyBullet(e.X, e.Y + 1f, (float)Math.Cos(a) * 34f, (float)Math.Sin(a) * 34f,
                    Palette.Rose, 'o', 10);
            }
            Audio.EnemyShoot();
        }

        void FireAimed(float x, float y, float speed, byte color, char glyph, int damage)
        {
            float dx = Player.X - x;
            float dy = Player.Y - y;
            float len = (float)Math.Sqrt(dx * dx + dy * dy);
            if (len < 0.001f) len = 1f;
            AddEnemyBullet(x, y, dx / len * speed, dy / len * speed, color, glyph, damage);
            Audio.EnemyShoot();
        }

        void AddEnemyBullet(float x, float y, float vx, float vy, byte color, char glyph, int damage)
        {
            if (Bullets.Count > 400) return;
            Bullet b = new Bullet();
            b.X = x;
            b.Y = y;
            b.VX = vx;
            b.VY = vy;
            b.IsPlayer = false;
            b.Glyph = glyph;
            b.Color = color;
            b.Damage = damage;
            b.Life = 6f;
            Bullets.Add(b);
        }

        void UpdatePowerUps(float dt)
        {
            for (int i = 0; i < PowerUps.Count; i++)
            {
                PowerUp p = PowerUps[i];
                if (!p.Alive) continue;
                p.Age += dt;
                p.Y += p.VY * dt;
                p.X += (float)Math.Sin(p.Age * 3f + p.Phase) * 9f * dt;
                if (p.Y > Height + 2) p.Alive = false;

                if (Math.Abs(p.X - Player.X) < 3.2f && Math.Abs(p.Y - Player.Y) < 3.2f)
                {
                    CollectPowerUp(p);
                }
            }
        }

        void CollectPowerUp(PowerUp p)
        {
            p.Alive = false;
            AddScore(25);
            Audio.PowerUp();

            switch (p.Kind)
            {
                case 'T':
                    if (Player.WeaponLevel < 4)
                    {
                        Player.WeaponLevel++;
                        AddFloatText("GUN UP", Player.X, Player.Y - 3, Palette.Gold, 1.1f);
                    }
                    else
                    {
                        AddScore(250);
                        AddFloatText("GUN MAX", Player.X, Player.Y - 3, Palette.Gold, 1.1f);
                    }
                    break;
                case 'S':
                    Player.Shield = Math.Min(3, Player.Shield + 2);
                    Player.ShieldFlash = 0.5f;
                    AddFloatText("SHIELD +2", Player.X, Player.Y - 3, Palette.CyanBright, 1.1f);
                    break;
                case 'H':
                    Player.Hull = Math.Min(Player.MaxHull, Player.Hull + 30);
                    AddFloatText("HULL +30", Player.X, Player.Y - 3, Palette.Green, 1.1f);
                    break;
                case 'W':
                    Player.WarpEnergy = 1f;
                    AddScore(100);
                    AddFloatText("WARP FULL", Player.X, Player.Y - 3, Palette.Blue, 1.1f);
                    break;
            }

            SpawnSparks(p.X, p.Y, 16, Palette.Toxic);
        }

        void UpdateChain(float dt)
        {
            if (ChainTimer <= 0) return;
            ChainTimer -= dt;
            if (ChainTimer <= 0)
            {
                Chain = 0;
                ChainTimer = 0;
            }
        }

        void Collide()
        {
            Player p = Player;

            for (int bi = 0; bi < Bullets.Count; bi++)
            {
                Bullet b = Bullets[bi];
                if (!b.Alive) continue;

                if (b.IsPlayer)
                {
                    for (int ei = 0; ei < Enemies.Count; ei++)
                    {
                        Enemy e = Enemies[ei];
                        if (!e.Alive) continue;
                        if (PointInEnemy(e, b.X, b.Y, 0.7f))
                        {
                            b.Alive = false;
                            e.HP -= b.Damage;
                            e.HitFlash = 0.09f;
                            SpawnSparks(b.X, b.Y, 3, Palette.Ice, 26f);
                            if (e.HP <= 0)
                            {
                                DestroyEnemy(e);
                            }
                            else
                            {
                                Audio.Hit();
                                if (e.Kind == EnemyKind.Boss) AddScore(5);
                            }
                            break;
                        }
                    }
                }
                else if (p.Invuln <= 0 &&
                         Math.Abs(b.X - p.X) < 2.0f && Math.Abs(b.Y - p.Y) < 1.8f)
                {
                    b.Alive = false;
                    SpawnSparks(b.X, b.Y, 10, Palette.Blood, 34f);
                    DamagePlayer(b.Damage, b.X, b.Y);
                    if (State != GameState.Playing) return;
                }
            }

            for (int ei = 0; ei < Enemies.Count; ei++)
            {
                Enemy e = Enemies[ei];
                if (!e.Alive) continue;

                if (e.Kind == EnemyKind.Boss)
                {
                    if (Math.Abs(e.X - p.X) < e.W / 2f + 1f && Math.Abs(e.Y - p.Y) < e.H / 2f + 1f)
                    {
                        DamagePlayer(30, e.X, e.Y);
                        p.Y = Math.Min(Height - 4f, e.Y + e.H / 2f + 4f);
                        if (State != GameState.Playing) return;
                    }
                    continue;
                }

                if (p.Invuln <= 0 &&
                    Math.Abs(e.X - p.X) < e.W / 2f + 1f &&
                    Math.Abs(e.Y - p.Y) < e.H / 2f + 1f)
                {
                    DestroyEnemy(e);
                    DamagePlayer(25, e.X, e.Y);
                    if (State != GameState.Playing) return;
                }
            }

            for (int i = 0; i < PowerUps.Count; i++)
            {
                PowerUp pu = PowerUps[i];
                if (!pu.Alive) continue;
                if (Math.Abs(pu.X - p.X) < 3.2f && Math.Abs(pu.Y - p.Y) < 3.2f)
                    CollectPowerUp(pu);
            }
        }

        bool PointInEnemy(Enemy e, float x, float y, float pad)
        {
            return Math.Abs(x - e.X) <= e.W / 2f + pad && Math.Abs(y - e.Y) <= e.H / 2f + pad;
        }

        public void DamagePlayer(int amount, float srcX, float srcY)
        {
            if (State != GameState.Playing) return;
            if (Player.Invuln > 0) return;

            if (Player.Shield > 0)
            {
                Player.Shield--;
                Player.ShieldFlash = 0.45f;
                Player.Invuln = 0.22f;
                ShakeAmount = Math.Max(ShakeAmount, 5f);
                SpawnSparks(Player.X, Player.Y, 18, Palette.Ice, 46f);
                AddFloatText("SHIELD", Player.X, Player.Y - 3, Palette.CyanBright, 0.8f);
                Audio.ShieldHit();
                return;
            }

            Player.Hull -= amount;
            Player.HitFlash = 0.6f;
            Player.Invuln = 1.4f;
            ShakeAmount = Math.Max(ShakeAmount, 9f);
            SpawnSparks(Player.X, Player.Y, 22, Palette.Fire, 52f);
            AddFloatText("-" + amount, Player.X, Player.Y - 3, Palette.RedBright, 0.9f);
            Audio.PlayerHit();

            if (Player.Hull <= 0)
            {
                Player.Hull = 0;
                Die();
            }
        }

        void Die()
        {
            State = GameState.GameOver;
            GameOverTime = 0;
            Chain = 0;
            ChainTimer = 0;

            for (int i = 0; i < 140; i++)
            {
                float a = (float)(Rng.NextDouble() * Math.PI * 2);
                float sp = 10f + (float)Rng.NextDouble() * 60f;
                AddParticle(Player.X, Player.Y,
                    (float)Math.Cos(a) * sp, (float)Math.Sin(a) * sp,
                    0.5f + (float)Rng.NextDouble() * 1.1f,
                    i % 5 == 0 ? '*' : (i % 3 == 0 ? '+' : '.'),
                    i % 4 == 0 ? Palette.Ice : Palette.Fire, 0.7f, 0f);
            }
            AddShockwave(Player.X, Player.Y, Palette.CyanBright, 18f, 1.1f);
            AddShockwave(Player.X, Player.Y, Palette.Orange, 28f, 1.4f);
            ShakeAmount = 16f;
            Audio.GameOver();

            if (Score > Best)
            {
                Best = Score;
                NewHighScore = true;
                if (PersistHighScore) HighScores.Save(Best);
            }
        }

        public void DestroyEnemy(Enemy e)
        {
            if (!e.Alive) return;
            e.Alive = false;

            int baseScore = e.ScoreValue;
            if (e.Kind == EnemyKind.Boss)
            {
                baseScore += 1500;
                BossActive = false;
                Boss = null;
                WaveTimer = 3.2f;
                ShakeAmount = 26f;
                BannerText = "DREADNOUGHT DOWN";
                BannerColor = Palette.Yellow;
                BannerTimer = 3.0f;
                BannerBig = false;
                for (int i = 0; i < 170; i++)
                {
                    float a = (float)(Rng.NextDouble() * Math.PI * 2);
                    float sp = 15f + (float)Rng.NextDouble() * 85f;
                    AddParticle(e.X + (float)(Rng.NextDouble() - 0.5) * 12f,
                        e.Y + (float)(Rng.NextDouble() - 0.5) * 5f,
                        (float)Math.Cos(a) * sp, (float)Math.Sin(a) * sp,
                        0.7f + (float)Rng.NextDouble() * 1.4f,
                        i % 4 == 0 ? '*' : (i % 2 == 0 ? '+' : '.'),
                        i % 3 == 0 ? Palette.Ice : Palette.Fire, 0.8f, 0f);
                }
                AddShockwave(e.X, e.Y, Palette.White, 40f, 1.6f);
                AddShockwave(e.X, e.Y, Palette.Magenta, 60f, 2.0f);
                AddShockwave(e.X, e.Y, Palette.Orange, 80f, 2.4f);

                // clear incoming fire as a reward
                for (int i = 0; i < Bullets.Count; i++)
                {
                    if (!Bullets[i].IsPlayer && Bullets[i].Alive)
                    {
                        Bullets[i].Alive = false;
                        SpawnSparks(Bullets[i].X, Bullets[i].Y, 2, Palette.PurpleRamp, 30f);
                    }
                }

                SpawnPowerUp(e.X - 4f, e.Y, 'T');
                SpawnPowerUp(e.X, e.Y, 'S');
                SpawnPowerUp(e.X + 4f, e.Y, 'H');
                Audio.ExplodeBig();
            }
            else
            {
                int count = e.Kind == EnemyKind.Asteroid ? 28 : 34;
                byte[] ramp = e.Kind == EnemyKind.Hunter ? Palette.PurpleRamp : Palette.Fire;
                for (int i = 0; i < count; i++)
                {
                    float a = (float)(Rng.NextDouble() * Math.PI * 2);
                    float sp = 6f + (float)Rng.NextDouble() * 48f;
                    AddParticle(e.X + (float)(Rng.NextDouble() - 0.5) * e.W * 0.7f,
                        e.Y + (float)(Rng.NextDouble() - 0.5) * e.H,
                        (float)Math.Cos(a) * sp, (float)Math.Sin(a) * sp,
                        0.35f + (float)Rng.NextDouble() * 0.7f,
                        i % 3 == 0 ? '*' : (i % 2 == 0 ? '+' : '.'),
                        ramp, 0.9f, 0f);
                }
                AddShockwave(e.X, e.Y, e.Kind == EnemyKind.Hunter ? Palette.Magenta : Palette.Orange,
                    12f + e.W, 0.55f);
                ShakeAmount = Math.Max(ShakeAmount, e.Kind == EnemyKind.Asteroid ? 3f : 4.5f);
                Audio.ExplodeSmall();

                if ((float)Rng.NextDouble() < e.DropChance) SpawnPowerUp(e.X, e.Y, RandomPowerUpKind());
            }

            AddKillScore(baseScore, e.X, e.Y);
            Kills++;
        }

        char RandomPowerUpKind()
        {
            double r = Rng.NextDouble();
            if (r < 0.36) return 'T';
            if (r < 0.62) return 'S';
            if (r < 0.90) return 'H';
            return 'W';
        }

        void SpawnPowerUp(float x, float y, char kind)
        {
            PowerUp p = new PowerUp();
            p.X = Math.Max(2f, Math.Min(Width - 2f, x));
            p.Y = y;
            p.VY = 20f;
            p.Kind = kind;
            p.Phase = (float)(Rng.NextDouble() * Math.PI * 2);
            PowerUps.Add(p);
        }

        void AddKillScore(int baseScore, float x, float y)
        {
            Chain++;
            ChainTimer = 2.8f;
            int mult = 1 + Math.Min(5, Chain / 3);
            int gained = baseScore * mult;
            Score += gained;

            AddFloatText("+" + gained, x, y - 1, Palette.Gold, 0.9f);
            if (mult > 1)
                AddFloatText("x" + mult, x, y + 1, Palette.Orange, 0.9f);
        }

        public void AddScore(int amount)
        {
            Score += amount;
        }

        public void AddParticle(float x, float y, float vx, float vy, float life,
            char glyph, byte[] ramp, float drag, float gravity)
        {
            if (Particles.Count > 1800) return;
            Particle p = new Particle();
            p.X = x;
            p.Y = y;
            p.VX = vx;
            p.VY = vy;
            p.Life = life;
            p.MaxLife = life;
            p.Glyph = glyph;
            p.Ramp = ramp;
            p.Drag = drag;
            p.Gravity = gravity;
            Particles.Add(p);
        }

        public void SpawnSparks(float x, float y, int count, byte[] ramp)
        {
            SpawnSparks(x, y, count, ramp, 40f);
        }

        public void SpawnSparks(float x, float y, int count, byte[] ramp, float speed)
        {
            for (int i = 0; i < count; i++)
            {
                float a = (float)(Rng.NextDouble() * Math.PI * 2);
                float sp = speed * (0.25f + (float)Rng.NextDouble());
                AddParticle(x, y, (float)Math.Cos(a) * sp, (float)Math.Sin(a) * sp,
                    0.18f + (float)Rng.NextDouble() * 0.35f,
                    i % 3 == 0 ? '+' : '.', ramp, 1.6f, 0f);
            }
        }

        public void AddShockwave(float x, float y, byte color, float maxRadius, float life)
        {
            Shockwave w = new Shockwave();
            w.X = x;
            w.Y = y;
            w.Color = color;
            w.Radius = 1f;
            w.Speed = maxRadius / life;
            w.Life = life;
            w.MaxLife = life;
            Shockwaves.Add(w);
        }

        public void AddFloatText(string text, float x, float y, byte color, float life)
        {
            FloatText f = new FloatText();
            f.Text = text;
            f.X = x;
            f.Y = y;
            f.VY = -11f;
            f.Life = life;
            f.MaxLife = life;
            f.Color = color;
            FloatTexts.Add(f);
        }

        void UpdateEffects(float dt)
        {
            for (int i = Particles.Count - 1; i >= 0; i--)
            {
                Particle p = Particles[i];
                p.Life -= dt;
                if (p.Life <= 0 || p.X < -6 || p.X > Width + 6 || p.Y < -6 || p.Y > Height + 6)
                {
                    Particles.RemoveAt(i);
                    continue;
                }
                p.VY += p.Gravity * dt;
                p.VX *= 1f - Math.Min(1f, p.Drag * dt);
                p.VY *= 1f - Math.Min(1f, p.Drag * dt);
                p.X += p.VX * dt;
                p.Y += p.VY * dt;
            }

            for (int i = Shockwaves.Count - 1; i >= 0; i--)
            {
                Shockwave w = Shockwaves[i];
                w.Life -= dt;
                w.Radius += w.Speed * dt;
                if (w.Life <= 0) Shockwaves.RemoveAt(i);
            }

            for (int i = FloatTexts.Count - 1; i >= 0; i--)
            {
                FloatText f = FloatTexts[i];
                f.Life -= dt;
                f.Y += f.VY * dt;
                if (f.Life <= 0) FloatTexts.RemoveAt(i);
            }

            if (ShakeAmount > 0)
            {
                ShakeAmount -= dt * 32f;
                if (ShakeAmount < 0.4f) ShakeAmount = 0;
                int amp = (int)ShakeAmount;
                ShakeX = amp > 0 ? Rng.Next(-amp, amp + 1) : 0;
                ShakeY = amp > 0 ? Rng.Next(-Math.Max(1, amp / 2), Math.Max(1, amp / 2) + 1) : 0;
            }
            else
            {
                ShakeX = 0;
                ShakeY = 0;
            }
        }

        void Cleanup()
        {
            Bullets.RemoveAll(delegate(Bullet b) { return !b.Alive; });
            Enemies.RemoveAll(delegate(Enemy e) { return !e.Alive; });
            PowerUps.RemoveAll(delegate(PowerUp p) { return !p.Alive; });
        }

        // ------------------------------------------------------------------ draw

        public void Draw(Screen s)
        {
            s.Clear(0);
            Nebula.Draw(s);
            Stars.Draw(s, WarpVisual);

            s.ShakeX = ShakeX;
            s.ShakeY = ShakeY;
            DrawPowerUps(s);
            DrawBullets(s);
            DrawEnemies(s);
            DrawParticles(s);
            DrawShockwaves(s);
            DrawFloatTexts(s);
            if (State != GameState.Title && !(State == GameState.GameOver && Player.Hull <= 0))
                DrawPlayer(s);
            s.ShakeX = 0;
            s.ShakeY = 0;

            if (State == GameState.Title)
            {
                DrawTitle(s);
                return;
            }

            DrawHud(s);
            DrawBossBar(s);
            DrawBanner(s);
            DrawBottomHints(s);

            if (State == GameState.Paused) DrawPause(s);
            else if (State == GameState.GameOver) DrawGameOver(s);
        }

        void DrawPlayer(Screen s)
        {
            Player p = Player;
            int cx = (int)(p.X + 0.5f);
            int cy = (int)(p.Y + 0.5f);
            int top = cy - 2;
            int left = cx - 3;

            bool blink = p.Invuln > 0 && ((int)(Time * 18f) % 2 == 0) && State == GameState.Playing;
            bool flash = p.HitFlash > 0.3f;

            string[] sprite = Player.Sprite;
            for (int r = 0; r < sprite.Length; r++)
            {
                string row = sprite[r];
                for (int c = 0; c < row.Length; c++)
                {
                    char ch = row[c];
                    if (ch == ' ') continue;
                    byte col;
                    if (ch == '^') col = Palette.White;
                    else if (ch == '=') col = Palette.Yellow;
                    else if (ch == '#') col = Palette.CyanBright;
                    else col = Palette.Cyan;

                    if (blink) col = Palette.DimGray;
                    if (flash) col = Palette.White;
                    s.Set(left + c, top + r, ch, col);
                }
            }

            if (State != GameState.Paused)
            {
                float f = (float)(Math.Sin(p.EnginePhase * 18f) * 0.5 + 0.5);
                int len = 1 + (int)(f * 3f);
                char flameChar = f > 0.55f ? 'v' : '.';
                for (int k = 0; k < len; k++)
                {
                    byte col = k == 0 ? Palette.Yellow : (k == 1 ? Palette.Orange : Palette.Copper);
                    s.Set(cx, cy + 2 + k, flameChar, col);
                }
                if (len > 2)
                {
                    s.Set(cx - 1, cy + 2, '.', Palette.Orange);
                    s.Set(cx + 1, cy + 2, '.', Palette.Orange);
                }

                if (MuzzleFlash > 0)
                {
                    s.Set(cx, cy - 3, '*', Palette.White);
                    s.Set(cx - 1, cy - 2, '.', Palette.Yellow);
                    s.Set(cx + 1, cy - 2, '.', Palette.Yellow);
                }
            }

            if (p.Shield > 0)
            {
                bool hot = p.ShieldFlash > 0;
                byte ringColor = hot ? Palette.White : Palette.Cyan;
                float radius = 4.2f + (float)Math.Sin(Time * 6f) * 0.4f;
                int points = 14;
                for (int i = 0; i < points; i++)
                {
                    float a = (float)(Math.PI * 2.0 * i / points) + Time * 1.2f;
                    int x = (int)(p.X + Math.Cos(a) * radius + 0.5f);
                    int y = (int)(p.Y + Math.Sin(a) * radius * 0.62f + 0.5f);
                    s.Set(x, y, '.', ringColor);
                }
            }
        }

        void DrawBullets(Screen s)
        {
            for (int i = 0; i < Bullets.Count; i++)
            {
                Bullet b = Bullets[i];
                if (!b.Alive) continue;
                int x = (int)(b.X + 0.5f);
                int y = (int)(b.Y + 0.5f);
                if (b.IsPlayer)
                {
                    s.Set(x, y, b.Glyph, b.Color);
                    s.Set(x, y + 1, '.', Palette.BlueDeep);
                    s.Set(x, y + 2, '.', Palette.BlueDark);
                }
                else
                {
                    s.Set(x, y, b.Glyph, b.Color);
                    s.Set(x, y - 1, '.', Palette.PurpleDeep);
                }
            }
        }

        void DrawEnemies(Screen s)
        {
            for (int i = 0; i < Enemies.Count; i++)
            {
                Enemy e = Enemies[i];
                if (!e.Alive || e.Sprite == null) continue;

                int left = (int)(e.X + 0.5f) - e.W / 2;
                int top = (int)(e.Y + 0.5f) - e.H / 2;
                bool flash = e.HitFlash > 0;

                // dark silhouette halo keeps the boss readable over nebula gas
                if (e.Kind == EnemyKind.Boss)
                {
                    int[] ox = { 1, -1, 0, 0 };
                    int[] oy = { 0, 0, 1, -1 };
                    for (int k = 0; k < 4; k++)
                    {
                        for (int r = 0; r < e.Sprite.Length; r++)
                        {
                            string srow = e.Sprite[r];
                            for (int c = 0; c < srow.Length; c++)
                                if (srow[c] != ' ') s.Set(left + c + ox[k], top + r + oy[k], srow[c], Palette.BlueInk);
                        }
                    }
                }

                for (int r = 0; r < e.Sprite.Length; r++)
                {
                    string row = e.Sprite[r];
                    for (int c = 0; c < row.Length; c++)
                    {
                        char ch = row[c];
                        if (ch == ' ') continue;

                        byte col = flash ? Palette.White : e.Color;
                        if (e.Kind == EnemyKind.Asteroid)
                        {
                            if (ch == 'o') col = flash ? Palette.White : Palette.Gray;
                            else if (ch == 'x') col = Palette.DimGray;
                        }
                        else if (e.Kind == EnemyKind.Drone)
                        {
                            if (ch == 'o') col = flash ? Palette.White : Palette.Yellow;
                            else if (ch == '=') col = flash ? Palette.White : Palette.Red;
                        }
                        else if (e.Kind == EnemyKind.Hunter)
                        {
                            if (ch == 'v') col = flash ? Palette.White : Palette.Pink;
                        }
                        else if (e.Kind == EnemyKind.Boss)
                        {
                            if (ch == 'O')
                                col = flash ? Palette.White : (((int)(e.Age * 7f)) % 2 == 0 ? Palette.Gold : Palette.Yellow);
                            else if (ch == '#') col = flash ? Palette.White : Palette.Pink;
                            else if (ch == '=') col = flash ? Palette.White : Palette.CyanBright;
                            else col = flash ? Palette.White : Palette.Magenta;
                        }

                        s.Set(left + c, top + r, ch, col);
                    }
                }

                if (e.Kind == EnemyKind.Boss && !flash)
                {
                    int glowX = left + e.W / 2;
                    s.Set(glowX, top + 5, (int)(e.Age * 10f) % 2 == 0 ? 'v' : '.', Palette.Orange);
                }
            }
        }

        void DrawParticles(Screen s)
        {
            for (int i = 0; i < Particles.Count; i++)
            {
                Particle p = Particles[i];
                float f = p.Life / p.MaxLife;
                if (f <= 0) continue;
                int idx = (int)((1f - f) * (p.Ramp.Length - 1));
                if (idx < 0) idx = 0;
                if (idx >= p.Ramp.Length) idx = p.Ramp.Length - 1;

                char c;
                if (f > 0.6f) c = p.Glyph;
                else if (f > 0.28f) c = '+';
                else c = '.';

                s.Set((int)(p.X + 0.5f), (int)(p.Y + 0.5f), c, p.Ramp[idx]);
            }
        }

        void DrawShockwaves(Screen s)
        {
            for (int i = 0; i < Shockwaves.Count; i++)
            {
                Shockwave w = Shockwaves[i];
                float f = w.Life / w.MaxLife;
                byte col = f > 0.65f ? Palette.White : (f > 0.3f ? w.Color : Palette.DimGray);
                char c = f > 0.45f ? 'o' : '.';
                int points = 40;
                for (int k = 0; k < points; k++)
                {
                    float a = (float)(Math.PI * 2.0 * k / points);
                    int x = (int)(w.X + Math.Cos(a) * w.Radius + 0.5f);
                    int y = (int)(w.Y + Math.Sin(a) * w.Radius * 0.55f + 0.5f);
                    s.Set(x, y, c, col);
                }
            }
        }

        void DrawPowerUps(Screen s)
        {
            for (int i = 0; i < PowerUps.Count; i++)
            {
                PowerUp p = PowerUps[i];
                if (!p.Alive) continue;
                int x = (int)(p.X + 0.5f);
                int y = (int)(p.Y + 0.5f);
                bool blink = ((int)(p.Age * 8f) % 2) == 0;

                byte col;
                switch (p.Kind)
                {
                    case 'T': col = Palette.Gold; break;
                    case 'S': col = Palette.CyanBright; break;
                    case 'H': col = Palette.Green; break;
                    default: col = Palette.Blue; break;
                }

                s.Set(x - 1, y, '[', col);
                s.Set(x, y, p.Kind, blink ? Palette.White : col);
                s.Set(x + 1, y, ']', col);
                if (blink)
                {
                    s.Set(x - 2, y, '.', Palette.Cyan);
                    s.Set(x + 2, y, '.', Palette.Cyan);
                }
            }
        }

        void DrawFloatTexts(Screen s)
        {
            for (int i = 0; i < FloatTexts.Count; i++)
            {
                FloatText f = FloatTexts[i];
                if (f.Life <= 0.22f && ((int)(f.Life * 30f) % 2) == 0) continue;
                int x = (int)(f.X + 0.5f) - f.Text.Length / 2;
                int y = (int)(f.Y + 0.5f);
                s.Write(x, y, f.Text, f.Color);
            }
        }

        void DrawHud(Screen s)
        {
            s.FillRect(0, 0, Width, 2, Palette.White, Palette.Darker);

            s.WriteDirect(1, 0, "SCORE", Palette.Gray, Palette.Darker);
            s.WriteDirect(7, 0, Score.ToString("000000"), Palette.White, Palette.Darker);

            if (Chain > 1)
            {
                int mult = 1 + Math.Min(5, Chain / 3);
                string chain = "CHAIN " + Chain + " x" + mult;
                int waveX = (Width - ("WAVE 00").Length) / 2;
                if (15 + chain.Length < waveX - 3)
                    s.WriteDirect(15, 0, chain, Palette.Gold, Palette.Darker);
            }

            string wave = "WAVE " + Wave.ToString("00");
            byte waveColor = Palette.CyanBright;
            s.WriteDirect((Width - wave.Length) / 2, 0, wave, waveColor, Palette.Darker);

            string best = "BEST " + Best.ToString("000000");
            s.WriteDirect(Math.Max(0, Width - best.Length - 1), 0, best, Palette.DimGray, Palette.Darker);

            int x = 1;
            s.WriteDirect(x, 1, "HULL", Palette.Gray, Palette.Darker);
            x += 5;
            int barW = Math.Max(6, Math.Min(18, Width / 8));
            int filled = (int)Math.Round((double)Player.Hull / Player.MaxHull * barW);
            if (filled < 0) filled = 0;
            if (filled > barW) filled = barW;

            byte hullColor = Player.Hull > 60 ? Palette.Green : (Player.Hull > 30 ? Palette.Gold : Palette.Red);
            s.WriteDirect(x, 1, "[", Palette.DimGray, Palette.Darker);
            x++;
            for (int i = 0; i < barW; i++)
            {
                s.WriteDirect(x + i, 1, i < filled ? "#" : "-", i < filled ? hullColor : Palette.Dark, Palette.Darker);
            }
            x += barW;
            s.WriteDirect(x, 1, "]", Palette.DimGray, Palette.Darker);
            x += 2;

            string pct = ((int)Math.Round(Player.Hull * 100f / Player.MaxHull)) + "%";
            s.WriteDirect(x, 1, pct, hullColor, Palette.Darker);
            x += pct.Length + 2;

            string gun = "GUN " + Player.WeaponLevel;
            s.WriteDirect(x, 1, gun, Palette.Gold, Palette.Darker);
            x += gun.Length + 2;

            string shield = Player.Shield > 0 ? "SHIELD " + Player.Shield : "SHIELD --";
            s.WriteDirect(x, 1, shield, Player.Shield > 0 ? Palette.CyanBright : Palette.DimGray, Palette.Darker);
            x += shield.Length + 2;

            int warpW = Math.Max(5, Math.Min(10, Width / 14));
            string warpLabel = "WARP ";
            int need = warpLabel.Length + warpW + 2;
            if (x + need < Width)
            {
                s.WriteDirect(x, 1, warpLabel, Palette.Gray, Palette.Darker);
                x += warpLabel.Length;
                s.WriteDirect(x, 1, "[", Palette.DimGray, Palette.Darker);
                x++;
                int wf = (int)Math.Round(Player.WarpEnergy * warpW);
                if (wf < 0) wf = 0;
                if (wf > warpW) wf = warpW;
                for (int i = 0; i < warpW; i++)
                    s.WriteDirect(x + i, 1, i < wf ? "=" : "-",
                        i < wf ? Palette.Blue : Palette.Dark, Palette.Darker);
                x += warpW;
                s.WriteDirect(x, 1, "]", Palette.DimGray, Palette.Darker);
            }
        }

        void DrawBossBar(Screen s)
        {
            if (!BossActive || Boss == null || !Boss.Alive) return;

            int y = 2;
            s.FillRect(0, y, Width, 1, Palette.White, Palette.Darker);

            int barW = Math.Max(10, Math.Min(60, Width - 30));
            string label = "DREADNOUGHT";
            int total = label.Length + barW + 10;
            int x = Math.Max(1, (Width - total) / 2);

            s.WriteDirect(x, y, label, Palette.Magenta, Palette.Darker);
            x += label.Length + 1;
            s.WriteDirect(x, y, "[", Palette.DimGray, Palette.Darker);
            x++;
            int filled = (int)Math.Round((double)Boss.HP / Boss.MaxHP * barW);
            if (filled < 0) filled = 0;
            if (filled > barW) filled = barW;
            byte col = Boss.HP < Boss.MaxHP * 0.4f ? Palette.Red : Palette.Purple;
            for (int i = 0; i < barW; i++)
                s.WriteDirect(x + i, y, i < filled ? "#" : "-", i < filled ? col : Palette.Dark, Palette.Darker);
            s.WriteDirect(x + barW, y, "]", Palette.DimGray, Palette.Darker);
            s.WriteDirect(x + barW + 2, y, ((int)(Boss.HP * 100f / Boss.MaxHP)) + "%", Palette.Magenta, Palette.Darker);
        }

        void DrawBanner(Screen s)
        {
            if (BannerTimer <= 0) return;
            float t = BannerTimer;
            if (t < 0.5f && ((int)(t * 20f) % 2) == 0) return;

            int y = Math.Max(4, Height / 5);
            if (BannerBig)
            {
                y = Math.Max(9, Height / 2);
                string line = ">> " + BannerText + " <<";
                s.WriteCentered(y, line, Palette.Magenta);
                s.WriteCentered(y + 1, "!! WARNING !!", Palette.RedBright);
                int lineW = Math.Min(Width - 4, line.Length + 20);
                int half = (Width - lineW) / 2;
                for (int i = 0; i < lineW; i++)
                {
                    s.Set(half + i, y - 1, '=', Palette.PurpleDeep);
                    s.Set(half + i, y + 2, '=', Palette.PurpleDeep);
                }
            }
            else
            {
                string text = ">> " + BannerText + " <<";
                s.WriteCentered(y, text, BannerColor);
                int lineW = Math.Min(Width - 4, text.Length + 12);
                for (int i = 0; i < lineW; i++)
                {
                    int half = (Width - lineW) / 2;
                    s.Set(half + i, y - 1, '-', Palette.BlueDark);
                    s.Set(half + i, y + 1, '-', Palette.BlueDark);
                }
            }
        }

        void DrawBottomHints(Screen s)
        {
            if (Height < 16) return;
            int y = Height - 1;
            s.FillRect(0, y, Width, 1, Palette.White, Palette.Darker);

            string left;
            if (State == GameState.Paused)
                left = "P RESUME   |   M SOUND   |   ESC QUIT";
            else if (State == GameState.GameOver)
                left = "R / ENTER RETRY   |   M SOUND   |   ESC QUIT";
            else if (Width < 84)
                left = "[A/D] MOVE  [SPACE] FIRE  [P] PAUSE  [M] SOUND";
            else
                left = "[A/D] MOVE  [SPACE] FIRE  [SHIFT] WARP  [P] PAUSE  [M] SOUND";
            byte lc = State == GameState.Paused ? Palette.CyanBright : Palette.Gray;
            s.WriteDirect(1, y, left, lc, Palette.Darker);

            string right = (int)Math.Round(Fps) + " FPS";
            if (Audio.Muted) right = "MUTED  " + right;
            s.WriteDirect(Math.Max(0, Width - right.Length - 1), y, right, Palette.DimGray, Palette.Darker);
        }

        void DrawTitle(Screen s)
        {
            int scale = (Width >= 94 && Height >= 32) ? 2 : 1;
            int logoH = BigFont.Height(scale);
            int logoW = BigFont.Width("STARFALL", scale);
            int logoTop = Height >= 34 ? 3 : Math.Max(1, (Height - 20) / 2);

            int px = (Width - logoW) / 2;
            s.FillRect(px - 3, logoTop - 2, logoW + 6, logoH + 4, Palette.White, Palette.Darker);
            // subtle frame
            for (int i = 0; i < logoW + 4; i++)
            {
                s.Direct(px - 2 + i, logoTop - 2, '-', Palette.BlueDark, Palette.Darker);
                s.Direct(px - 2 + i, logoTop + logoH + 1, '-', Palette.BlueDark, Palette.Darker);
            }

            byte[] colors = { Palette.White, Palette.CyanBright, Palette.CyanBright, Palette.Cyan, Palette.Teal };
            BigFont.Draw(s, "STARFALL", Width / 2, logoTop, scale, colors, Palette.BlueInk);

            int y = logoTop + logoH + 2;
            s.WriteCentered(y, "V O I D   R U N N E R", Palette.Gold);
            y++;
            s.WriteCentered(y, "A .NET 4.8 CONSOLE SHOOTER", Palette.DimGray);
            y += 2;

            bool blink = ((int)(TitleTime * 2f) % 2) == 0;
            if (blink || TitleTime < 1f)
                s.WriteCentered(y, ">> PRESS ENTER TO LAUNCH <<", Palette.CyanBright);

            int panelW = Math.Min(Width - 8, 76);
            int panelH = 8;
            int panelX = (Width - panelW) / 2;
            int panelY = Height - panelH - 2;
            if (panelY < y + 2) panelY = y + 2;

            Starfall.Draw.Panel(s, panelX, panelY, panelW, panelH, Palette.Blue, Palette.Darker);
            int tx = panelX + 3;
            s.WriteDirect(tx, panelY + 1, "MOVE   A / D  or  < >        FIRE   SPACE", Palette.Silver, Palette.Darker);
            s.WriteDirect(tx, panelY + 2, "WARP   SHIFT                 PAUSE  P", Palette.Silver, Palette.Darker);
            s.WriteDirect(tx, panelY + 3, "SOUND  M                     QUIT   ESC", Palette.Silver, Palette.Darker);
            if (panelW >= 70)
            {
                s.WriteDirect(tx, panelY + 5, "Chain kills to multiply score. A Dreadnought arrives every 5th wave.", Palette.DimGray, Palette.Darker);
                s.WriteDirect(tx, panelY + 6, "Collect  [T] gun   [S] shield   [H] hull   [W] warp", Palette.DimGray, Palette.Darker);
            }
            else
            {
                s.WriteDirect(tx, panelY + 5, "Chain kills to multiply score.", Palette.DimGray, Palette.Darker);
                s.WriteDirect(tx, panelY + 6, "Boss every 5th wave.", Palette.DimGray, Palette.Darker);
            }

            if (Best > 0)
            {
                string best = "BEST " + Best.ToString("000000");
                s.WriteCentered(panelY - 2, best, Palette.Gray);
            }
        }

        void DrawPause(Screen s)
        {
            int w = Math.Min(Width - 8, 54);
            int h = 9;
            int x = (Width - w) / 2;
            int y = (Height - h) / 2;
            Starfall.Draw.Panel(s, x, y, w, h, Palette.Cyan, Palette.Darker);
            BigFont.Draw(s, "PAUSED", Width / 2, y + 1, 1,
                new byte[] { Palette.White, Palette.CyanBright, Palette.Cyan, Palette.Blue, Palette.BlueDeep },
                Palette.BlueInk);
            s.WriteCentered(y + 7, "PRESS P TO RESUME", Palette.Gray);
        }

        void DrawGameOver(Screen s)
        {
            int w = Math.Min(Width - 4, Math.Max(72, BigFont.Width("GAME OVER", 1) + 6));
            int h = 14;
            int x = (Width - w) / 2;
            int y = Math.Max(2, (Height - h) / 2);
            Starfall.Draw.Panel(s, x, y, w, h, Palette.Red, Palette.Darker);

            BigFont.Draw(s, "GAME OVER", Width / 2, y + 1, 1,
                new byte[] { Palette.White, Palette.RedBright, Palette.Red, Palette.RedDeep, Palette.PurpleDeep },
                Palette.BlueInk);

            string line1 = "SCORE " + Score.ToString("000000") + "     BEST " + Best.ToString("000000");
            s.WriteCentered(y + 8, line1, Palette.White);

            string line2 = "WAVE " + Wave.ToString("00") + "     KILLS " + Kills + "     SHOTS " + ShotsFired;
            s.WriteCentered(y + 9, line2, Palette.Gray);

            if (NewHighScore)
                s.WriteCentered(y + 10, "* NEW HIGH SCORE *", Palette.Gold);

            if (GameOverTime > 0.7f || ((int)(GameOverTime * 3f) % 2) == 0)
                s.WriteCentered(y + 12, "R / ENTER RETRY      ESC QUIT", Palette.CyanBright);
        }

        static float Clamp(float v, float min, float max)
        {
            if (v < min) return min;
            if (v > max) return max;
            return v;
        }
    }
}
