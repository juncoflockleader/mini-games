namespace MiniGames
{
    public sealed class GameDefinition
    {
        public string Title { get; }
        public string Scene { get; }
        public string Description { get; }
        public GameDefinition(string title, string scene, string description)
        { Title = title; Scene = scene; Description = description; }
    }

    public static class GameCatalog
    {
        public const string LauncherScene = "Launcher";
        public static readonly GameDefinition[] Games =
        {
            new GameDefinition("Breakout", "Breakout", "Find your angle. Clear the bricks. Keep it going."),
            new GameDefinition("Sprout Run", "SproutRun", "Run a little. Grow a lot. Build your own way through.")
        };
    }
}
