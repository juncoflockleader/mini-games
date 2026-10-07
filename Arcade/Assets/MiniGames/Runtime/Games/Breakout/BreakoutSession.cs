using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

[assembly: InternalsVisibleTo("MiniGames.Tests")]

namespace MiniGames.Breakout
{
    public enum RoundState { Ready, Playing, Paused, LevelCleared, Won, GameOver }
    public enum HitKind { Wall, Paddle, Brick, Lost }

    public sealed class Brick
    {
        public Rect Bounds { get; }
        public int Row { get; }
        public bool Alive { get; internal set; } = true;
        public Brick(Rect bounds, int row) { Bounds = bounds; Row = row; }
    }

    // Screen-space simulation (positive Y points down). No scene objects or frame-rate-dependent physics.
    public sealed class BreakoutSession
    {
        public const float Width = 920, Height = 550, Radius = 7;
        public const float PaddleWidth = 116, PaddleY = 502, PaddleHeight = 12;
        public const int LevelCount = 3;
        readonly List<Brick> bricks = new List<Brick>();
        RoundState resumeState;
        public IReadOnlyList<Brick> Bricks => bricks;
        public RoundState State { get; internal set; }
        public Vector2 Ball { get; internal set; }
        public Vector2 Velocity { get; internal set; }
        public float PaddleX { get; private set; }
        public int Score { get; private set; }
        public int Lives { get; private set; }
        public int Level { get; private set; }
        public int Combo { get; private set; }
        public int Remaining { get; private set; }
        public event Action<Vector2, HitKind, int> Hit;

        public BreakoutSession() { Restart(); }

        public void Restart()
        {
            Score = 0;
            Lives = 3;
            Level = 1;
            PaddleX = Width / 2;
            LoadLevel();
        }

        void LoadLevel()
        {
            bricks.Clear();
            for (int row = 0; row < 5; row++)
            for (int col = 0; col < 10; col++)
            {
                if (Level == 2 && (col + row) % 4 == 0) continue;
                if (Level == 3 && Mathf.Abs(col - 4.5f) < (4 - row) * .65f) continue;
                bricks.Add(new Brick(new Rect(33 + col * 86, 58 + row * 36, 78, 26), row));
            }
            Remaining = bricks.Count;
            ReadyBall();
        }

        void ReadyBall()
        {
            Combo = 0;
            Velocity = Vector2.zero;
            State = RoundState.Ready;
            Ball = new Vector2(PaddleX, PaddleY - Radius - 3);
        }

        public void MovePaddle(float x)
        {
            if (State != RoundState.Ready && State != RoundState.Playing) return;
            PaddleX = Mathf.Clamp(x, PaddleWidth / 2 + 6, Width - PaddleWidth / 2 - 6);
            if (State == RoundState.Ready) Ball = new Vector2(PaddleX, PaddleY - Radius - 3);
        }

        public void Launch()
        {
            if (State != RoundState.Ready) return;
            Velocity = new Vector2(.32f, -1).normalized * (390 + (Level - 1) * 45);
            State = RoundState.Playing;
        }

        public void TogglePause()
        {
            if (State == RoundState.Paused) State = resumeState;
            else if (State == RoundState.Playing || State == RoundState.Ready)
            {
                resumeState = State;
                State = RoundState.Paused;
            }
        }

        public void NextLevel()
        {
            if (State != RoundState.LevelCleared) return;
            Level++;
            LoadLevel();
        }

        public void Tick(float deltaTime)
        {
            if (State != RoundState.Playing || deltaTime <= 0) return;
            float remainingTime = Mathf.Min(deltaTime, .1f);
            // Sweep the entire travel distance to the earliest impact, then spend the leftover time.
            for (int iteration = 0; iteration < 12 && remainingTime > .00001f; iteration++)
            {
                float earliest = remainingTime;
                Vector2 normal = Vector2.zero;
                Brick target = null;
                HitKind kind = HitKind.Wall;

                void Wall(float time, Vector2 n)
                {
                    if (time >= 0 && time <= earliest) { earliest = time; normal = n; }
                }
                if (Velocity.x < 0) Wall((Radius - Ball.x) / Velocity.x, Vector2.right);
                if (Velocity.x > 0) Wall((Width - Radius - Ball.x) / Velocity.x, Vector2.left);
                if (Velocity.y < 0) Wall((Radius - Ball.y) / Velocity.y, Vector2.up);

                var paddle = new Rect(PaddleX - PaddleWidth / 2, PaddleY, PaddleWidth, PaddleHeight);
                if (Velocity.y > 0 && Ball.y <= PaddleY - Radius + .01f &&
                    Sweep(Ball, Velocity, Expand(paddle), earliest, out float pt, out Vector2 pn))
                {
                    earliest = pt; normal = pn; kind = HitKind.Paddle;
                }
                foreach (Brick brick in bricks)
                {
                    if (!brick.Alive || !Sweep(Ball, Velocity, Expand(brick.Bounds), earliest, out float t, out Vector2 n)) continue;
                    earliest = t; normal = n; target = brick; kind = HitKind.Brick;
                }

                Ball += Velocity * earliest;
                remainingTime -= earliest;
                if (normal == Vector2.zero) break;
                if (kind == HitKind.Paddle)
                {
                    float offset = Mathf.Clamp((Ball.x - PaddleX) / (PaddleWidth / 2), -1, 1);
                    float angle = offset * 1.05f;
                    Velocity = new Vector2(Mathf.Sin(angle), -Mathf.Cos(angle)) * Velocity.magnitude;
                    Combo = 0;
                }
                else Velocity = Vector2.Reflect(Velocity, normal);
                Ball += normal * .02f;
                if (target != null)
                {
                    target.Alive = false;
                    Remaining--;
                    Combo++;
                    Score += (5 - target.Row) * 10 * Mathf.Min(Combo, 5);
                    Velocity = Velocity.normalized * Mathf.Min(Velocity.magnitude + 4, 680);
                }
                Hit?.Invoke(Ball, kind, target?.Row ?? 0);
                if (Remaining == 0)
                {
                    State = Level == LevelCount ? RoundState.Won : RoundState.LevelCleared;
                    Velocity = Vector2.zero;
                    return;
                }
            }
            if (Ball.y > Height + Radius)
            {
                Lives--;
                Hit?.Invoke(Ball, HitKind.Lost, 0);
                if (Lives == 0) { State = RoundState.GameOver; Velocity = Vector2.zero; }
                else ReadyBall();
            }
        }

        static Rect Expand(Rect r) => new Rect(r.x - Radius, r.y - Radius, r.width + Radius * 2, r.height + Radius * 2);

        // Ray versus expanded rectangle: avoids tunneling even when a frame crosses a whole brick.
        internal static bool Sweep(Vector2 p, Vector2 v, Rect r, float maxTime, out float time, out Vector2 normal)
        {
            time = 0;
            normal = Vector2.zero;
            float near = float.NegativeInfinity, far = float.PositiveInfinity;
            for (int axis = 0; axis < 2; axis++)
            {
                float origin = p[axis], speed = v[axis];
                float min = axis == 0 ? r.xMin : r.yMin, max = axis == 0 ? r.xMax : r.yMax;
                if (Mathf.Abs(speed) < .00001f)
                {
                    if (origin < min || origin > max) return false;
                    continue;
                }
                float entry = (min - origin) / speed, exit = (max - origin) / speed;
                if (entry > exit) { float swap = entry; entry = exit; exit = swap; }
                if (entry > near)
                {
                    near = entry;
                    normal = axis == 0 ? new Vector2(-Mathf.Sign(speed), 0) : new Vector2(0, -Mathf.Sign(speed));
                }
                far = Mathf.Min(far, exit);
                if (near > far) return false;
            }
            time = near;
            return near >= 0 && near <= maxTime && far >= 0;
        }
    }
}
