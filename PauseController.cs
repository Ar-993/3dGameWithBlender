using Microsoft.Xna.Framework.Input;

internal enum PauseTransition
{
    None,
    Paused,
    Resumed
}

/// <summary>
/// Хранит состояние паузы и самостоятельно определяет новое нажатие Escape.
/// </summary>
internal sealed class PauseController
{
    private KeyboardState previousKeyboard;

    public bool IsPaused { get; private set; }

    public PauseTransition Update(KeyboardState keyboard)
    {
        bool escapePressed =
            keyboard.IsKeyDown(Keys.Escape) &&
            previousKeyboard.IsKeyUp(Keys.Escape);

        previousKeyboard = keyboard;

        if (!escapePressed)
            return PauseTransition.None;

        IsPaused = !IsPaused;
        return IsPaused
            ? PauseTransition.Paused
            : PauseTransition.Resumed;
    }

    public void SynchronizeInput(KeyboardState keyboard)
    {
        previousKeyboard = keyboard;
    }
}
