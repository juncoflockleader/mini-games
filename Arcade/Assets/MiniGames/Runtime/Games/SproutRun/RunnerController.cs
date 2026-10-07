using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MiniGames.SproutRun
{
    public sealed class RunnerController : MonoBehaviour
    {
        const float Tile = 40, ViewWidth = 1312, ViewHeight = 480;
        const int Columns = 35, Rows = 12;
        const string MuteKey = "MiniGames.Audio.Muted";
        static readonly Color GroundColor = ArcadeUi.Hex("254B47"), Gold = ArcadeUi.Hex("F3C86A"), Blue = ArcadeUi.Hex("8CC9E7");
        static readonly string[] Tools = { "Ground", "Crate", "Seed +", "Dew -", "Spikes", "Beetle", "Start", "Finish", "Erase" };
        static readonly string[] Hints =
        {
            "Collect gold seeds. Grow bigger. Walk straight through wooden crates.",
            "Hold your jump to cross gaps. Big sprouts can squash beetles.",
            "Blue dew makes you smaller. Two drops will fit you under the low roof."
        };
        public RunnerSession Session { get; private set; }
        public RunnerLevel Draft { get; private set; }
        public bool Editing { get; private set; }
        ArcadeUi ui;
        RectTransform board, tilesRoot, runner, startMarker, finishMarker, overlay, modal;
        GameObject gameControls, editorControls, gameHud, editorPalette;
        Image runnerBody, progress;
        Text stageText, status, sizeText, overlayTitle, overlayDetail, overlayAction, soundText, modeText, controlsHint;
        InputField nameField;
        readonly List<TileView> views = new List<TileView>();
        readonly List<Image> beetleViews = new List<Image>();
        readonly List<Spark> sparks = new List<Spark>();
        readonly List<Button> paletteButtons = new List<Button>();
        readonly List<Image> sizePips = new List<Image>();
        InputActionMap actions;
        AudioSource audioSource;
        AudioClip upTone, downTone, hitTone, winTone;
        Sprite spikesSprite;
        float cameraX, editorCamera, toastUntil, effectClock;
        string toast;
        int stageIndex, selectedTool;
        bool testing, dirty, muted, updatingName;
        bool painting;
        int strokeTool;
        Vector2Int lastPaint = new Vector2Int(-1, -1);
        RunState shownState = (RunState)(-1);

        sealed class TileView
        {
            public Image Body, Accent;
            public Text Glyph;
            public Sprite Rounded;
        }
        sealed class Spark
        {
            public Image View;
            public Vector2 Position, Velocity;
            public float Life;
        }

        void Start()
        {
            Application.targetFrameRate = 120;
            muted = PlayerPrefs.GetInt(MuteKey, 0) != 0;
            ui = ArcadeUi.Create(transform);
            spikesSprite = SpikesSprite();
            BuildUi();
            audioSource = gameObject.AddComponent<AudioSource>(); audioSource.playOnAwake = false;
            upTone = Tone(680, .14f); downTone = Tone(310, .15f); hitTone = Tone(160, .08f); winTone = Tone(940, .35f);
            actions = new InputActionMap("Sprout Run");
            var jump = actions.AddAction("Jump", InputActionType.Button, "<Keyboard>/space");
            jump.AddBinding("<Keyboard>/w"); jump.AddBinding("<Keyboard>/upArrow");
            jump.performed += _ => { if (!Typing && !Editing && modal == null) Primary(); };
            jump.canceled += _ => Session?.JumpReleased();
            var pointer = actions.AddAction("Pointer jump", InputActionType.Button, "<Mouse>/leftButton");
            pointer.performed += context =>
            {
                BeginPaint(false);
                if (!Editing && modal == null && Session.State == RunState.Running && PointerInBoard(out _)) Session.JumpPressed();
            };
            pointer.canceled += _ => { EndPaint(); Session?.JumpReleased(); };
            var erase = actions.AddAction("Erase", InputActionType.Button, "<Mouse>/rightButton");
            erase.performed += _ => BeginPaint(true);
            erase.canceled += _ => EndPaint();
            actions.AddAction("Pause", InputActionType.Button, "<Keyboard>/escape").performed += _ =>
            { if (modal != null) CloseModal(); else if (!Typing && !Editing) Session.TogglePause(); };
            actions.AddAction("Retry", InputActionType.Button, "<Keyboard>/r").performed += _ => { if (!Typing && !Editing && modal == null) Restart(); };
            actions.AddAction("Editor", InputActionType.Button, "<Keyboard>/e").performed += _ => { if (!Typing && modal == null) ToggleEditor(); };
            actions.AddAction("Mute", InputActionType.Button, "<Keyboard>/m").performed += _ => { if (!Typing && modal == null) ToggleSound(); };
            actions.Enable();
            StartSession(RunnerLevel.BuiltIn(0));
        }

        bool Typing => EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null &&
            EventSystem.current.currentSelectedGameObject.GetComponent<InputField>() != null;

        void BuildUi()
        {
            var root = ui.Root;
            ui.Header("THE COLLECTION  /  02");
            ui.Button(root, "<  ALL GAMES", 1168, 34, 208, 46, Leave);
            ui.Label(root, "02  /  GROW INTO IT", 64, 129, 450, 22, 12, ArcadeUi.Mint, true);
            ui.Label(root, "SPROUT RUN", 60, 155, 495, 65, 45, ArcadeUi.White, true);
            modeText = ui.Label(root, "RUN. GROW. MAKE ROOM.", 64, 224, 400, 24, 12, ArcadeUi.Muted, true);

            var gc = ui.Group(root, "Play controls", 565, 149, 811, 110); gameControls = gc.gameObject;
            ui.Button(gc, "<", 0, 0, 40, 43, () => ChangeStage(-1));
            stageText = ui.Label(gc, "", 48, 0, 224, 43, 16, ArcadeUi.White, true, TextAnchor.MiddleCenter);
            ui.Button(gc, ">", 280, 0, 40, 43, () => ChangeStage(1));
            ui.Button(gc, "RETRY / R", 334, 0, 132, 43, Restart);
            ui.Button(gc, "PAUSE / ESC", 478, 0, 147, 43, () => Session.TogglePause());
            ui.Button(gc, "EDITOR / E", 637, 0, 174, 43, ToggleEditor, true);
            var hud = ui.Group(gc, "Run HUD", 0, 57, 811, 55); gameHud = hud.gameObject;
            sizeText = ui.Label(hud, "SIZE 01", 0, 10, 105, 28, 15, ArcadeUi.Mint, true);
            for (int i = 0; i < 4; i++) sizePips.Add(ui.Box(hud, "Size " + (i + 1), 106 + i * 40, 21 - i * 5, 28, 10 + i * 5, ArcadeUi.Line));
            ui.Label(hud, "SEED = GROW     DEW = SHRINK", 300, 11, 330, 23, 12, ArcadeUi.Muted);
            soundText = ui.Button(hud, "", 637, 0, 174, 40, ToggleSound).GetComponentInChildren<Text>();
            UpdateSoundText();

            var ec = ui.Group(root, "Editor controls", 560, 150, 816, 51); editorControls = ec.gameObject;
            nameField = Field(ec, 0, 0, 226, 43, "Level name", false);
            nameField.characterLimit = 40;
            nameField.onValueChanged.AddListener(value => { if (!updatingName && Draft != null) { Draft.name = value; dirty = true; } });
            ui.Button(ec, "NEW", 238, 0, 72, 43, () => ReplaceDraft(() => SetDraft(RunnerLevel.Blank(), true)));
            ui.Button(ec, "LOAD", 320, 0, 76, 43, () => ReplaceDraft(LoadDraft));
            ui.Button(ec, "SAVE", 406, 0, 76, 43, SaveDraft);
            ui.Button(ec, "JSON", 492, 0, 76, 43, JsonDialog);
            ui.Button(ec, "TEST  /  E", 580, 0, 128, 43, TestDraft, true);
            ui.Button(ec, "PLAY", 720, 0, 96, 43, ExitEditor);
            var palette = ui.Group(root, "Paint palette", 472, 218, 904, 42); editorPalette = palette.gameObject;
            for (int i = 0; i < Tools.Length; i++)
            {
                int tool = i;
                paletteButtons.Add(ui.Button(palette, Tools[i], i * 101, 0, 95, 39, () => SelectTool(tool)));
            }

            board = ui.Box(root, "World viewport", 64, 282, ViewWidth, ViewHeight, ArcadeUi.Hex("14282E"), false).rectTransform;
            board.gameObject.AddComponent<RectMask2D>();
            for (int i = 0; i < 12; i++)
            {
                float x = i * 127 + 20;
                ui.Box(board, "Distant stems", x, 280 - i % 3 * 46, 3, 210, ArcadeUi.Hex("1B3638"), false);
                var leaf = ui.Box(board, "Distant leaf", x - 24, 276 - i % 3 * 46, 28, 13, ArcadeUi.Hex("1E3A3A"));
                leaf.rectTransform.localEulerAngles = new Vector3(0, 0, -28);
                ui.Box(board, "Firefly", x + 48, 53 + i % 4 * 30, 3, 3, ArcadeUi.Hex("476458"));
            }
            ui.Box(board, "Moon", 1125, 40, 60, 60, ArcadeUi.Hex("33504B"));
            tilesRoot = ui.Group(board, "Tiles", 0, 0, ViewWidth, ViewHeight);
            for (int i = 0; i < Columns * Rows; i++)
            {
                var body = ui.Box(tilesRoot, "Cell", 0, 0, Tile - 2, Tile - 2, GroundColor);
                var accent = ui.Box(body.transform, "Tile accent", 2, 0, Tile - 6, 4, ArcadeUi.Mint, false);
                var glyph = ui.Label(body.transform, "", 0, 0, Tile - 2, Tile - 2, 23, ArcadeUi.White, true, TextAnchor.MiddleCenter);
                views.Add(new TileView { Body = body, Accent = accent, Glyph = glyph, Rounded = body.sprite });
            }
            startMarker = Marker("START", Blue);
            finishMarker = Marker("FINISH", Gold);
            runnerBody = ui.Box(board, "Sprout", 0, 0, 26, 36, ArcadeUi.Mint);
            runner = runnerBody.rectTransform;
            ui.Box(runner, "Eye left", 7, 9, 3, 5, ArcadeUi.Background);
            ui.Box(runner, "Eye right", 17, 9, 3, 5, ArcadeUi.Background);
            ui.Box(runner, "Cheek", 18, 18, 5, 2, ArcadeUi.Hex("7DA853"));
            var leafLeft = ui.Box(runner, "Leaf left", 3, -5, 11, 5, ArcadeUi.Hex("D4FCA7"));
            leafLeft.rectTransform.localEulerAngles = new Vector3(0, 0, -25);
            var leafRight = ui.Box(runner, "Leaf right", 13, -8, 11, 5, ArcadeUi.Hex("D4FCA7"));
            leafRight.rectTransform.localEulerAngles = new Vector3(0, 0, 25);
            for (int i = 0; i < 40; i++)
            {
                var view = ui.Box(board, "Seed dust", 0, 0, 5, 5, Gold);
                view.gameObject.SetActive(false); sparks.Add(new Spark { View = view });
            }
            progress = ui.Box(board, "Progress", 0, ViewHeight - 3, 0, 3, ArcadeUi.Mint, false);
            overlay = ui.Box(board, "Run message", 374, 106, 564, 250, ArcadeUi.Background).rectTransform;
            overlay.GetComponent<Image>().raycastTarget = true;
            overlayTitle = ui.Label(overlay, "", 25, 26, 514, 45, 32, ArcadeUi.White, true, TextAnchor.MiddleCenter);
            overlayDetail = ui.Label(overlay, "", 37, 85, 490, 65, 17, ArcadeUi.Muted, false, TextAnchor.UpperCenter);
            overlayAction = ui.Button(overlay, "", 142, 173, 280, 51, Primary, true).GetComponentInChildren<Text>();
            status = ui.Label(root, "", 64, 778, 1312, 28, 16, ArcadeUi.Mint);
            controlsHint = ui.Label(root, "", 64, 814, 1312, 20, 11, ArcadeUi.Muted);
        }

        RectTransform Marker(string label, Color color)
        {
            var marker = ui.Group(board, label, 0, 0, 40, 100);
            ui.Box(marker, "Post", 17, 24, 4, 76, color, false);
            ui.Box(marker, "Flag", 21, 24, 28, 19, color);
            ui.Label(marker, label, -12, 0, 90, 22, 10, color, true);
            return marker;
        }

        void Update()
        {
            if (Session == null) return;
            float dt = Time.unscaledDeltaTime;
            if (Editing && modal == null && !Typing) EditorInput(dt);
            else if (!Editing && modal == null) Session.Tick(dt);
            if (!Editing) cameraX = Mathf.Clamp(Session.Position.x - 8, 0, Mathf.Max(0, Session.Level.width - ViewWidth / Tile));
            Render();
            if (!Editing && Session.State != RunState.Paused) UpdateSparks(dt);
        }

        public void StartSession(RunnerLevel level)
        {
            if (Session != null) Session.Happened -= OnEvent;
            Session = new RunnerSession(level); Session.Happened += OnEvent;
            cameraX = Mathf.Clamp(Session.Position.x - 8, 0, level.width - ViewWidth / Tile);
            shownState = (RunState)(-1); toastUntil = 0;
            foreach (var spark in sparks) { spark.Life = 0; spark.View.gameObject.SetActive(false); }
            Render();
        }

        public void Primary()
        {
            if (Editing || modal != null) return;
            EventSystem.current.SetSelectedGameObject(null);
            switch (Session.State)
            {
                case RunState.Ready: Session.Begin(); break;
                case RunState.Running: Session.JumpPressed(); break;
                case RunState.Paused: Session.TogglePause(); break;
                case RunState.Dead: Restart(); Session.Begin(); break;
                case RunState.Cleared:
                    if (testing) ToggleEditor();
                    else { stageIndex = (stageIndex + 1) % 3; StartSession(RunnerLevel.BuiltIn(stageIndex)); }
                    break;
            }
        }

        public void Restart() { if (Session != null && !Editing) StartSession(Session.Level); }
        void ChangeStage(int delta)
        {
            if (testing) { ToggleEditor(); return; }
            stageIndex = (stageIndex + delta + 3) % 3; StartSession(RunnerLevel.BuiltIn(stageIndex));
        }

        public void ToggleEditor()
        {
            if (Editing) { TestDraft(); return; }
            if (Draft == null)
            {
                if (File.Exists(RunnerStorage.DraftPath) && RunnerStorage.Load(RunnerStorage.DraftPath, out var saved, out _)) SetDraft(saved, false);
                else SetDraft(Session.Level.Copy(), true);
            }
            Editing = true; cameraX = editorCamera;
            foreach (var spark in sparks) spark.View.gameObject.SetActive(false);
            Session.JumpReleased(); SelectTool(selectedTool); Render();
            EventSystem.current.SetSelectedGameObject(null);
        }

        public void TestDraft()
        {
            if (!Draft.Validate(out var error)) { Toast(error); return; }
            editorCamera = cameraX; Editing = false; testing = true;
            StartSession(Draft); EventSystem.current.SetSelectedGameObject(null);
        }

        void ExitEditor()
        {
            editorCamera = cameraX; Editing = false; testing = false;
            StartSession(RunnerLevel.BuiltIn(stageIndex)); EventSystem.current.SetSelectedGameObject(null);
        }

        void SetDraft(RunnerLevel level, bool modified)
        {
            Draft = level; dirty = modified; editorCamera = cameraX = 0;
            updatingName = true; nameField.text = level.name; updatingName = false;
        }

        void EditorInput(float dt)
        {
            var keys = Keyboard.current;
            float direction = keys == null ? 0 : ((keys.dKey.isPressed || keys.rightArrowKey.isPressed ? 1 : 0) - (keys.aKey.isPressed || keys.leftArrowKey.isPressed ? 1 : 0));
            cameraX += direction * 20 * dt;
            var mouse = Mouse.current;
            if (PointerInBoard(out var local))
            {
                cameraX -= mouse.scroll.ReadValue().y * .015f;
                cameraX = Mathf.Clamp(cameraX, 0, Draft.width - ViewWidth / Tile);
                if (painting) PaintPoint(local);
            }
            else lastPaint = new Vector2Int(-1, -1);
            cameraX = Mathf.Clamp(cameraX, 0, Draft.width - ViewWidth / Tile);
            editorCamera = cameraX;
        }

        void BeginPaint(bool erase)
        {
            if (!Editing || modal != null || !PointerInBoard(out var local)) return;
            EventSystem.current.SetSelectedGameObject(null);
            painting = true; strokeTool = erase ? 8 : selectedTool; lastPaint = new Vector2Int(-1, -1);
            PaintPoint(local);
        }

        void EndPaint()
        {
            // A click or a complete drag can arrive between rendered frames.
            if (painting && Editing && modal == null && PointerInBoard(out var local)) PaintPoint(local);
            painting = false; lastPaint = new Vector2Int(-1, -1);
        }

        void PaintPoint(Vector2 local)
        {
            var cell = new Vector2Int(Mathf.FloorToInt(local.x / Tile + cameraX), Mathf.FloorToInt((ViewHeight + local.y) / Tile));
            if (cell == lastPaint) return;
            int steps = lastPaint.x < 0 ? 1 : Mathf.Max(Mathf.Abs(cell.x - lastPaint.x), Mathf.Abs(cell.y - lastPaint.y));
            Vector2 from = lastPaint.x < 0 ? cell : lastPaint;
            for (int i = 1; i <= steps; i++)
            {
                Vector2 point = Vector2.Lerp(from, cell, i / (float)steps);
                Paint(Mathf.RoundToInt(point.x), Mathf.RoundToInt(point.y), strokeTool);
            }
            lastPaint = cell;
        }

        public void Paint(int x, int y, int tool)
        {
            if (Draft == null || x < 0 || y < 0 || x >= Draft.width || y >= Draft.height) return;
            if (tool == 6) { Draft.spawnX = x; Draft.spawnY = y; Draft.Set(x, y, TileKind.Empty); }
            else if (tool == 7) { Draft.goalX = x; Draft.goalY = y; Draft.Set(x, y, TileKind.Empty); Draft.Set(x, y + 1, TileKind.Empty); }
            else Draft.Set(x, y, tool == 8 ? TileKind.Empty : (TileKind)(tool + 1));
            dirty = true;
        }

        void SelectTool(int index)
        {
            selectedTool = index;
            for (int i = 0; i < paletteButtons.Count; i++)
            {
                paletteButtons[i].GetComponent<Image>().color = i == index ? ArcadeUi.Mint : ArcadeUi.Line;
                paletteButtons[i].GetComponentInChildren<Text>().color = i == index ? ArcadeUi.Background : ArcadeUi.White;
            }
            EventSystem.current.SetSelectedGameObject(null);
        }

        bool PointerInBoard(out Vector2 local)
        {
            local = Vector2.zero;
            return Mouse.current != null && RectTransformUtility.RectangleContainsScreenPoint(board, Mouse.current.position.ReadValue()) &&
                RectTransformUtility.ScreenPointToLocalPointInRectangle(board, Mouse.current.position.ReadValue(), null, out local);
        }

        void Render()
        {
            if (Session == null) return;
            RunnerLevel level = Editing ? Draft : Session.Level;
            gameControls.SetActive(!Editing); editorControls.SetActive(Editing); editorPalette.SetActive(Editing); gameHud.SetActive(!Editing);
            modeText.text = Editing ? "LEVEL EDITOR  /  " + (dirty ? "UNSAVED CHANGES" : "SAVED DRAFT") : testing ? "PLAYTEST  /  E TO RETURN TO YOUR DRAFT" : "RUN. GROW. MAKE ROOM.";
            controlsHint.text = Editing ? "TEST  E     PAN  A D / Arrows / mouse wheel     SAVE  Keep one local draft     JSON  Import or export a level" :
                "JUMP  Space / W / Up / hold click     RETRY  R     PAUSE  Esc     EDITOR  E     SOUND  M";
            stageText.text = testing ? "YOUR PLAYTEST" : (stageIndex + 1).ToString("D2") + "  /  " + level.name;
            int first = Mathf.FloorToInt(cameraX);
            for (int row = 0; row < Rows; row++)
            for (int col = 0; col < Columns; col++)
            {
                int x = first + col;
                TileKind kind = Editing ? Draft.At(x, row) : Session.At(x, row);
                TileView view = views[row * Columns + col];
                bool visible = x < level.width && (Editing || kind != TileKind.Empty);
                view.Body.gameObject.SetActive(visible);
                if (!visible) continue;
                DrawTile(view, kind, (x - cameraX) * Tile, ViewHeight - (row + 1) * Tile);
            }
            PlaceMarker(startMarker, level.spawnX, level.spawnY); startMarker.gameObject.SetActive(Editing || cameraX < 3);
            PlaceMarker(finishMarker, level.goalX, level.goalY);
            while (beetleViews.Count < Session.Beetles.Count)
            {
                var beetle = ui.Box(board, "Beetle", 0, 0, 30, 24, ArcadeUi.Hex("B4A0CF"));
                ui.Box(beetle.transform, "Eye", 19, 5, 4, 4, ArcadeUi.Background);
                beetle.rectTransform.SetSiblingIndex(runner.GetSiblingIndex());
                beetleViews.Add(beetle);
            }
            for (int i = 0; i < beetleViews.Count; i++)
            {
                bool alive = !Editing && i < Session.Beetles.Count && Session.Beetles[i].Alive;
                beetleViews[i].gameObject.SetActive(alive);
                if (alive)
                {
                    Rect b = Session.Beetles[i].Bounds;
                    ArcadeUi.Place(beetleViews[i].rectTransform, (b.x - cameraX) * Tile, ViewHeight - b.yMax * Tile, b.width * Tile, b.height * Tile);
                }
            }
            runner.gameObject.SetActive(!Editing);
            Rect body = Session.Body;
            ArcadeUi.Place(runner, (body.x - cameraX) * Tile, ViewHeight - body.yMax * Tile, 25.6f, 36);
            runner.localScale = Vector3.one * Session.Size;
            runnerBody.color = Session.Protection > 0 && (int)(Time.unscaledTime * 12) % 2 == 0 ? ArcadeUi.White : ArcadeUi.Mint;
            progress.gameObject.SetActive(!Editing);
            ArcadeUi.Place(progress.rectTransform, 0, ViewHeight - 3, ViewWidth * Session.Progress, 3);
            sizeText.text = "SIZE " + (Session.Tier + 1).ToString("D2");
            for (int i = 0; i < sizePips.Count; i++) sizePips[i].color = i <= Session.Tier ? ArcadeUi.Mint : ArcadeUi.Line;
            overlay.gameObject.SetActive(!Editing && Session.State != RunState.Running);
            status.text = Time.unscaledTime < toastUntil ? toast : Editing ?
                "PAINT  " + Tools[selectedTool] + "   /   Right-click erases   /   A D or scroll to pan   /   Columns " + (first + 1) + "–" + Mathf.Min(first + 33, level.width) + " of " + level.width :
                Session.Blocked ? "A little tight? Jump over solid ground, grow to break crates, or use dew to fit under a roof." : testing ? "Your draft is safe. Press E to return to the editor." : Hints[stageIndex];
            if (shownState == Session.State) return;
            shownState = Session.State;
            switch (Session.State)
            {
                case RunState.Ready: overlayTitle.text = level.name.ToUpperInvariant(); overlayDetail.text = "Run right automatically. Tap for a hop; hold for a higher jump.\nGold seeds make you grow."; overlayAction.text = "LET'S GROW  /  SPACE"; break;
                case RunState.Paused: overlayTitle.text = "ROOM TO BREATHE."; overlayDetail.text = "Your sprout is right where you left it."; overlayAction.text = "RESUME  /  SPACE"; break;
                case RunState.Dead: overlayTitle.text = "ONE MORE LITTLE TRY?"; overlayDetail.text = Session.DeathReason; overlayAction.text = "TRY AGAIN  /  SPACE"; break;
                case RunState.Cleared: overlayTitle.text = "LOOK HOW FAR YOU GREW."; overlayDetail.text = Session.Seeds + " seeds collected  /  " + Session.Elapsed.ToString("0.0") + " seconds\n" + (testing ? "Take your ideas back to the editor." : "A little run. A big finish."); overlayAction.text = testing ? "BACK TO EDITOR" : stageIndex == 2 ? "PLAY FROM THE START" : "NEXT STAGE  /  SPACE"; break;
            }
        }

        void DrawTile(TileView v, TileKind kind, float x, float y)
        {
            float inset = kind == TileKind.Seed || kind == TileKind.Dew ? 10 : kind == TileKind.Beetle ? 5 : 1;
            ArcadeUi.Place(v.Body.rectTransform, x + inset, y + inset, Tile - inset * 2, Tile - inset * 2);
            v.Body.rectTransform.localEulerAngles = Vector3.zero;
            v.Body.sprite = kind == TileKind.Spikes ? spikesSprite : kind == TileKind.Seed || kind == TileKind.Dew || kind == TileKind.Beetle ? v.Rounded : null;
            v.Body.type = kind == TileKind.Spikes ? Image.Type.Simple : Image.Type.Sliced;
            v.Accent.gameObject.SetActive(kind == TileKind.Ground);
            v.Glyph.gameObject.SetActive(kind == TileKind.Crate || kind == TileKind.Beetle);
            v.Glyph.text = kind == TileKind.Crate ? "×" : "••";
            v.Body.color = kind == TileKind.Ground ? GroundColor : kind == TileKind.Crate ? ArcadeUi.Hex("9B734A") :
                kind == TileKind.Seed ? Gold : kind == TileKind.Dew ? Blue : kind == TileKind.Spikes ? ArcadeUi.Hex("BC685E") :
                kind == TileKind.Beetle ? ArcadeUi.Hex("B4A0CF") : ArcadeUi.Hex("1B3036");
            v.Accent.color = ArcadeUi.Hex("609575");
            if (kind == TileKind.Seed) v.Body.rectTransform.localEulerAngles = new Vector3(0, 0, -20);
            if (kind == TileKind.Spikes) ArcadeUi.Place(v.Body.rectTransform, x + 4, y + 14, 32, 26);
        }

        void PlaceMarker(RectTransform marker, int x, int y) => ArcadeUi.Place(marker, (x - cameraX) * Tile, ViewHeight - y * Tile - 100, 40, 100);

        void OnEvent(RunEvent kind, Vector2 position)
        {
            if (kind == RunEvent.Tight) { Toast("No room to grow here. Move into an open space first."); return; }
            if (kind == RunEvent.Grow) Toast("GROW!   Size " + (Session.Tier + 1) + " / 4");
            if (kind == RunEvent.Shrink) Toast("A little smaller. A little more room.");
            if (kind == RunEvent.Hurt) Toast("Ouch! One size smaller — briefly protected.");
            if (!muted && Time.unscaledTime >= effectClock)
            {
                audioSource.PlayOneShot(kind == RunEvent.Finish ? winTone : kind == RunEvent.Grow || kind == RunEvent.Jump ? upTone : kind == RunEvent.Shrink ? downTone : hitTone, .15f);
                effectClock = Time.unscaledTime + .035f;
            }
            if (kind == RunEvent.Jump) return;
            int count = 0;
            foreach (var spark in sparks)
            {
                if (spark.Life > 0) continue;
                spark.Position = position; spark.Velocity = UnityEngine.Random.insideUnitCircle * 3 + Vector2.up * 2;
                spark.Life = .5f; spark.View.gameObject.SetActive(true);
                spark.View.color = kind == RunEvent.Shrink ? Blue : kind == RunEvent.Hurt ? ArcadeUi.BrickColors[0] : Gold;
                if (++count >= 8) break;
            }
        }

        void UpdateSparks(float dt)
        {
            foreach (var spark in sparks)
            {
                if (spark.Life <= 0) continue;
                spark.Life -= dt; spark.Position += spark.Velocity * dt; spark.Velocity += Vector2.down * 9 * dt;
                spark.View.gameObject.SetActive(spark.Life > 0);
                ArcadeUi.Place(spark.View.rectTransform, (spark.Position.x - cameraX) * Tile, ViewHeight - spark.Position.y * Tile, 5, 5);
            }
        }

        void Toast(string message) { toast = message; toastUntil = Time.unscaledTime + 5; }
        void SaveDraft()
        {
            if (RunnerStorage.Save(Draft, RunnerStorage.DraftPath, out var error)) { dirty = false; Toast("Draft saved locally. JSON lets you export a copy to share."); }
            else Toast(error);
        }
        void LoadDraft()
        {
            if (RunnerStorage.Load(RunnerStorage.DraftPath, out var level, out var error)) { SetDraft(level, false); Toast("Saved draft loaded."); }
            else Toast(error);
        }
        void ReplaceDraft(Action replace)
        {
            if (!dirty) { replace(); return; }
            Confirm("REPLACE YOUR DRAFT?", "You have unsaved edits. Save first to keep them, or replace the draft.", replace);
        }
        void Leave()
        {
            if (dirty) Confirm("LEAVE UNSAVED EDITS?", "Your saved draft will remain available. Edits since your last save will be lost.", () => SceneManager.LoadScene(GameCatalog.LauncherScene));
            else SceneManager.LoadScene(GameCatalog.LauncherScene);
        }

        RectTransform OpenModal(string title, string detail, float height)
        {
            CloseModal();
            modal = ui.Box(ui.Root, "Dialog shade", 0, 0, 1440, 900, new Color(.02f, .04f, .07f, .93f), false).rectTransform;
            modal.GetComponent<Image>().raycastTarget = true;
            var panel = ui.Box(modal, "Dialog", 300, (900 - height) / 2, 840, height, ArcadeUi.Panel).rectTransform;
            ui.Label(panel, title, 32, 27, 740, 48, 29, ArcadeUi.White, true);
            ui.Label(panel, detail, 32, 85, 776, 65, 16, ArcadeUi.Muted);
            return panel;
        }
        void CloseModal()
        {
            if (modal != null) { modal.gameObject.SetActive(false); Destroy(modal.gameObject); modal = null; }
            EventSystem.current.SetSelectedGameObject(null);
        }
        void Confirm(string title, string detail, Action action)
        {
            var panel = OpenModal(title, detail, 260);
            ui.Button(panel, "KEEP EDITING", 32, 180, 235, 48, CloseModal, true);
            ui.Button(panel, "DISCARD & CONTINUE", 287, 180, 270, 48, () => { CloseModal(); action(); });
        }

        void JsonDialog()
        {
            var panel = OpenModal("A LITTLE WORLD, IN A FILE.", "Copy to share, or paste a level to import. Import replaces the current draft after validation.", 730);
            var field = Field(panel, 32, 145, 776, 416, "Paste level JSON here", true);
            field.characterLimit = 128 * 1024; field.text = Draft.ToJson();
            var message = ui.Label(panel, "Files: " + RunnerStorage.DirectoryPath, 32, 570, 776, 68, 12, ArcadeUi.Muted);
            ui.Button(panel, "COPY + EXPORT", 32, 650, 210, 48, () =>
            {
                if (!RunnerLevel.TryParse(field.text, out var export, out var error)) { message.text = error; return; }
                string path = Path.Combine(RunnerStorage.DirectoryPath, "export.json");
                if (!RunnerStorage.Save(export, path, out error)) { message.text = error; return; }
                GUIUtility.systemCopyBuffer = export.ToJson(); message.text = "Copied! Export saved to " + path;
            }, true);
            ui.Button(panel, "PASTE", 254, 650, 120, 48, () => field.text = GUIUtility.systemCopyBuffer);
            ui.Button(panel, "IMPORT", 386, 650, 160, 48, () =>
            {
                if (!RunnerLevel.TryParse(field.text, out var level, out var error)) { message.text = error; return; }
                if (dirty) Confirm("IMPORT THIS LEVEL?", "Replace the unsaved draft with “" + level.name + "”? Your saved file will stay unchanged until you save.", () => { SetDraft(level, true); Toast("Level imported. Test it, then save your draft."); });
                else { SetDraft(level, true); CloseModal(); Toast("Level imported. Test it, then save your draft."); }
            });
            ui.Button(panel, "CLOSE", 648, 650, 160, 48, CloseModal);
        }

        InputField Field(Transform parent, float x, float y, float width, float height, string placeholder, bool multiline)
        {
            var box = ui.Box(parent, "Text input", x, y, width, height, ArcadeUi.Background);
            box.raycastTarget = true;
            var input = box.gameObject.AddComponent<InputField>();
            var text = ui.Label(box.transform, "", 12, 8, width - 24, height - 16, multiline ? 14 : 16, ArcadeUi.White);
            text.supportRichText = false;
            input.textComponent = text;
            input.placeholder = ui.Label(box.transform, placeholder, 12, 8, width - 24, height - 16, 16, ArcadeUi.Muted);
            input.lineType = multiline ? InputField.LineType.MultiLineNewline : InputField.LineType.SingleLine;
            input.selectionColor = new Color(.5f, .8f, .4f, .4f);
            return input;
        }

        void ToggleSound()
        {
            muted = !muted; PlayerPrefs.SetInt(MuteKey, muted ? 1 : 0); PlayerPrefs.Save(); UpdateSoundText();
        }
        void UpdateSoundText() => soundText.text = muted ? "SOUND OFF / M" : "SOUND ON / M";
        void OnApplicationFocus(bool focused)
        {
            if (!focused) { painting = false; lastPaint = new Vector2Int(-1, -1); }
            if (!focused && !Editing && Session?.State == RunState.Running) Session.TogglePause();
        }
        void OnDestroy()
        {
            actions?.Dispose();
            if (Session != null) Session.Happened -= OnEvent;
            foreach (var clip in new[] { upTone, downTone, hitTone, winTone }) if (clip != null) Destroy(clip);
            if (spikesSprite != null) { Destroy(spikesSprite.texture); Destroy(spikesSprite); }
        }
        static Sprite SpikesSprite()
        {
            const int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "Sprout spikes", filterMode = FilterMode.Point };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                pixels[y * size + x] = y <= 31 - Mathf.Abs(x % 16 - 7.5f) * 4 ? Color.white : Color.clear;
            texture.SetPixels(pixels); texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * .5f, 32);
        }
        static AudioClip Tone(float frequency, float duration)
        {
            const int rate = 22050;
            var samples = new float[Mathf.CeilToInt(rate * duration)];
            for (int i = 0; i < samples.Length; i++)
            {
                float t = i / (float)rate;
                samples[i] = Mathf.Sin(t * (frequency + t * 180) * Mathf.PI * 2) * Mathf.Min(t * 180, 1) * Mathf.Pow(1 - t / duration, 2);
            }
            var clip = AudioClip.Create("Sprout tone", samples.Length, 1, rate, false); clip.SetData(samples, 0); return clip;
        }
    }
}
