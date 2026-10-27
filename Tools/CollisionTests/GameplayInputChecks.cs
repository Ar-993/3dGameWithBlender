using _3DLight.Gameplay;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

internal static class GameplayInputChecks
{
    private static readonly Rectangle Window = new(0, 0, 320, 180);

    public static void Run(Action<string, Action> run)
    {
        run("gameplay input maps keys to actions and emits button edges once", () =>
        {
            GameplayInputState input = ReadyInput();
            KeyboardState held = new(Keys.W, Keys.D, Keys.Space, Keys.E, Keys.D2, Keys.T, Keys.F, Keys.L, Keys.O);
            GameplayInputFrame first = Read(input, held, click: true);
            Require(first.Move == Vector2.One && first.JumpPressed && first.AttackPressed,
                "Movement, jump or attack was not mapped to an action");
            Require(first.InteractDown && first.SelectedSlot == 1 && first.DropPressed &&
                first.ToggleFlashlight && first.ToggleLighting && first.ToggleColliders,
                "Item or debug controls were not mapped");

            GameplayInputFrame repeated = Read(input, held, click: true);
            Require(repeated.Move == Vector2.One && repeated.InteractDown &&
                !repeated.JumpPressed && !repeated.AttackPressed && !repeated.DropPressed &&
                !repeated.ToggleFlashlight && !repeated.ToggleLighting && !repeated.ToggleColliders &&
                repeated.SelectedSlot == -1, "Holding buttons repeated an edge action");

            Read(input, new());
            Require(Read(input, held, click: true).JumpPressed, "A fresh Space press was lost");
            Read(input, new());
            Require(Read(input, new(Keys.NumPad4)).SelectedSlot == 3, "Numpad slot selection was lost");
        });

        run("pause consumes held jump and item actions before resuming", () =>
        {
            GameplayInputState input = ReadyInput();
            Require(!Read(input, new(Keys.Escape)).CanUpdateWorld && input.IsPaused && !input.MouseCaptured,
                "Escape did not pause and release the cursor");

            KeyboardState held = new(Keys.Space, Keys.E, Keys.D2, Keys.T, Keys.F);
            GameplayInputFrame paused = Read(input, held, click: true);
            Require(!paused.CanUpdateWorld && paused.SuppressInteraction && paused.InteractDown &&
                !paused.JumpPressed && !paused.AttackPressed && paused.SelectedSlot == -1,
                "Paused input exposed actions or lost the held E state");

            GameplayInputFrame resumed = Read(input,
                new(Keys.Escape, Keys.Space, Keys.E, Keys.D2, Keys.T, Keys.F), click: true);
            Require(resumed.CanUpdateWorld && !input.IsPaused && input.MouseCaptured &&
                resumed.SuppressInteraction && resumed.InteractDown && !resumed.JumpPressed &&
                !resumed.AttackPressed && !resumed.DropPressed && !resumed.ToggleFlashlight &&
                resumed.SelectedSlot == -1 && resumed.LookDelta == Vector2.Zero,
                "Resuming replayed held buttons or moved the camera");

            GameplayInputFrame holding = Read(input, held, click: true);
            Require(!holding.SuppressInteraction && !holding.JumpPressed && !holding.DropPressed &&
                !holding.ToggleFlashlight && holding.SelectedSlot == -1,
                "Buttons held through pause became fresh actions after capture");

            Read(input, new());
            GameplayInputFrame fresh = Read(input, new(Keys.Space, Keys.E, Keys.T));
            Require(fresh.JumpPressed && fresh.InteractDown && fresh.DropPressed,
                "Pause synchronization blocked subsequent fresh input");
        });

        run("focus recovery requires a fresh inside click and capture never attacks", () =>
        {
            GameplayInputState input = ReadyInput();
            GameplayInputFrame inactive = input.Update(new(Keys.Space, Keys.E), Mouse(click: true), false, Window);
            Require(!inactive.CanUpdateWorld && inactive.InteractDown && !input.MouseCaptured,
                "Losing focus did not suspend the world and release the cursor");

            GameplayInputFrame activated = Read(input, new(Keys.Space, Keys.E), click: true);
            Require(activated.CanUpdateWorld && activated.SuppressInteraction &&
                !activated.JumpPressed && !activated.AttackPressed && !input.MouseCaptured,
                "Held input during Alt+Tab captured the cursor or triggered an action");

            Read(input, new());
            input.Update(new(), Mouse(click: true, x: -1), true, Window);
            Require(!input.MouseCaptured, "An outside click captured the cursor");
            Read(input, new());
            GameplayInputFrame capture = Read(input, new(Keys.T, Keys.F, Keys.D3), click: true);
            Require(input.MouseCaptured && capture.SuppressInteraction && !capture.AttackPressed &&
                !capture.DropPressed && !capture.ToggleFlashlight && capture.SelectedSlot == -1 &&
                capture.LookDelta == Vector2.Zero,
                "The capture click triggered gameplay actions");

            Read(input, new());
            Require(Read(input, new(), click: true).AttackPressed, "A fresh captured click did not attack");
        });

        run("input entry synchronizes held buttons and mouse look uses client center", () =>
        {
            GameplayInputState input = new();
            GameplayInputFrame entry = input.Enter(new(Keys.Space, Keys.E), Mouse(click: true), true);
            Require(entry.InteractDown && entry.SuppressInteraction, "Entry lost held interaction state");
            GameplayInputFrame first = Read(input, new(Keys.Space, Keys.E), click: true);
            Require(!first.JumpPressed && !first.AttackPressed && first.LookDelta == Vector2.Zero,
                "Entry replayed held input or initial mouse displacement");

            GameplayInputFrame look = input.Update(new(), Mouse(x: 173, y: 82), true, Window);
            Require(look.LookDelta == new Vector2(13, -8), "Mouse delta did not use the client center");
        });
    }

    private static GameplayInputState ReadyInput()
    {
        GameplayInputState input = new();
        input.Enter(new(), Mouse(), true);
        Read(input, new());
        return input;
    }

    private static GameplayInputFrame Read(GameplayInputState input, KeyboardState keyboard,
        bool click = false) => input.Update(keyboard, Mouse(click), true, Window);

    private static MouseState Mouse(bool click = false, int x = 160, int y = 90) => new(
        x, y, 0, click ? ButtonState.Pressed : ButtonState.Released,
        ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
