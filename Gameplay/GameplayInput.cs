using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace _3DLight.Gameplay;

// Опрос устройств и применение состояния курсора. Правила ввода — в GameplayInputState.
internal sealed class GameplayInput
{
    private readonly Game game;
    private readonly GameplayInputState state = new();

    public bool IsPaused => state.IsPaused;
    public bool MouseCaptured => state.MouseCaptured;

    public GameplayInput(Game game) => this.game = game;

    public GameplayInputFrame Enter()
    {
        GameplayInputFrame frame = state.Enter(Keyboard.GetState(), Mouse.GetState(), game.IsActive);
        ApplyCursorState();
        return frame;
    }

    public void Exit()
    {
        state.Exit();
        ApplyCursorState();
    }

    public GameplayInputFrame Update()
    {
        GameplayInputFrame frame = state.Update(
            Keyboard.GetState(), Mouse.GetState(), game.IsActive, game.Window.ClientBounds);
        ApplyCursorState();
        return frame;
    }

    private void ApplyCursorState()
    {
        game.IsMouseVisible = !MouseCaptured;
        if (MouseCaptured)
        {
            Rectangle bounds = game.Window.ClientBounds;
            Mouse.SetPosition(bounds.Width / 2, bounds.Height / 2);
        }
    }
}
