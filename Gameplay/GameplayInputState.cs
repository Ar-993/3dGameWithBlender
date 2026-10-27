using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace _3DLight.Gameplay;

// Здесь находятся назначения клавиш, история нажатий, пауза и правила захвата мыши.
// Класс работает со снимками устройств и не обращается к окну или GraphicsDevice.
internal sealed class GameplayInputState
{
    private KeyboardState previousKeyboard;
    private MouseState previousMouse;
    private bool wasActive;
    private bool capturedOnEnter;

    public bool IsPaused { get; private set; }
    public bool MouseCaptured { get; private set; }

    public GameplayInputFrame Enter(KeyboardState keyboard, MouseState mouse, bool isActive)
    {
        RememberDevices(keyboard, mouse, isActive);
        MouseCaptured = isActive && !IsPaused;
        capturedOnEnter = MouseCaptured;
        return SuspendedFrame(keyboard);
    }

    public void Exit()
    {
        capturedOnEnter = false;
        MouseCaptured = false;
    }

    public GameplayInputFrame Update(
        KeyboardState keyboard,
        MouseState mouse,
        bool isActive,
        Rectangle clientBounds)
    {
        bool justActivated = isActive && !wasActive;
        bool capturedThisFrame = capturedOnEnter;
        capturedOnEnter = false;

        GameplayInputFrame frame;
        if (!isActive)
        {
            MouseCaptured = false;
            frame = SuspendedFrame(keyboard);
        }
        else
        {
            if (Pressed(keyboard, Keys.Escape))
            {
                IsPaused = !IsPaused;
                MouseCaptured = !IsPaused;
                capturedThisFrame = MouseCaptured;
            }

            frame = IsPaused
                ? SuspendedFrame(keyboard)
                : ReadGameplayActions(keyboard, mouse, clientBounds, justActivated, capturedThisFrame);
        }

        // Обновляем историю даже в паузе и без фокуса: удержанная кнопка не становится новым нажатием.
        RememberDevices(keyboard, mouse, isActive);
        return frame;
    }

    private GameplayInputFrame ReadGameplayActions(
        KeyboardState keyboard,
        MouseState mouse,
        Rectangle clientBounds,
        bool justActivated,
        bool capturedThisFrame)
    {
        bool leftClick = mouse.LeftButton == ButtonState.Pressed &&
            previousMouse.LeftButton == ButtonState.Released;

        // Удержание ЛКМ при Alt+Tab не захватывает курсор; нужен новый клик внутри окна.
        if (!MouseCaptured && !justActivated && leftClick && IsInsideClient(mouse, clientBounds))
        {
            MouseCaptured = true;
            capturedThisFrame = true;
        }

        bool suppressInteraction = !MouseCaptured || capturedThisFrame || justActivated;
        Vector2 lookDelta = MouseCaptured && !capturedThisFrame
            ? new Vector2(mouse.X - clientBounds.Width / 2, mouse.Y - clientBounds.Height / 2)
            : Vector2.Zero;

        return new GameplayInputFrame
        {
            CanUpdateWorld = true,
            SuppressInteraction = suppressInteraction,
            Move = ReadMovement(keyboard),
            JumpPressed = Pressed(keyboard, Keys.Space),
            // Клик, захвативший курсор, не начинает атаку.
            AttackPressed = MouseCaptured && !capturedThisFrame && leftClick,
            LookDelta = lookDelta,
            InteractDown = keyboard.IsKeyDown(Keys.E),
            SelectedSlot = suppressInteraction ? -1 : ReadSelectedSlot(keyboard),
            DropPressed = !suppressInteraction && Pressed(keyboard, Keys.T),
            ToggleFlashlight = !suppressInteraction && Pressed(keyboard, Keys.F),
            ToggleLighting = Pressed(keyboard, Keys.L),
            ToggleColliders = Pressed(keyboard, Keys.O)
        };
    }

    private static GameplayInputFrame SuspendedFrame(KeyboardState keyboard) => new()
    {
        CanUpdateWorld = false,
        SuppressInteraction = true,
        InteractDown = keyboard.IsKeyDown(Keys.E)
    };

    private static Vector2 ReadMovement(KeyboardState keyboard) => new(
        (keyboard.IsKeyDown(Keys.D) ? 1f : 0f) - (keyboard.IsKeyDown(Keys.A) ? 1f : 0f),
        (keyboard.IsKeyDown(Keys.W) ? 1f : 0f) - (keyboard.IsKeyDown(Keys.S) ? 1f : 0f));

    private int ReadSelectedSlot(KeyboardState keyboard)
    {
        if (Pressed(keyboard, Keys.D1) || Pressed(keyboard, Keys.NumPad1)) return 0;
        if (Pressed(keyboard, Keys.D2) || Pressed(keyboard, Keys.NumPad2)) return 1;
        if (Pressed(keyboard, Keys.D3) || Pressed(keyboard, Keys.NumPad3)) return 2;
        if (Pressed(keyboard, Keys.D4) || Pressed(keyboard, Keys.NumPad4)) return 3;
        return -1;
    }

    private bool Pressed(KeyboardState keyboard, Keys key) =>
        keyboard.IsKeyDown(key) && previousKeyboard.IsKeyUp(key);

    private static bool IsInsideClient(MouseState mouse, Rectangle bounds) =>
        mouse.X >= 0 && mouse.X < bounds.Width && mouse.Y >= 0 && mouse.Y < bounds.Height;

    private void RememberDevices(KeyboardState keyboard, MouseState mouse, bool isActive)
    {
        previousKeyboard = keyboard;
        previousMouse = mouse;
        wasActive = isActive;
    }
}
