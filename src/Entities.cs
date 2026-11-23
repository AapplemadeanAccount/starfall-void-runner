using System;
using System.Collections.Generic;

namespace Starfall
{
    public class Player
    {
        public float X;
        public float Y;
        public float VX;
        public float VY;

        public int Hull = 100;
        public int MaxHull = 100;
        public int Shield;
        public int WeaponLevel = 1;

        public float Invuln;
        public float FireCooldown;
        public float ShieldFlash;
        public float HitFlash;
        public float WarpEnergy = 1f;
        public float EnginePhase;
        public float Bob;

        public const int SpriteW = 7;
        public const int SpriteH = 4;

        public static readonly string[] Sprite =
        {
            "   ^   ",
            "  /#\\  ",
            " /###\\ ",
            "<##=##>"
        };

        public void Reset(int centerX, int centerY)
        {
            X = centerX;
            Y = centerY;
            VX = 0;
            VY = 0;
            Hull = 100;
            MaxHull = 100;
            Shield = 0;
            WeaponLevel = 1;
            Invuln = 2.2f;
            FireCooldown = 0.25f;
            ShieldFlash = 0;
            HitFlash = 0;
            WarpEnergy = 1f;
            EnginePhase = 0;
            Bob = 0;
        }
    }

    public class Bullet
    {
        public float X;
        public float Y;
        public float VX;
        public float VY;
        public bool IsPlayer;
        public char Glyph;
        public byte Color;
        public int Damage = 1;
        public float Life = 4f;
        public bool Alive = true;
    }

    public enum EnemyKind
    {
        Asteroid,
        Drone,
        Hunter,
        Boss
    }

    public class Enemy
    {
        public EnemyKind Kind;
        public float X;
        public float Y;
        public float VX;
        public float VY;
        public float BaseX;
        public float TargetY;
        public float Phase;
        public float Age;
        public float FireTimer;
        public float RadialTimer;

        public int HP;
        public int MaxHP;
        public int W;
        public int H;
        public int ScoreValue;
        public float DropChance;
        public byte Color;

        public float HitFlash;
        public bool Alive = true;
        public string[] Sprite;

        public static readonly string[][] Asteroids =
        {
            new[] { " .---. ", "( o o )", " '---' " },
            new[] { "  /\\  ", " /  o \\", " \\___/ " },
            new[] { " .-. ", "( x )", " '-' " }
        };

        public static readonly string[] DroneSprite =
        {
            " /=\\ ",
            "|o o|",
            " \\=/ "
        };

        public static readonly string[] HunterSprite =
        {
            "\\   /",
            " \\ / ",
            "  v  "
        };

        public static readonly string[] BossSprite =
        {
            "   /=======\\   ",
            "  /  #####  \\  ",
            " <  # O O #  > ",
            "  \\  #####  /  ",
            "   \\=======/   "
        };

        public static string[] SpriteFor(EnemyKind kind)
        {
            switch (kind)
            {
                case EnemyKind.Drone: return DroneSprite;
                case EnemyKind.Hunter: return HunterSprite;
                case EnemyKind.Boss: return BossSprite;
                default: return Asteroids[0];
            }
        }
    }

    public class Particle
    {
        public float X;
        public float Y;
        public float VX;
        public float VY;
        public float Life;
        public float MaxLife;
        public float Drag;
        public float Gravity;
        public char Glyph;
        public byte[] Ramp;
        public bool Ember;
    }

    public class Shockwave
    {
        public float X;
        public float Y;
        public float Radius;
        public float Speed;
        public float Life;
        public float MaxLife;
        public byte Color;
    }

    public class PowerUp
    {
        public float X;
        public float Y;
        public float VY;
        public float Phase;
        public float Age;
        public char Kind;   // T = triple, S = shield, H = hull, W = warp
        public bool Alive = true;
    }

    public class FloatText
    {
        public string Text;
        public float X;
        public float Y;
        public float VY;
        public float Life;
        public float MaxLife;
        public byte Color;
    }
}
