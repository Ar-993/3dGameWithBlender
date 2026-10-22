using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace _3DLight.Gameplay;

internal readonly record struct GameplayInputFrame(
    KeyboardState Keyboard,
    bool CanUpdateWorld,
    bool SuppressInteraction,
    bool AttackPressed,
    Vector2 LookDelta,
    bool ToggleLighting,
    bool ToggleColliders,
    bool ToggleFlashlight = false,
    int SelectedSlot = -1,
    bool DropPressed = false);


/// <summary>
/// Owns gameplay input history, focus, pause, and cursor capture.
/// </summary>
internal sealed class GameplayInput
{
    private readonly Game game;
    private readonly PauseController pause = new();
    private KeyboardState previousKeyboard;
    private MouseState previousMouse;
    private bool wasActive;
    private bool capturedOnEnter;

    public bool IsPaused => pause.IsPaused;
    public bool MouseCaptured { get; private set; }

    public GameplayInput(Game game)
    {
        this.game = game;
    }

    public void Enter()
    {
        previousKeyboard = Keyboard.GetState();
        previousMouse = Mouse.GetState();
        pause.SynchronizeInput(previousKeyboard);
        wasActive = game.IsActive;

        if (game.IsActive && !pause.IsPaused)
        {
            CaptureMouse();
            capturedOnEnter = true;
        }
        else
        {
            ReleaseMouse();
        }
    }

    public void Exit()
    {
        capturedOnEnter = false;
        ReleaseMouse();
    }

    public GameplayInputFrame Update()
    {
        KeyboardState keyboard = Keyboard.GetState();
        MouseState mouse = Mouse.GetState();
        bool capturedThisFrame = capturedOnEnter;
        capturedOnEnter = false;
        bool justActivated = game.IsActive && !wasActive;

        if (!game.IsActive)
        {
            ReleaseMouse();
            pause.SynchronizeInput(keyboard);
            SynchronizeInput(keyboard, mouse);
            return new GameplayInputFrame(keyboard, false, true,
                false, Vector2.Zero, false, false);
        }

        PauseTransition transition = pause.Update(keyboard);
        if (transition == PauseTransition.Paused)
        {
            ReleaseMouse();
        }
        else if (transition == PauseTransition.Resumed)
        {
            CaptureMouse();
            capturedThisFrame = true;
        }

        if (pause.IsPaused)
        {
            SynchronizeInput(keyboard, mouse);
            return new GameplayInputFrame(keyboard, false, true,
                false, Vector2.Zero, false, false);
        }

        // A button held during Alt+Tab cannot capture the cursor.
        if (!MouseCaptured && !justActivated && IsInsideClient(mouse) &&
            mouse.LeftButton == ButtonState.Pressed &&
            previousMouse.LeftButton == ButtonState.Released)
        {
            CaptureMouse();
            capturedThisFrame = true;
        }

        Vector2 lookDelta = Vector2.Zero;
        if (MouseCaptured)
        {
            Point center = WindowCenter;
            if (!capturedThisFrame)
                lookDelta = new Vector2(mouse.X - center.X, mouse.Y - center.Y);
            Mouse.SetPosition(center.X, center.Y);
        }

        // The click that captures the cursor never starts an attack.
        bool attackPressed = MouseCaptured && !capturedThisFrame &&
            mouse.LeftButton == ButtonState.Pressed &&
            previousMouse.LeftButton == ButtonState.Released;

        bool toggleFlashlight = MouseCaptured && !capturedThisFrame && !justActivated &&
            keyboard.IsKeyDown(Keys.F) && previousKeyboard.IsKeyUp(Keys.F);

        bool toggleLighting = keyboard.IsKeyDown(Keys.L) && previousKeyboard.IsKeyUp(Keys.L);

        bool toggleColliders = keyboard.IsKeyDown(Keys.O) && previousKeyboard.IsKeyUp(Keys.O);

        bool suppressInteraction = !MouseCaptured || capturedThisFrame || justActivated;

        bool Pressed(Keys key)
        {
            return keyboard.IsKeyDown(key)
                && previousKeyboard.IsKeyUp(key);
        }

        // По умолчанию слот не переключаем.
        int selectedSlot = -1;
        bool dropPressed = false;

        // Обрабатываем инвентарь, только если ввод разрешён.
        if (!suppressInteraction)
        {
            if (Pressed(Keys.D1) || Pressed(Keys.NumPad1))
                selectedSlot = 0;
            else if (Pressed(Keys.D2) || Pressed(Keys.NumPad2))
                selectedSlot = 1;
            else if (Pressed(Keys.D3) || Pressed(Keys.NumPad3))
                selectedSlot = 2;
            else if (Pressed(Keys.D4) || Pressed(Keys.NumPad4))
                selectedSlot = 3;

            dropPressed = Pressed(Keys.T);
        }

        // Запоминаем состояние кнопок после проверки нажатий.
        SynchronizeInput(keyboard, mouse);

        return new GameplayInputFrame(
            keyboard, true, suppressInteraction,
            attackPressed, lookDelta,
            toggleLighting, toggleColliders, toggleFlashlight,
            selectedSlot, dropPressed);

    }

    private Point WindowCenter => new(
        game.Window.ClientBounds.Width / 2,
        game.Window.ClientBounds.Height / 2);

    private void CaptureMouse()
    {
        MouseCaptured = true;
        game.IsMouseVisible = false;
        Point center = WindowCenter;
        Mouse.SetPosition(center.X, center.Y);
    }

    private void ReleaseMouse()
    {
        MouseCaptured = false;
        game.IsMouseVisible = true;
    }

    private bool IsInsideClient(MouseState mouse) =>
        mouse.X >= 0 && mouse.X < game.Window.ClientBounds.Width &&
        mouse.Y >= 0 && mouse.Y < game.Window.ClientBounds.Height;

    private void SynchronizeInput(KeyboardState keyboard, MouseState mouse)
    {
        previousKeyboard = keyboard;
        previousMouse = mouse;
        wasActive = game.IsActive;
    }
}
