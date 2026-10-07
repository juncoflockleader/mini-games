using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MiniGames.Breakout
{
    public sealed class BreakoutController : MonoBehaviour
    {
        const string BestKey = "MiniGames.Breakout.Best", MuteKey = "MiniGames.Audio.Muted";
        public BreakoutSession Session { get; private set; }
        ArcadeUi ui;
        RectTransform board, overlay;
        Image ball, paddle;
        Text score, best, lives, level, combo, remaining, status, overlayTitle, overlayText, primaryText, muteText;
        Button primary, pause;
        readonly List<Image> brickViews = new List<Image>();
        readonly List<Particle> particles = new List<Particle>();
        readonly Image[] trail = new Image[9];
        readonly Vector2[] trailPositions = new Vector2[9];
        AudioSource audioSource;
        AudioClip brickTone, paddleTone, wallTone, lostTone;
        InputActionMap shortcuts;
        RoundState shownState = (RoundState)(-1);
        Vector2 lastPointer;
        int bestScore, shownLevel;
        bool muted;
        float trailClock;

        sealed class Particle
        {
            public Image View;
            public Vector2 Position, Velocity;
            public Color Color;
            public float Life;
        }

        void Start()
        {
            Application.targetFrameRate = 120;
            bestScore = PlayerPrefs.GetInt(BestKey, 0);
            muted = PlayerPrefs.GetInt(MuteKey, 0) != 0;
            Session = new BreakoutSession();
            Session.Hit += OnHit;
            ui = ArcadeUi.Create(transform);
            var root = ui.Root;
            ui.Header("THE COLLECTION  /  01");
            ui.Button(root, "<  ALL GAMES", 1168, 34, 208, 46, ReturnToLauncher);
            ui.Label(root, "01  /  THE CLASSICS", 88, 139, 450, 22, 12, ArcadeUi.Mint, true);
            ui.Label(root, "BREAKOUT", 84, 168, 620, 70, 52, ArcadeUi.White, true);
            ui.Label(root, "Find your angle. Make every bounce count.", 520, 191, 610, 38, 18, ArcadeUi.Muted);
            var mute = ui.Button(root, "", 1180, 182, 172, 42, ToggleSound);
            muteText = mute.GetComponentInChildren<Text>();
            UpdateSoundLabel();
            board = ui.Box(root, "Playfield", 88, 258, BreakoutSession.Width, BreakoutSession.Height, ArcadeUi.Panel).rectTransform;
            for (int x = 28; x < 910; x += 36)
            for (int y = 25; y < 535; y += 36)
                ui.Box(board, "Grid dot", x, y, 2, 2, ArcadeUi.Hex("293440"), false);
            ui.Box(board, "Left rail", 0, 16, 2, 472, ArcadeUi.Line, false);
            ui.Box(board, "Right rail", 918, 16, 2, 472, ArcadeUi.Line, false);
            ui.Box(board, "Drain", 24, 540, 872, 1, ArcadeUi.Hex("4C3B3B"), false);
            ui.Label(board, "KEEP THE BALL IN PLAY", 270, 403, 380, 24, 11, ArcadeUi.Hex("43515F"), false, TextAnchor.MiddleCenter);
            BuildBricks();
            for (int i = 0; i < trail.Length; i++)
                trail[i] = ui.Box(board, "Ball trail", 0, 0, 10 - i * .7f, 10 - i * .7f, new Color(.72f, .95f, .6f, .28f - i * .025f));
            paddle = ui.Box(board, "Paddle", 0, 0, BreakoutSession.PaddleWidth, BreakoutSession.PaddleHeight, ArcadeUi.Mint);
            ui.Box(paddle.transform, "Paddle highlight", 9, 2, 98, 2, new Color(1, 1, 1, .45f));
            ball = ui.Box(board, "Ball", 0, 0, 14, 14, ArcadeUi.White);
            for (int i = 0; i < 72; i++)
            {
                var p = new Particle { View = ui.Box(board, "Spark", 0, 0, 4, 4, ArcadeUi.White, false) };
                p.View.enabled = false;
                particles.Add(p);
            }

            var side = ui.Box(root, "Session panel", 1040, 258, 312, 550, ArcadeUi.Panel).transform;
            ui.Label(side, "YOUR SCORE", 28, 27, 250, 22, 12, ArcadeUi.Muted, true);
            score = ui.Label(side, "00000", 24, 56, 272, 70, 52, ArcadeUi.White, true);
            best = ui.Label(side, "", 28, 139, 270, 25, 13, ArcadeUi.Muted);
            ui.Box(side, "Divider", 28, 183, 256, 1, ArcadeUi.Line, false);
            ui.Label(side, "LEVEL", 28, 210, 100, 24, 11, ArcadeUi.Muted, true);
            ui.Label(side, "LIVES", 175, 210, 100, 24, 11, ArcadeUi.Muted, true);
            level = ui.Label(side, "01 / 03", 28, 240, 145, 35, 25, ArcadeUi.White, true);
            lives = ui.Label(side, "3", 175, 240, 100, 35, 25, ArcadeUi.Mint, true);
            combo = ui.Label(side, "BUILD YOUR STREAK", 28, 301, 260, 26, 13, ArcadeUi.Mint, true);
            remaining = ui.Label(side, "", 28, 336, 265, 26, 14, ArcadeUi.Muted);
            pause = ui.Button(side, "PAUSE   /   ESC", 28, 389, 256, 48, () => Session.TogglePause());
            ui.Button(side, "RESTART   /   R", 28, 451, 256, 48, Restart);
            status = ui.Label(side, "READY WHEN YOU ARE", 28, 515, 256, 18, 10, ArcadeUi.Muted, false, TextAnchor.MiddleCenter);

            overlay = ui.Box(board, "Round overlay", 210, 258, 500, 205, ArcadeUi.Hex("101720")).rectTransform;
            overlayTitle = ui.Label(overlay, "", 18, 20, 464, 44, 29, ArcadeUi.White, true, TextAnchor.MiddleCenter);
            overlayText = ui.Label(overlay, "", 20, 73, 460, 40, 15, ArcadeUi.Muted, false, TextAnchor.MiddleCenter);
            primary = ui.Button(overlay, "", 120, 131, 260, 50, PrimaryAction, true);
            primaryText = primary.GetComponentInChildren<Text>();
            ui.Label(root, "MOVE  Mouse / A D / Arrow keys     LAUNCH  Space     PAUSE  Esc     SOUND  M", 88, 818, 960, 20, 11, ArcadeUi.Muted);

            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            brickTone = Tone(720, .07f); paddleTone = Tone(390, .06f); wallTone = Tone(260, .04f); lostTone = Tone(130, .22f);
            lastPointer = Mouse.current?.position.ReadValue() ?? Vector2.zero;
            shortcuts = new InputActionMap("Breakout shortcuts");
            shortcuts.AddAction("Pause", InputActionType.Button, "<Keyboard>/escape").performed += _ => Session.TogglePause();
            shortcuts.AddAction("Restart", InputActionType.Button, "<Keyboard>/r").performed += _ => Restart();
            shortcuts.AddAction("Sound", InputActionType.Button, "<Keyboard>/m").performed += _ => ToggleSound();
            shortcuts.AddAction("Continue", InputActionType.Button, "<Keyboard>/space").performed += _ => PrimaryAction();
            shortcuts.Enable();
            RefreshView();
        }

        void Update()
        {
            if (Session == null) return;
            var keyboard = Keyboard.current;
            float axis = 0;
            if (keyboard != null)
            {
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) axis--;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) axis++;
            }
            if (axis != 0) Session.MovePaddle(Session.PaddleX + axis * 760 * Time.unscaledDeltaTime);
            if (Mouse.current != null)
            {
                Vector2 pointer = Mouse.current.position.ReadValue();
                bool inside = RectTransformUtility.RectangleContainsScreenPoint(board, pointer);
                if (axis == 0 && inside && Vector2.Distance(pointer, lastPointer) > .1f &&
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(board, pointer, null, out var local))
                    Session.MovePaddle(local.x);
                lastPointer = pointer;
                if (inside && Mouse.current.leftButton.wasPressedThisFrame && Session.State == RoundState.Ready)
                    Session.Launch();
            }
            Session.Tick(Time.unscaledDeltaTime);
            if (Session.Score > bestScore) bestScore = Session.Score;
            RefreshView();
            AnimateEffects(Time.unscaledDeltaTime);
        }

        void BuildBricks()
        {
            foreach (var view in brickViews) Destroy(view.gameObject);
            brickViews.Clear();
            foreach (var brick in Session.Bricks)
            {
                Rect b = brick.Bounds;
                var view = ui.Box(board, "Brick", b.x, b.y, b.width, b.height, ArcadeUi.BrickColors[brick.Row]);
                ui.Box(view.transform, "Highlight", 6, 3, b.width - 12, 2, new Color(1, 1, 1, .22f), false);
                brickViews.Add(view);
            }
            shownLevel = Session.Level;
        }

        void RefreshView()
        {
            if (shownLevel != Session.Level || brickViews.Count != Session.Bricks.Count) BuildBricks();
            for (int i = 0; i < brickViews.Count; i++) brickViews[i].gameObject.SetActive(Session.Bricks[i].Alive);
            ArcadeUi.Place(paddle.rectTransform, Session.PaddleX - BreakoutSession.PaddleWidth / 2, BreakoutSession.PaddleY, BreakoutSession.PaddleWidth, BreakoutSession.PaddleHeight);
            ArcadeUi.Place(ball.rectTransform, Session.Ball.x - 7, Session.Ball.y - 7, 14, 14);
            ball.enabled = Session.State != RoundState.GameOver;
            score.text = Session.Score.ToString("D5");
            best.text = "PERSONAL BEST    " + bestScore.ToString("D5");
            lives.text = Session.Lives + " / 3";
            level.text = Session.Level.ToString("D2") + " / 03";
            remaining.text = Session.Remaining + " bricks to go";
            combo.text = Session.Combo > 1 ? "STREAK  x" + Mathf.Min(Session.Combo, 5) : "BUILD YOUR STREAK";
            if (shownState == Session.State) return;
            shownState = Session.State;
            bool show = Session.State != RoundState.Playing;
            overlay.gameObject.SetActive(show);
            pause.interactable = Session.State == RoundState.Ready || Session.State == RoundState.Playing || Session.State == RoundState.Paused;
            string title = "", detail = "", action = "";
            switch (Session.State)
            {
                case RoundState.Ready: title = Session.Lives == 3 ? "LET'S BREAK SOME BRICKS." : "STILL IN THE GAME."; detail = "Move your paddle, then press Space to serve."; action = "LAUNCH BALL"; break;
                case RoundState.Paused: title = "TAKE A BREATHER."; detail = "Your next bounce can wait."; action = "RESUME"; break;
                case RoundState.LevelCleared: title = "A CLEAN SWEEP."; detail = "Level complete. A new pattern is waiting."; action = "NEXT LEVEL"; break;
                case RoundState.Won: title = "YOU BROKE THEM ALL."; detail = "Three levels. One very satisfying finish."; action = "PLAY AGAIN"; break;
                case RoundState.GameOver: title = "ONE MORE ROUND?"; detail = "Every good streak starts with another try."; action = "TRY AGAIN"; break;
            }
            overlayTitle.text = title; overlayText.text = detail; primaryText.text = action;
            status.text = Session.State == RoundState.Playing ? "IN PLAY  /  FIND YOUR FLOW" : Session.State.ToString().ToUpperInvariant();
            EventSystem.current.SetSelectedGameObject(show ? primary.gameObject : null);
            for (int i = 0; i < trailPositions.Length; i++) trailPositions[i] = Session.Ball;
            SaveBest();
        }

        public void PrimaryAction()
        {
            switch (Session.State)
            {
                case RoundState.Ready: Session.Launch(); break;
                case RoundState.Paused: Session.TogglePause(); break;
                case RoundState.LevelCleared: Session.NextLevel(); break;
                case RoundState.Won:
                case RoundState.GameOver: Restart(); break;
            }
        }

        public void Restart()
        {
            SaveBest();
            Session.Restart();
            shownLevel = 0;
            shownState = (RoundState)(-1);
            foreach (var p in particles) { p.Life = 0; p.View.enabled = false; }
        }

        void OnHit(Vector2 position, HitKind kind, int row)
        {
            if (!muted)
                audioSource.PlayOneShot(kind == HitKind.Brick ? brickTone : kind == HitKind.Paddle ? paddleTone : kind == HitKind.Lost ? lostTone : wallTone, .22f);
            if (kind != HitKind.Brick && kind != HitKind.Paddle) return;
            int count = 0;
            foreach (var p in particles)
            {
                if (p.Life > 0) continue;
                p.Position = position;
                p.Velocity = Random.insideUnitCircle * 130 + Vector2.up * 25;
                p.Color = kind == HitKind.Brick ? ArcadeUi.BrickColors[row] : ArcadeUi.Mint;
                p.Life = .45f;
                p.View.enabled = true;
                if (++count == 9) break;
            }
        }

        void AnimateEffects(float dt)
        {
            bool playing = Session.State == RoundState.Playing;
            trailClock += dt;
            if (playing && trailClock >= .012f)
            {
                trailClock = 0;
                for (int i = trailPositions.Length - 1; i > 0; i--) trailPositions[i] = trailPositions[i - 1];
                trailPositions[0] = Session.Ball;
            }
            for (int i = 0; i < trail.Length; i++)
            {
                trail[i].enabled = playing;
                float size = 10 - i * .7f;
                ArcadeUi.Place(trail[i].rectTransform, trailPositions[i].x - size / 2, trailPositions[i].y - size / 2, size, size);
            }
            if (Session.State == RoundState.Paused) return;
            foreach (var p in particles)
            {
                if (p.Life <= 0) continue;
                p.Life -= dt;
                p.Position += p.Velocity * dt;
                p.Velocity += Vector2.up * 180 * dt;
                var color = p.Color; color.a = Mathf.Clamp01(p.Life / .45f); p.View.color = color;
                p.View.enabled = p.Life > 0 && p.Position.x > 0 && p.Position.x < BreakoutSession.Width && p.Position.y > 0 && p.Position.y < BreakoutSession.Height;
                ArcadeUi.Place(p.View.rectTransform, p.Position.x, p.Position.y, 4, 4);
            }
        }

        void ToggleSound()
        {
            muted = !muted;
            PlayerPrefs.SetInt(MuteKey, muted ? 1 : 0);
            PlayerPrefs.Save();
            UpdateSoundLabel();
        }
        void UpdateSoundLabel() => muteText.text = muted ? "SOUND OFF  /  M" : "SOUND ON  /  M";
        void SaveBest()
        {
            if (Session != null) bestScore = Mathf.Max(bestScore, Session.Score);
            if (bestScore <= PlayerPrefs.GetInt(BestKey, 0)) return;
            PlayerPrefs.SetInt(BestKey, bestScore);
            PlayerPrefs.Save();
        }
        void ReturnToLauncher() { SaveBest(); SceneManager.LoadScene(GameCatalog.LauncherScene); }
        void OnApplicationFocus(bool focus) { if (!focus && Session?.State == RoundState.Playing) Session.TogglePause(); }
        void OnApplicationQuit() => SaveBest();
        void OnDestroy()
        {
            shortcuts?.Dispose();
            SaveBest();
            if (Session != null) Session.Hit -= OnHit;
            foreach (var clip in new[] { brickTone, paddleTone, wallTone, lostTone }) if (clip != null) Destroy(clip);
        }
        static AudioClip Tone(float frequency, float duration)
        {
            const int rate = 22050;
            var data = new float[Mathf.CeilToInt(rate * duration)];
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)rate;
                data[i] = Mathf.Sin(t * frequency * Mathf.PI * 2) * Mathf.Min(t * 250, 1) * Mathf.Pow(1 - t / duration, 2);
            }
            var clip = AudioClip.Create("Arcade tone", data.Length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
