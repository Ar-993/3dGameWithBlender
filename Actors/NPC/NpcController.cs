using Ars3D.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace _3DLight.Npcs;

internal sealed class NpcController : IDisposable
{
    private readonly CharacterPhysicsComponent physics;
    private readonly CharacterAnimationComponent animation = new(modelScale: 1f);
    private readonly NpcBrain brain;
    private readonly NpcSkin skin;
    private readonly float visualYaw;

    public NpcDefinition Definition { get; }
    public CapsuleShape Capsule => new(physics.Position, physics.Radius, physics.Height);
    public bool ReachedDestination { get; private set; }

    public NpcController(NpcDefinition definition, NpcSkin skin)
    {
        Definition = definition;
        this.skin = skin;
        brain = new NpcBrain(definition.Speed);
        physics = new CharacterPhysicsComponent(Vector3.Zero, definition.Radius, definition.Height, gravity: -28f);
        visualYaw = MathHelper.ToRadians(skin.VisualYawDegrees);
    }

    public void LoadContent(GraphicsDevice graphics, Texture2D fallbackTexture, Effect effect)
    {
        animation.LoadContent(graphics, skin.Model, skin.IdleClip, fallbackTexture, effect,
            targetHeight: Definition.Height);
        if (animation.GetClipDuration(skin.IdleClip) <= 0f || animation.GetClipDuration(skin.WalkClip) <= 0f)
            throw new InvalidDataException($"Для NPC '{Definition.Id}' нужны клипы ожидания и ходьбы.");
    }

    public void SpawnAt(Vector3 position)
    {
        physics.SpawnAt(position);
        ReachedDestination = false;
        animation.RotationY = visualYaw;
        animation.Restart(skin.IdleClip, loop: true);
    }

    public void Update(Level level, NpcOrder order, float seconds, ReadOnlySpan<CapsuleShape> obstacles)
    {
        Vector3 before = physics.Position;
        NpcIntent intent = brain.Tick(before, order, seconds);
        ReachedDestination = intent.ReachedDestination;

        physics.Move(level, intent.Movement, seconds, obstacles);
        Vector3 actual = physics.Position - before;
        actual.Y = 0f;
        bool walking = intent.Movement != Vector3.Zero && actual.LengthSquared() > 0.000001f;
        if (walking)
            animation.RotationY = MathF.Atan2(actual.X, actual.Z) + visualYaw;

        animation.Play(walking ? skin.WalkClip : skin.IdleClip, loop: true);
        animation.Update(seconds);
    }

    public void Draw(Matrix view, Matrix projection, SceneLighting lighting, SceneRenderQueue queue) =>
        animation.Draw(physics.Position, view, projection, lighting, queue);

    public void Dispose() => animation.Dispose();
}