using MiniGames.Breakout;
using NUnit.Framework;
using UnityEngine;

namespace MiniGames.Tests
{
    public sealed class BreakoutSessionTests
    {
        [Test]
        public void NewRoundHasFiftyBricksThreeLivesAndAttachedBall()
        {
            var game = new BreakoutSession();
            Assert.That(game.Remaining, Is.EqualTo(50));
            Assert.That(game.Lives, Is.EqualTo(3));
            Assert.That(game.State, Is.EqualTo(RoundState.Ready));
            game.MovePaddle(300);
            Assert.That(game.Ball.x, Is.EqualTo(300));
        }

        [TestCase(-200, 64)]
        [TestCase(2000, 856)]
        public void PaddleStaysInsideArena(float input, float expected)
        {
            var game = new BreakoutSession(); game.MovePaddle(input);
            Assert.That(game.PaddleX, Is.EqualTo(expected));
        }

        [Test]
        public void PauseFreezesBallAndPaddleThenResumes()
        {
            var game = new BreakoutSession(); game.Launch(); game.Tick(.05f); game.TogglePause();
            Vector2 before = game.Ball; float paddle = game.PaddleX;
            game.Tick(.1f); game.MovePaddle(100);
            Assert.That(game.Ball, Is.EqualTo(before)); Assert.That(game.PaddleX, Is.EqualTo(paddle));
            game.TogglePause(); game.Tick(.05f);
            Assert.That(game.Ball, Is.Not.EqualTo(before));
        }

        [Test]
        public void PausingBeforeServeReturnsToReady()
        {
            var game = new BreakoutSession(); game.TogglePause(); game.TogglePause();
            Assert.That(game.State, Is.EqualTo(RoundState.Ready));
            Assert.That(game.Velocity, Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void FastBallHitsBrickWithoutTunnelingAndAwardsScoreOnce()
        {
            var game = new BreakoutSession(); game.Launch();
            game.Ball = new Vector2(70, 260); game.Velocity = new Vector2(0, -1800);
            game.Tick(.04f);
            Assert.That(game.Remaining, Is.EqualTo(49));
            Assert.That(game.Score, Is.EqualTo(10));
            Assert.That(game.Velocity.y, Is.GreaterThan(0));
            game.Tick(.01f); Assert.That(game.Score, Is.EqualTo(10));
        }

        [Test]
        public void FastDownwardBallBouncesOffPaddle()
        {
            var game = new BreakoutSession(); game.Launch();
            game.Ball = new Vector2(game.PaddleX + 35, 460); game.Velocity = new Vector2(0, 1800);
            game.Tick(.04f);
            Assert.That(game.Velocity.y, Is.LessThan(0));
            Assert.That(game.Velocity.x, Is.GreaterThan(0));
            Assert.That(game.Lives, Is.EqualTo(3));
        }

        [Test]
        public void SideWallReflectsAndSpendsRemainingFrameTime()
        {
            var game = new BreakoutSession(); game.Launch();
            game.Ball = new Vector2(12, 320); game.Velocity = new Vector2(-500, 0);
            game.Tick(.04f);
            Assert.That(game.Velocity.x, Is.GreaterThan(0));
            Assert.That(game.Ball.x, Is.GreaterThan(20));
        }

        [Test]
        public void MissingPaddleConsumesOneLifeAndRequiresNewServe()
        {
            var game = new BreakoutSession(); game.Launch();
            game.Ball = new Vector2(50, 549); game.Velocity = new Vector2(0, 400); game.Tick(.05f);
            Assert.That(game.Lives, Is.EqualTo(2));
            Assert.That(game.State, Is.EqualTo(RoundState.Ready));
            game.Tick(1); Assert.That(game.Lives, Is.EqualTo(2));
        }

        [Test]
        public void ThirdMissEndsRoundAndRestartResetsEverything()
        {
            var game = new BreakoutSession();
            for (int i = 0; i < 3; i++)
            {
                game.Launch(); game.Ball = new Vector2(50, 560); game.Velocity = new Vector2(0, 300); game.Tick(.01f);
            }
            Assert.That(game.State, Is.EqualTo(RoundState.GameOver));
            Assert.That(game.Lives, Is.Zero);
            game.Restart();
            Assert.That(game.Lives, Is.EqualTo(3)); Assert.That(game.Score, Is.Zero);
            Assert.That(game.Remaining, Is.EqualTo(50)); Assert.That(game.State, Is.EqualTo(RoundState.Ready));
        }

        [Test]
        public void ClearingAllThreePatternsEndsInVictory()
        {
            var game = new BreakoutSession();
            for (int level = 1; level <= 3; level++)
            {
                game.Launch();
                foreach (var brick in game.Bricks)
                {
                    if (!brick.Alive) continue;
                    game.Ball = new Vector2(brick.Bounds.center.x, brick.Bounds.yMax + BreakoutSession.Radius + 2);
                    game.Velocity = new Vector2(0, -400);
                    game.Tick(.01f);
                }
                Assert.That(game.Remaining, Is.Zero);
                Assert.That(game.State, Is.EqualTo(level == 3 ? RoundState.Won : RoundState.LevelCleared));
                if (level < 3) game.NextLevel();
            }
            Assert.That(game.Score, Is.GreaterThan(0));
            Assert.That(game.Lives, Is.EqualTo(3));
        }

        [Test]
        public void ParallelRayOutsideBrickDoesNotCollide()
        {
            bool hit = BreakoutSession.Sweep(new Vector2(0, 100), new Vector2(0, -500), new Rect(10, 10, 50, 20), 1, out _, out _);
            Assert.That(hit, Is.False);
        }
    }
}
