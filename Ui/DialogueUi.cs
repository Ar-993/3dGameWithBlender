using _3DLight.Npcs;
using Gum.GueDeriving;
using Gum.Wireframe;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using BlendState = Microsoft.Xna.Framework.Graphics.BlendState;

namespace _3DLight.Ui;

internal sealed class DialogueUi
{
    private const bool UseSpeechBubble = false;
    private const int Padding = 12;
    private const string Controls = "[E] ДАЛЬШЕ   [Esc] ЗАКРЫТЬ";

    private readonly ColoredRectangleRuntime panel = CreateBackground();
    private readonly ColoredRectangleRuntime bubble = CreateBackground();
    private readonly ColoredRectangleRuntime tail = CreateBackground();
    private Vector2 textPosition;
    private string text = "";

    public bool HasBubble => bubble.Visible;
    public bool HasText => panel.Visible || HasBubble;

    public void Layout(Viewport viewport, SpriteFont font, DialogueSession dialogue, Vector2? anchor)
    {
        panel.Visible = false;
        bubble.Visible = false;
        tail.Visible = false;

        if (dialogue.IsOpen)
        {
            if (UseSpeechBubble && anchor is { } head &&
                TryPlaceBubble(viewport, font, head, dialogue.SpeakerName + "\n" + dialogue.Line + "\n" + Controls))
                return;

            int width = Math.Min(760, viewport.Width - 24);
            text = dialogue.SpeakerName + "\n\n" +
                Wrap(font, dialogue.Line, width - 2 * Padding) + "\n\n" + Controls;
            int height = (int)MathF.Ceiling(font.MeasureString(text).Y) + 2 * Padding;
            int x = (viewport.Width - width) / 2;
            int y = Math.Max(8, viewport.Height - height - 24);
            SetBounds(panel, x, y, width, height);
            textPosition = new Vector2(x + Padding, y + Padding);
            return;
        }

        if (anchor is { } target)
            TryPlaceBubble(viewport, font, target, "[E] ГОВОРИТЬ");
    }

    public void DrawText(SpriteBatch batch, SpriteFont font) =>
    batch.DrawString(font, text, textPosition, Color.White);

    private bool TryPlaceBubble(Viewport viewport, SpriteFont font, Vector2 anchor, string message)
    {
        string wrapped = Wrap(font, message, Math.Min(300, viewport.Width - 40));
        Vector2 size = font.MeasureString(wrapped);
        int width = (int)MathF.Ceiling(size.X) + 2 * Padding;
        int height = (int)MathF.Ceiling(size.Y) + 2 * Padding;
        int y = (int)anchor.Y - height - 8;
        if (width > viewport.Width - 16 || y < 8)
            return false;

        int x = Math.Clamp((int)anchor.X - width / 2, 8, viewport.Width - width - 8);
        int tailX = Math.Clamp((int)anchor.X - 3, x, x + width - 6);
        SetBounds(bubble, x, y, width, height);
        SetBounds(tail, tailX, y + height, 6, 8);
        text = wrapped;
        textPosition = new Vector2(x + Padding, y + Padding);
        return true;
    }

    private static ColoredRectangleRuntime CreateBackground()
    {
        var rectangle = new ColoredRectangleRuntime
        {
            Color = new Color(18, 20, 24),
            Alpha = 235,
            Visible = false
        };
        rectangle.AddToRoot();
        return rectangle;
    }

    private static void SetBounds(ColoredRectangleRuntime rectangle, int x, int y, int width, int height)
    {
        rectangle.X = x;
        rectangle.Y = y;
        rectangle.Width = width;
        rectangle.Height = height;
        rectangle.Visible = true;
    }

    private static string Wrap(SpriteFont font, string text, float width)
    {
        var lines = new List<string>();
        foreach (string paragraph in text.Replace("\r", "").Split('\n'))
        {
            string line = "";
            foreach (string word in paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                string candidate = line.Length == 0 ? word : line + " " + word;
                if (line.Length > 0 && font.MeasureString(candidate).X > width)
                {
                    lines.Add(line);
                    line = word;
                }
                else
                {
                    line = candidate;
                }
            }
            lines.Add(line);
        }
        return string.Join("\n", lines);
    }
}