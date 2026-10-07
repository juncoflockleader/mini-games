using System;
using System.IO;
using MiniGames.SproutRun;
using NUnit.Framework;
using UnityEngine;

namespace MiniGames.Tests
{
    public sealed class RunnerSessionTests
    {
        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void AuthoredStagesPassValidation(int index)
        {
            Assert.That(RunnerLevel.BuiltIn(index).Validate(out var error), Is.True, error);
        }

        [Test]
        public void SeedsStackToFourSizesAndCapWithoutMovingFeet()
        {
            var game = new RunnerSession(RunnerLevel.Blank());
            Vector2 feet = game.Position;
            for (int i = 0; i < 6; i++) Assert.That(game.TryGrow(), Is.True);
            Assert.That(game.Tier, Is.EqualTo(3));
            Assert.That(game.Body.height, Is.EqualTo(2.7f).Within(.001f));
            Assert.That(game.Position, Is.EqualTo(feet));
        }

        [Test]
        public void TightGrowthLeavesSizeAndCratesUntouched()
        {
            var level = RunnerLevel.Blank(); level.Set(8, 2, TileKind.Ground); level.Set(7, 2, TileKind.Crate);
            var game = new RunnerSession(level) { Position = new Vector2(8, 1) };
            Assert.That(game.TryGrow(), Is.False);
            Assert.That(game.Tier, Is.Zero);
            Assert.That(game.At(7, 2), Is.EqualTo(TileKind.Crate));
        }

        [Test]
        public void CratesBlockSmallSproutsAndBreakForLargerOnes()
        {
            var level = RunnerLevel.Blank(); level.Set(6, 1, TileKind.Crate);
            var small = new RunnerSession(level) { Position = new Vector2(5.3f, 1) }; small.Begin(); small.Tick(.1f);
            Assert.That(small.At(6, 1), Is.EqualTo(TileKind.Crate)); Assert.That(small.Blocked, Is.True);
            Assert.That(small.Body.xMax, Is.EqualTo(6).Within(.001f));
            var large = new RunnerSession(level) { Position = new Vector2(5.3f, 1), Tier = 1 }; large.Begin(); large.Tick(.1f);
            Assert.That(large.At(6, 1), Is.EqualTo(TileKind.Empty));
            Assert.That(level.At(6, 1), Is.EqualTo(TileKind.Crate), "Play must not mutate authoring data.");
        }

        [Test]
        public void HeldJumpGoesHigherThanTapAndBothLand()
        {
            float Peak(bool hold)
            {
                var game = new RunnerSession(RunnerLevel.Blank()); game.Begin(); game.JumpPressed();
                if (!hold) game.JumpReleased();
                float peak = game.Position.y;
                for (int i = 0; i < 180; i++) { game.Tick(RunnerSession.Step); peak = Mathf.Max(peak, game.Position.y); }
                Assert.That(game.Grounded, Is.True); Assert.That(game.Position.y, Is.EqualTo(1).Within(.001f));
                return peak;
            }
            Assert.That(Peak(true) - Peak(false), Is.GreaterThan(.8f));
        }

        [Test]
        public void DamageShrinksOnceDuringProtectionThenKillsAtSmallestSize()
        {
            var game = new RunnerSession(RunnerLevel.Blank()) { Tier = 1 }; game.Begin();
            game.Damage(); game.Damage();
            Assert.That(game.Tier, Is.Zero); Assert.That(game.State, Is.EqualTo(RunState.Running));
            for (int i = 0; i < 180; i++) game.Tick(RunnerSession.Step);
            game.Damage(); Assert.That(game.State, Is.EqualTo(RunState.Dead));
        }

        [Test]
        public void PausedSessionFreezesSimulationAndResumes()
        {
            var game = new RunnerSession(RunnerLevel.Blank()); game.Begin(); game.Tick(.1f); game.TogglePause();
            var before = game.Position; float elapsed = game.Elapsed;
            game.Tick(1);
            Assert.That(game.Position, Is.EqualTo(before)); Assert.That(game.Elapsed, Is.EqualTo(elapsed));
            game.TogglePause(); game.Tick(.1f); Assert.That(game.Position.x, Is.GreaterThan(before.x));
        }

        [Test]
        public void PitEndsAttemptAndNewSessionRestoresEverything()
        {
            var level = RunnerLevel.Blank(); level.Set(3, 1, TileKind.Seed);
            for (int x = 5; x < 15; x++) level.Set(x, 0, TileKind.Empty);
            var game = new RunnerSession(level); game.Begin();
            for (int i = 0; i < 600; i++) game.Tick(RunnerSession.Step);
            Assert.That(game.State, Is.EqualTo(RunState.Dead)); Assert.That(game.Seeds, Is.EqualTo(1));
            var retry = new RunnerSession(game.Level);
            Assert.That(retry.State, Is.EqualTo(RunState.Ready)); Assert.That(retry.Tier, Is.Zero);
            Assert.That(retry.At(3, 1), Is.EqualTo(TileKind.Seed));
        }

        [TestCase(0)] [TestCase(2)]
        public void GrowthTutorialAndSizeCorridorAreCompletable(int index)
        {
            var game = new RunnerSession(RunnerLevel.BuiltIn(index)); game.Begin();
            for (int i = 0; i < 6000 && game.State == RunState.Running; i++) game.Tick(RunnerSession.Step);
            Assert.That(game.State, Is.EqualTo(RunState.Cleared), "Stopped at " + game.Position + " tier " + game.Tier);
        }

        [Test]
        public void GapStageIsCompletableWithTwoHeldJumps()
        {
            var game = new RunnerSession(RunnerLevel.BuiltIn(1)); game.Begin();
            int jump = 0;
            for (int i = 0; i < 6000 && game.State == RunState.Running; i++)
            {
                if (jump == 0 && game.Position.x >= 18.25f || jump == 1 && game.Position.x >= 36.25f)
                { game.JumpPressed(); jump++; }
                game.Tick(RunnerSession.Step);
            }
            Assert.That(game.State, Is.EqualTo(RunState.Cleared), "Stopped at " + game.Position + " tier " + game.Tier);
        }

        [Test]
        public void FixedStepProducesEquivalentResultsAtDifferentFrameRates()
        {
            var a = new RunnerSession(RunnerLevel.BuiltIn(0)); var b = new RunnerSession(RunnerLevel.BuiltIn(0)); a.Begin(); b.Begin();
            for (int i = 0; i < 240; i++) a.Tick(1f / 120);
            for (int i = 0; i < 60; i++) b.Tick(1f / 30);
            Assert.That(a.Position.x, Is.EqualTo(b.Position.x).Within(.06f)); Assert.That(a.Tier, Is.EqualTo(b.Tier));
        }

        [Test]
        public void ImportRoundTripPreservesDraftAndRejectsInvalidData()
        {
            var level = RunnerLevel.BuiltIn(2);
            Assert.That(RunnerLevel.TryParse(level.ToJson(), out var copy, out var error), Is.True, error);
            Assert.That(copy.ToJson(), Is.EqualTo(level.ToJson()));
            Assert.That(RunnerLevel.TryParse("not json", out _, out _), Is.False);
            Assert.That(RunnerLevel.TryParse("{}", out _, out _), Is.False);
            level.version = 77; Assert.That(RunnerLevel.TryParse(level.ToJson(), out _, out _), Is.False);
        }

        [Test]
        public void ValidationRejectsDuplicatesUnsupportedMarkersAndOversizedWorlds()
        {
            var level = RunnerLevel.Blank(); level.cells.Add(new LevelCell(0, 0, TileKind.Seed));
            Assert.That(level.Validate(out _), Is.False);
            level = RunnerLevel.Blank(); level.Set(level.spawnX, 0, TileKind.Empty); Assert.That(level.Validate(out _), Is.False);
            level = RunnerLevel.Blank(); level.Set(level.goalX, 2, TileKind.Ground); Assert.That(level.Validate(out _), Is.False);
            level = RunnerLevel.Blank(); level.width = 161; Assert.That(level.Validate(out _), Is.False);
        }

        [Test]
        public void SaveLoadRetainsPreviousDraftAsBackup()
        {
            string directory = Path.Combine(Path.GetTempPath(), "SproutRunTests-" + Guid.NewGuid());
            string path = Path.Combine(directory, "draft.json");
            try
            {
                var first = RunnerLevel.BuiltIn(0); var second = RunnerLevel.BuiltIn(1);
                Assert.That(RunnerStorage.Save(first, path, out var error), Is.True, error);
                Assert.That(RunnerStorage.Save(second, path, out error), Is.True, error);
                Assert.That(RunnerStorage.Load(path, out var loaded, out error), Is.True, error);
                Assert.That(loaded.ToJson(), Is.EqualTo(second.ToJson()));
                Assert.That(RunnerStorage.Load(path + ".bak", out loaded, out error), Is.True, error);
                Assert.That(loaded.ToJson(), Is.EqualTo(first.ToJson()));
            }
            finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        }
    }
}
