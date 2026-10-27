using _3DLight.Ui;
using Gum;
using Gum.Forms;
using Gum.GueDeriving;
using Gum.Wireframe;
using Items;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
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
    private SpriteFont interfaceFont = null!;
    private Texture2D pixel = null!;
    private string? interactionItemName;
    private string? worldInteractionText;
    private bool interactionItemIsHeld;
    private bool interactionIsVisible;
    private bool isPaused;
    private readonly WorldItem?[] inventorySlots = new WorldItem?[4];
    private InventoryIconCache inventoryIcons = null!;
    private int selectedInventorySlot;


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
        interfaceFont = game.Content.Load<SpriteFont>("Fonts/OldStandardText");
        pixel = new Texture2D(game.GraphicsDevice, 1, 1);
        pixel.SetData(new[] { Color.White });

        inventoryIcons = new InventoryIconCache(game.GraphicsDevice);
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

    public void SetWorldInteractionText(string? text) => worldInteractionText = text;

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
            SamplerState.LinearClamp,
            DepthStencilState.None,
            RasterizerState.CullNone);

        DrawInventory(viewport.Height);

        DrawCrosshair(viewport.Width / 2, viewport.Height / 2);

        if (worldInteractionText is not null || interactionItemName is not null && !interactionItemIsHeld)
        {
            DrawInteractionHint(viewport.Width, viewport.Height);
        }
        spriteBatch.End();
    }

    public void Dispose()
    {   
        inventoryIcons?.Dispose();
        gum.Uninitialize();
        spriteBatch?.Dispose();
        pixel?.Dispose();
    }

    private void DrawCrosshair(int x, int y)
    {
        spriteBatch.Draw(pixel, new Rectangle(x, y, 3, 3), new Color(0, 0, 0, 150));
        spriteBatch.Draw(pixel, new Rectangle(x - 1, y - 1, 2, 2), new Color(230, 230, 230, 210));
    }

    public void SetInventory(ItemInteraction interaction)
    {
        selectedInventorySlot = interaction.SelectedSlot;
        for (int i = 0; i < inventorySlots.Length; i++)
        {
            WorldItem? item = interaction.GetSlot(i);
            inventorySlots[i] = item;
            inventoryIcons.Prepare(item);
        }
    }

    private void DrawInventory(int screenHeight)
    {
        const int size = 64, gap = 8, left = 20;
        int top = screenHeight - size - 24;
        for (int i = 0; i < inventorySlots.Length; i++)
        {
            var rect = new Rectangle(left + i * (size + gap), top, size, size);
            bool selected = i == selectedInventorySlot;
            spriteBatch.Draw(pixel, rect, new Color(18, 20, 24, 150));
            Color border = selected ? Color.Gold : new Color(160, 160, 160, 180);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, size, 2), border);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - 2, size, 2), border);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, 2, size), border);
            spriteBatch.Draw(pixel, new Rectangle(rect.Right - 2, rect.Y, 2, size), border);

            WorldItem? item = inventorySlots[i];
            if (item is not null)
            {
                Texture2D? icon = inventoryIcons.Get(item);
                if (icon is not null)
                {
                    float scale = MathF.Min(44f / icon.Width, 44f / icon.Height);
                    int width = Math.Max(1, (int)(icon.Width * scale));
                    int height = Math.Max(1, (int)(icon.Height * scale));
                    var destination = new Rectangle(rect.Center.X - width / 2,
                        rect.Center.Y - height / 2 + 3, width, height);
                    spriteBatch.Draw(icon, destination, Color.White);
                }
                else
                {
                    Vector2 placeholderSize = interfaceFont.MeasureString("?");
                    var placeholderPosition = new Vector2(
                        rect.Center.X - placeholderSize.X / 2f,
                        rect.Center.Y - placeholderSize.Y / 2f);
                    spriteBatch.DrawString(interfaceFont, "?", placeholderPosition, Color.White);
                }
            }
            spriteBatch.DrawString(interfaceFont, (i + 1).ToString(),
                new Vector2(rect.X + 5, rect.Y + 2), Color.White);
        }

        string hint = "1-4: СЛОТ   E: ВЗЯТЬ   T: ВЫБРОСИТЬ";
        if (inventorySlots[selectedInventorySlot] is { } active)
        {
            hint = active.Name + "   T: ВЫБРОСИТЬ";
            if (active.State.Definition.Id == "flashlight")
                hint += "   F: СВЕТ";
        }
        spriteBatch.DrawString(interfaceFont, hint,
            new Vector2(left, top - interfaceFont.LineSpacing - 6), Color.White);
    }


    private void DrawInteractionHint(int screenWidth, int screenHeight)
    {
        string itemName = interactionItemName == "Дед Мороз"
            ? "ДЕДА МОРОЗА"
            : interactionItemName!;
        string hint = worldInteractionText ?? $"[E] ВЗЯТЬ {itemName}";
        Vector2 size = interfaceFont.MeasureString(hint);
        Vector2 position = new((int)((screenWidth - size.X) * 0.5f), screenHeight - 54);
        spriteBatch.DrawString(interfaceFont, hint, position + new Vector2(2f), new Color(0, 0, 0, 190));
        spriteBatch.DrawString(interfaceFont, hint, position, new Color(235, 235, 235));
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
