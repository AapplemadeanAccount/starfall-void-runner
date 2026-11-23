using System;
using System.Collections.Generic;
using System.Threading;

namespace Starfall
{
    // Tiny non-blocking beeper. The game never waits on sound: a background
    // thread drains a short queue through Console.Beep.
    public static class Audio
    {
        public static bool Muted;

        static readonly object Gate = new object();
        static readonly Queue<int[]> Notes = new Queue<int[]>();
        static readonly AutoResetEvent Signal = new AutoResetEvent(false);
        static Thread worker;
        static int lastShootTick;

        public static void Init()
        {
            if (worker != null) return;
            worker = new Thread(Loop);
            worker.IsBackground = true;
            worker.Name = "StarfallAudio";
            worker.Start();
        }

        public static void ToggleMute()
        {
            Muted = !Muted;
            if (Muted)
            {
                lock (Gate) Notes.Clear();
            }
        }

        static void Loop()
        {
            while (true)
            {
                int[] note = null;
                lock (Gate)
                {
                    if (Notes.Count > 0) note = Notes.Dequeue();
                }

                if (note == null)
                {
                    Signal.WaitOne(120);
                    continue;
                }

                try
                {
                    for (int i = 0; i + 1 < note.Length; i += 2)
                    {
                        if (Muted) break;
                        Console.Beep(note[i], note[i + 1]);
                    }
                }
                catch
                {
                    // Some consoles (or redirected stdout) cannot beep. Ignore.
                }
            }
        }

        static void Play(params int[] note)
        {
            if (Muted) return;
            lock (Gate)
            {
                if (Notes.Count < 6) Notes.Enqueue(note);
            }
            Signal.Set();
        }

        public static void Shoot()
        {
            int now = Environment.TickCount;
            if (unchecked(now - lastShootTick) < 65) return;
            lastShootTick = now;
            Play(1500, 10, 1100, 8);
        }

        public static void Hit()
        {
            Play(560, 16);
        }

        public static void EnemyShoot()
        {
            Play(320, 14);
        }

        public static void ExplodeSmall()
        {
            Play(180, 40, 110, 45);
        }

        public static void ExplodeBig()
        {
            Play(240, 50, 160, 60, 90, 70, 60, 120);
        }

        public static void PlayerHit()
        {
            Play(150, 70, 90, 80);
        }

        public static void ShieldHit()
        {
            Play(900, 30, 1300, 30);
        }

        public static void PowerUp()
        {
            Play(660, 45, 990, 45, 1320, 60);
        }

        public static void WaveStart()
        {
            Play(500, 40, 760, 55);
        }

        public static void BossAlarm()
        {
            Play(180, 170, 150, 170, 120, 240);
        }

        public static void GameOver()
        {
            Play(420, 110, 320, 110, 220, 150, 120, 260);
        }
    }
}
