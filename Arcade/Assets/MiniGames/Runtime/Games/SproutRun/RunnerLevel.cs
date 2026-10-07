using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace MiniGames.SproutRun
{
    public enum TileKind { Empty, Ground, Crate, Seed, Dew, Spikes, Beetle }

    [Serializable]
    public sealed class LevelCell
    {
        public int x, y;
        public TileKind kind;
        public LevelCell(int x, int y, TileKind kind) { this.x = x; this.y = y; this.kind = kind; }
    }

    [Serializable]
    public sealed class RunnerLevel
    {
        public int version = 1;
        public string name = "My little world";
        public int width = 80, height = 12;
        public int spawnX = 2, spawnY = 1, goalX = 76, goalY = 1;
        public List<LevelCell> cells = new List<LevelCell>();

        public RunnerLevel Copy() => JsonUtility.FromJson<RunnerLevel>(JsonUtility.ToJson(this));
        public string ToJson() => JsonUtility.ToJson(this, true);

        public TileKind At(int x, int y)
        {
            foreach (var cell in cells) if (cell.x == x && cell.y == y) return cell.kind;
            return TileKind.Empty;
        }

        public void Set(int x, int y, TileKind kind)
        {
            if (x < 0 || x >= width || y < 0 || y >= height) return;
            cells.RemoveAll(c => c.x == x && c.y == y);
            if (kind != TileKind.Empty) cells.Add(new LevelCell(x, y, kind));
        }

        public bool Validate(out string error)
        {
            error = null;
            if (version != 1) error = "This level needs schema version 1.";
            else if (string.IsNullOrWhiteSpace(name) || name.Length > 40) error = "Use a level name between 1 and 40 characters.";
            else if (width < 36 || width > 160 || height != 12) error = "Levels must be 36–160 columns wide and exactly 12 rows tall.";
            else if (cells == null || cells.Count > width * height) error = "The tile list is missing or too large.";
            if (error != null) return false;
            var occupied = new HashSet<int>();
            foreach (var cell in cells)
            {
                if (cell == null || cell.x < 0 || cell.x >= width || cell.y < 0 || cell.y >= height || cell.kind < TileKind.Ground || cell.kind > TileKind.Beetle)
                { error = "Every tile needs an in-bounds position and a kind from 1 to 6."; return false; }
                if (!occupied.Add(cell.y * width + cell.x))
                { error = "There are two tiles in the same cell. Remove the duplicate."; return false; }
            }
            if (spawnX < 1 || spawnX >= width - 1 || spawnY < 1 || spawnY >= height - 1 ||
                goalX <= spawnX + 2 || goalX >= width - 1 || goalY < 1 || goalY >= height - 1)
                error = "Place start and finish inside the grid, with finish at least three columns after start.";
            else if (At(spawnX, spawnY) != TileKind.Empty || At(goalX, goalY) != TileKind.Empty || At(goalX, goalY + 1) != TileKind.Empty)
                error = "Clear the start cell and both cells of the finish post.";
            else if (!Support(At(spawnX, spawnY - 1)) || !Support(At(goalX, goalY - 1)))
                error = "Put ground or a crate directly under the start and finish.";
            return error == null;
        }

        static bool Support(TileKind kind) => kind == TileKind.Ground || kind == TileKind.Crate;

        public static bool TryParse(string json, out RunnerLevel level, out string error)
        {
            level = null;
            if (string.IsNullOrWhiteSpace(json) || json.Length > 128 * 1024)
            { error = "Paste a level JSON document smaller than 128 KB."; return false; }
            try
            {
                var candidate = JsonUtility.FromJson<RunnerLevel>(json);
                if (candidate == null) { error = "The JSON does not contain a level."; return false; }
                if (!candidate.Validate(out error)) return false;
                level = candidate;
                return true;
            }
            catch (Exception e) when (e is ArgumentException || e is FormatException)
            { error = "That is not valid level JSON: " + e.Message; return false; }
        }

        public static RunnerLevel Blank()
        {
            var level = new RunnerLevel();
            for (int x = 0; x < level.width; x++) level.Set(x, 0, TileKind.Ground);
            return level;
        }

        public static RunnerLevel BuiltIn(int index)
        {
            var level = Blank();
            level.name = new[] { "First shoots", "Mind the gap", "Room to grow" }[index];
            if (index == 0)
            {
                foreach (int x in new[] { 7, 12, 17, 39, 60 }) level.Set(x, 1, TileKind.Seed);
                foreach (int x in new[] { 23, 24, 43, 44, 65 })
                    for (int y = 1; y <= 4; y++) level.Set(x, y, TileKind.Crate);
                for (int x = 31; x < 36; x++) level.Set(x, 4, TileKind.Crate);
            }
            else if (index == 1)
            {
                foreach (int x in new[] { 7, 12, 33, 61 }) level.Set(x, 1, TileKind.Seed);
                foreach (int x in new[] { 19, 20, 21, 37, 38, 39 }) level.Set(x, 0, TileKind.Empty);
                foreach (int x in new[] { 29, 55, 56 }) level.Set(x, 1, TileKind.Spikes);
                level.Set(46, 1, TileKind.Beetle);
                for (int y = 1; y <= 3; y++) level.Set(67, y, TileKind.Crate);
            }
            else
            {
                foreach (int x in new[] { 7, 11, 15, 46, 52, 59 }) level.Set(x, 1, TileKind.Seed);
                for (int y = 1; y <= 4; y++) { level.Set(19, y, TileKind.Crate); level.Set(64, y, TileKind.Crate); }
                level.Set(24, 1, TileKind.Dew); level.Set(27, 1, TileKind.Dew);
                for (int x = 30; x <= 39; x++) level.Set(x, 3, TileKind.Ground);
                level.Set(43, 1, TileKind.Beetle);
                level.Set(69, 1, TileKind.Spikes);
            }
            return level;
        }
    }

    public static class RunnerStorage
    {
        public static string DirectoryPath => Path.Combine(Application.persistentDataPath, "SproutRun");
        public static string DraftPath => Path.Combine(DirectoryPath, "draft.json");

        public static bool Save(RunnerLevel level, string path, out string error)
        {
            if (!level.Validate(out error)) return false;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                string temporary = path + ".tmp";
                File.WriteAllText(temporary, level.ToJson());
                if (File.Exists(path)) File.Replace(temporary, path, path + ".bak");
                else File.Move(temporary, path);
                return true;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            { error = "Could not save: " + e.Message; return false; }
        }

        public static bool Load(string path, out RunnerLevel level, out string error)
        {
            level = null;
            try
            {
                if (!File.Exists(path)) { error = "No saved draft yet. Paint a level and choose Save."; return false; }
                if (new FileInfo(path).Length > 128 * 1024) { error = "The saved file is too large (128 KB maximum)."; return false; }
                return RunnerLevel.TryParse(File.ReadAllText(path), out level, out error);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            { error = "Could not load: " + e.Message; return false; }
        }
    }
}
