using Gum;
using Gum.Forms;
using Gum.GueDeriving;
using Gum.Wireframe;
using Microsoft.Xna.Framework;

/// <summary>
/// Весь экранный интерфейс игры, созданный кодом через Gum.
/// </summary>
internal sealed class GameUi : IDisposable
{
    private readonly GumService gum = GumService.Default;

    private TextRuntime playerText = null!;
    private TextRuntime skeletonText = null!;
    private TextRuntime performanceText = null!;
    private TextRuntime pauseText = null!;

    public void Initialize(Game game)
    {
        gum.Initialize(game, DefaultVisualsVersion.V3);

        playerText = CreateText(15f, 15f, Color.Yellow);
        skeletonText = CreateText(15f, 35f, Color.LawnGreen);
        performanceText = CreateText(15f, 55f, Color.Cyan);

        pauseText = CreateText(0f, 0f, Color.White);
        pauseText.Text = "PAUSED - PRESS ESC TO CONTINUE";
        pauseText.Anchor(Anchor.Center);
        pauseText.Visible = false;
    }

    public void Update(GameTime gameTime)
    {
        gum.Update(gameTime);
    }

    public void SetHudText(
        string player,
        string skeleton,
        string performance,
        bool skeletonIsDead)
    {
        playerText.Text = player;
        skeletonText.Text = skeleton;
        performanceText.Text = performance;
        skeletonText.Color = skeletonIsDead
            ? Color.Gray
            : Color.LawnGreen;
    }

    public void SetPaused(bool isPaused)
    {
        pauseText.Visible = isPaused;
    }

    public void Draw()
    {
        gum.Draw();
    }

    public void Dispose()
    {
        gum.Uninitialize();
    }

    private static TextRuntime CreateText(float x, float y, Color color)
    {
        var text = new TextRuntime
        {
            X = x,
            Y = y,
            Color = color,
            FontSize = 14,
            HasDropshadow = true,
            DropshadowColor = Color.Black
        };

        text.AddToRoot();
        return text;
    }
}
