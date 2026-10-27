using Microsoft.Xna.Framework;

namespace _3DLight.Gameplay;

// Действия за один кадр. Игровому коду не нужно знать назначенные клавиши.
internal readonly record struct GameplayInputFrame
{
    public GameplayInputFrame() { }

    public bool CanUpdateWorld { get; init; }
    public bool SuppressInteraction { get; init; }

    // X — вправо, Y — вперёд. Контроллер переводит направление в координаты мира.
    public Vector2 Move { get; init; }
    public bool JumpPressed { get; init; }
    public bool AttackPressed { get; init; }
    public Vector2 LookDelta { get; init; }

    public bool InteractDown { get; init; }
    public bool InteractPressed { get; init; }
    public int SelectedSlot { get; init; } = -1;
    public bool DropPressed { get; init; }
    public bool ToggleFlashlight { get; init; }

    public bool ToggleLighting { get; init; }
    public bool ToggleColliders { get; init; }
}
