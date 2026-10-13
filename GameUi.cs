using Gum;
using Gum.Forms;
using Gum.GueDeriving;
using Gum.Wireframe;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using _3DLight.Ui;
using BlendState = Microsoft.Xna.Framework.Graphics.BlendState;

/// <summary>
/// Экранный интерфейс игры: Gum для состояния мира и SpriteBatch для взаимодействия.
/// </summary>
internal sealed class GameUi : IDisposable
{
    private readonly GumService gum = GumService.Default;

    private TextRuntime playerText = null!;
    private TextRuntime skeletonText = null!;
    private TextRuntime performanceText = null!;
    private TextRuntime pauseText = null!;
    private SpriteBatch spriteBatch = null!;
    private Texture2D pixel = null!;
    private string? interactionItemName;
    private bool interactionItemIsHeld;
    private bool interactionIsVisible;
    private bool isPaused;

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

        spriteBatch = new SpriteBatch(game.GraphicsDevice);
        pixel = new Texture2D(game.GraphicsDevice, 1, 1);
        pixel.SetData(new[] { Color.White });
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
        this.isPaused = isPaused;
        pauseText.Visible = isPaused;
    }

    public void SetInteractionText(string? itemName, bool isHeld, bool isVisible)
    {
        interactionItemName = string.IsNullOrWhiteSpace(itemName) ? null : itemName.Trim();
        interactionItemIsHeld = isHeld;
        interactionIsVisible = isVisible;
    }

    public void Draw()
    {
        gum.Draw();

        if (!interactionIsVisible || isPaused)
        {
            return;
        }

        var viewport = spriteBatch.GraphicsDevice.Viewport;
        spriteBatch.Begin(
            SpriteSortMode.Deferred,
            BlendState.AlphaBlend,
            SamplerState.PointClamp,
            DepthStencilState.None,
            RasterizerState.CullNone);

        DrawCrosshair(viewport.Width / 2, viewport.Height / 2);

        if (interactionItemName is not null && !interactionItemIsHeld)
        {
            DrawPickupHint(viewport.Width, viewport.Height);
        }
        spriteBatch.End();
    }

    public void Dispose()
    {
        gum.Uninitialize();
        spriteBatch?.Dispose();
        pixel?.Dispose();
    }

    private void DrawCrosshair(int x, int y)
    {
        spriteBatch.Draw(pixel, new Rectangle(x, y, 3, 3), new Color(0, 0, 0, 150));
        spriteBatch.Draw(pixel, new Rectangle(x - 1, y - 1, 2, 2), new Color(230, 230, 230, 210));
    }

    private void DrawPickupHint(int screenWidth, int screenHeight)
    {
        string itemName = interactionItemName == "Дед Мороз"
            ? "ДЕДА МОРОЗА"
            : interactionItemName!;
        string hint = $"[E] ВЗЯТЬ {itemName}";
        Vector2 size = PsxPixelFont.MeasureString(hint);
        Vector2 position = new((int)((screenWidth - size.X) * 0.5f), screenHeight - 54);
        PsxPixelFont.Draw(spriteBatch, pixel, hint, position + new Vector2(2f), new Color(0, 0, 0, 190));
        PsxPixelFont.Draw(spriteBatch, pixel, hint, position, new Color(235, 235, 235));
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
