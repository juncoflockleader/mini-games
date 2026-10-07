using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;

namespace MiniGames
{
    public sealed class LauncherController : MonoBehaviour
    {
        InputAction playAction;
        void Start()
        {
            Application.targetFrameRate = 120;
            var ui = ArcadeUi.Create(transform);
            var root = ui.Root;
            ui.Header("THE COLLECTION  /  VOL. 01");
            ui.Label(root, "PICK UP & PLAY", 88, 188, 380, 28, 13, ArcadeUi.Mint, true);
            ui.Label(root, "Small games.\nGood times.", 84, 237, 610, 190, 70, ArcadeUi.White, true);
            ui.Label(root, "A growing collection of little escapes.\nEasy to learn. Hard to put down.", 88, 452, 530, 80, 23, ArcadeUi.Muted);
            ui.Box(root, "Collection rule", 88, 569, 90, 3, ArcadeUi.Mint, false);
            ui.Label(root, "ONE PLAYER.  ALL PLAY.", 88, 596, 480, 26, 12, ArcadeUi.Muted, true);

            var card = ui.Box(root, "Featured game", 744, 159, 608, 600, ArcadeUi.Panel).transform;
            ui.Label(card, "01   /   THE CLASSICS", 32, 27, 400, 24, 12, ArcadeUi.Muted, true);
            for (int row = 0; row < 5; row++)
            for (int col = 0; col < 7; col++)
                ui.Box(card, "Preview brick", 48 + col * 75, 83 + row * 29, 66, 20, ArcadeUi.BrickColors[row]);
            ui.Box(card, "Preview paddle", 254, 313, 106, 10, ArcadeUi.Mint);
            ui.Box(card, "Preview ball", 349, 259, 12, 12, ArcadeUi.White);
            for (int i = 0; i < 5; i++)
                ui.Box(card, "Ball trail", 360 + i * 8, 245 - i * 11, 8 - i, 8 - i, new Color(.8f, 1, .7f, .35f - i * .05f));
            ui.Label(card, "BREAKOUT", 32, 366, 500, 55, 42, ArcadeUi.White, true);
            ui.Label(card, GameCatalog.Games[0].Description, 34, 427, 540, 38, 17, ArcadeUi.Muted);
            var play = ui.Button(card, "PLAY BREAKOUT    >", 32, 492, 544, 65,
                () => SceneManager.LoadScene(GameCatalog.Games[0].Scene), true);
            ui.Label(root, "3 LEVELS   /   MOUSE + KEYBOARD", 774, 781, 530, 22, 11, ArcadeUi.Muted, false, TextAnchor.MiddleCenter);
            // Additional registered games own their own scenes and return through the catalog.
            for (int i = 1; i < GameCatalog.Games.Length; i++)
            {
                var game = GameCatalog.Games[i];
                ui.Button(root, "PLAY " + game.Title.ToUpperInvariant() + "    >", 88, 650 + (i - 1) * 90, 490, 52, () => SceneManager.LoadScene(game.Scene), true);
                ui.Label(root, "AUTO-RUNNER  /  STACKING GROWTH  /  LEVEL EDITOR", 88, 714 + (i - 1) * 90, 520, 30, 11, ArcadeUi.Muted);
            }
            Canvas.ForceUpdateCanvases();
            EventSystem.current.SetSelectedGameObject(play.gameObject);
            playAction = new InputAction("Play featured game", InputActionType.Button, "<Keyboard>/space");
            playAction.performed += _ => SceneManager.LoadScene(GameCatalog.Games[0].Scene);
            playAction.Enable();
        }

        void OnDestroy() => playAction?.Dispose();
    }
}
