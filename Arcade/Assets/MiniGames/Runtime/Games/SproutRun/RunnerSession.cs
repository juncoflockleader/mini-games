using System;
using System.Collections.Generic;
using UnityEngine;

namespace MiniGames.SproutRun
{
    public enum RunState { Ready, Running, Paused, Dead, Cleared }
    public enum RunEvent { Grow, Shrink, Break, Hurt, Jump, Lost, Finish, Tight }

    public sealed class RunnerBeetle
    {
        public Vector2 Position;
        public float Direction = -1;
        public bool Alive = true;
        public Rect Bounds => new Rect(Position.x - .38f, Position.y, .76f, .6f);
    }

    public sealed class RunnerSession
    {
        public const float Speed = 5.5f, Step = 1f / 120;
        public static readonly float[] Scales = { 1, 1.45f, 2.1f, 3 };
        public readonly RunnerLevel Level;
        public readonly TileKind[,] Tiles;
        public readonly List<RunnerBeetle> Beetles = new List<RunnerBeetle>();
        public RunState State { get; private set; } = RunState.Ready;
        public Vector2 Position { get; internal set; }
        public int Tier { get; internal set; }
        public float VelocityY { get; internal set; }
        public bool Grounded { get; internal set; } = true;
        public float Protection { get; private set; }
        public float Elapsed { get; private set; }
        public int Seeds { get; private set; }
        public bool Blocked { get; private set; }
        public string DeathReason { get; private set; }
        public float Size => Scales[Tier];
        public Rect Body => Bounds(Position, Tier);
        public float Progress => Mathf.Clamp01((Position.x - Level.spawnX) / (Level.goalX - (float)Level.spawnX));
        public event Action<RunEvent, Vector2> Happened;
        RunState beforePause;
        float accumulator, jumpBuffer, coyote, tightCooldown;
        bool jumpHeld;

        public RunnerSession(RunnerLevel source)
        {
            if (!source.Validate(out string error)) throw new ArgumentException(error);
            Level = source.Copy();
            Tiles = new TileKind[Level.width, Level.height];
            foreach (var cell in Level.cells)
            {
                if (cell.kind == TileKind.Beetle) Beetles.Add(new RunnerBeetle { Position = new Vector2(cell.x + .5f, cell.y) });
                else Tiles[cell.x, cell.y] = cell.kind;
            }
            Position = new Vector2(Level.spawnX + .5f, Level.spawnY);
        }

        public static Rect Bounds(Vector2 feet, int tier)
        {
            float size = Scales[tier];
            return new Rect(feet.x - .32f * size, feet.y, .64f * size, .9f * size);
        }

        public TileKind At(int x, int y) => x < 0 || y < 0 || x >= Level.width || y >= Level.height ? TileKind.Empty : Tiles[x, y];
        public void Begin() { if (State == RunState.Ready) State = RunState.Running; }
        public void JumpPressed() { jumpHeld = true; jumpBuffer = .12f; }
        public void JumpReleased() => jumpHeld = false;
        public void TogglePause()
        {
            if (State == RunState.Paused) { State = beforePause; return; }
            if (State != RunState.Ready && State != RunState.Running) return;
            beforePause = State; State = RunState.Paused;
            jumpHeld = false; jumpBuffer = 0;
        }

        public void Tick(float delta)
        {
            if (State != RunState.Running) return;
            accumulator += Mathf.Clamp(delta, 0, .1f);
            while (accumulator >= Step && State == RunState.Running)
            { accumulator -= Step; Simulate(); }
        }

        void Simulate()
        {
            Elapsed += Step;
            Protection = Mathf.Max(0, Protection - Step);
            tightCooldown -= Step;
            coyote = Grounded ? .10f : coyote - Step;
            jumpBuffer -= Step;
            if (jumpBuffer > 0 && coyote > 0)
            {
                VelocityY = 10; Grounded = false; coyote = 0; jumpBuffer = 0;
                Emit(RunEvent.Jump, Position);
            }
            VelocityY -= 26 * (VelocityY > 0 && !jumpHeld ? 2.6f : 1) * Step;
            Move(Speed * Step, true);
            Grounded = false;
            Move(VelocityY * Step, false);
            Collect();
            MoveBeetles();
            if (Position.y < -3) Die("A little too far down. Time your jump before the gap.");
            if (State == RunState.Running && Body.Overlaps(new Rect(Level.goalX, Level.goalY, 1, 2.5f)))
            { State = RunState.Cleared; Emit(RunEvent.Finish, Position); }
        }

        void Move(float amount, bool horizontal)
        {
            var next = Position + (horizontal ? new Vector2(amount, 0) : new Vector2(0, amount));
            Rect body = Bounds(next, Tier);
            float resolved = horizontal ? next.x : next.y;
            bool hit = false;
            foreach (var cell in Overlapping(body))
            {
                var kind = Tiles[cell.x, cell.y];
                if (kind != TileKind.Ground && kind != TileKind.Crate) continue;
                if (kind == TileKind.Crate && Tier >= 1)
                { Tiles[cell.x, cell.y] = TileKind.Empty; Emit(RunEvent.Break, new Vector2(cell.x + .5f, cell.y + .5f)); continue; }
                hit = true;
                if (horizontal) resolved = Mathf.Min(resolved, cell.x - body.width / 2);
                else if (amount > 0) resolved = Mathf.Min(resolved, cell.y - body.height);
                else resolved = Mathf.Max(resolved, cell.y + 1);
            }
            if (horizontal)
            {
                Blocked = hit;
                next.x = Mathf.Clamp(resolved, body.width / 2, Level.width - body.width / 2);
            }
            else
            {
                next.y = resolved;
                if (hit) { if (amount <= 0) Grounded = true; VelocityY = 0; }
            }
            Position = next;
        }

        // Epsilon excludes touching edges, including the floor under a grounded body.
        IEnumerable<Vector2Int> Overlapping(Rect body)
        {
            int minX = Mathf.Max(0, Mathf.FloorToInt(body.xMin + .0001f));
            int maxX = Mathf.Min(Level.width - 1, Mathf.FloorToInt(body.xMax - .0001f));
            int minY = Mathf.Max(0, Mathf.FloorToInt(body.yMin + .0001f));
            int maxY = Mathf.Min(Level.height - 1, Mathf.FloorToInt(body.yMax - .0001f));
            for (int y = minY; y <= maxY; y++)
            for (int x = minX; x <= maxX; x++) yield return new Vector2Int(x, y);
        }

        void Collect()
        {
            // Capture the body before pickups; growth is reflected in the next physics step.
            foreach (var cell in Overlapping(Body))
            {
                var kind = Tiles[cell.x, cell.y];
                var center = new Vector2(cell.x + .5f, cell.y + .5f);
                if (kind == TileKind.Seed)
                {
                    if (!TryGrow()) continue;
                    Tiles[cell.x, cell.y] = TileKind.Empty; Seeds++; Emit(RunEvent.Grow, center);
                }
                else if (kind == TileKind.Dew)
                {
                    Tier = Mathf.Max(0, Tier - 1);
                    Tiles[cell.x, cell.y] = TileKind.Empty; Emit(RunEvent.Shrink, center);
                }
                else if (kind == TileKind.Spikes && Body.Overlaps(new Rect(cell.x + .1f, cell.y, .8f, .65f))) Damage();
            }
        }

        internal bool TryGrow()
        {
            if (Tier == Scales.Length - 1) return true;
            Rect grown = Bounds(Position, Tier + 1);
            bool fits = grown.xMin >= 0 && grown.xMax <= Level.width && grown.yMax <= Level.height;
            foreach (var cell in Overlapping(grown)) if (Tiles[cell.x, cell.y] == TileKind.Ground) fits = false;
            if (!fits)
            {
                if (tightCooldown <= 0) { Emit(RunEvent.Tight, Position); tightCooldown = 1; }
                return false;
            }
            Tier++;
            foreach (var cell in Overlapping(grown))
                if (Tiles[cell.x, cell.y] == TileKind.Crate)
                { Tiles[cell.x, cell.y] = TileKind.Empty; Emit(RunEvent.Break, new Vector2(cell.x + .5f, cell.y + .5f)); }
            return true;
        }

        void MoveBeetles()
        {
            foreach (var beetle in Beetles)
            {
                if (!beetle.Alive) continue;
                float ahead = beetle.Position.x + beetle.Direction * .5f;
                int x = Mathf.FloorToInt(ahead), y = Mathf.FloorToInt(beetle.Position.y);
                if (x <= 0 || x >= Level.width - 1 || Solid(At(x, y)) || !Solid(At(x, y - 1))) beetle.Direction *= -1;
                beetle.Position += Vector2.right * (beetle.Direction * 1.4f * Step);
                if (!Body.Overlaps(beetle.Bounds)) continue;
                if (Tier >= 2 || (VelocityY < 0 && Position.y > beetle.Position.y + .34f))
                {
                    beetle.Alive = false; Emit(RunEvent.Break, beetle.Position);
                    if (VelocityY < 0) VelocityY = 6;
                }
                else Damage();
            }
        }

        static bool Solid(TileKind kind) => kind == TileKind.Ground || kind == TileKind.Crate;
        internal void Damage()
        {
            if (Protection > 0 || State != RunState.Running) return;
            if (Tier == 0) { Die("That one stung. Jump over hazards, or grow before you meet them."); return; }
            Tier--; Protection = 1.25f; Emit(RunEvent.Hurt, Position);
        }
        void Die(string reason)
        {
            if (State != RunState.Running) return;
            State = RunState.Dead; DeathReason = reason; Emit(RunEvent.Lost, Position);
        }
        void Emit(RunEvent kind, Vector2 position) => Happened?.Invoke(kind, position);
    }
}
