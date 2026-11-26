using _3DLight.Items;
using Camera;
using Items;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace _3DLight.Gameplay
{
    internal sealed class PlayerWeaponSystem : IDisposable
    {
        private const float TraceDuration = 0.10f;
        private readonly GraphicsDevice graphics;
        private readonly BasicEffect effect;
        private readonly AudioSystem? audio;
        private float traceRemaining;
        private Vector3 shotOrigin;
        private Vector3 shotEnd;
        private bool hitTarget;

        public PlayerWeaponSystem(GraphicsDevice graphics, AudioSystem? audio)
        {
            this.graphics = graphics;
            this.audio = audio;
            effect = new BasicEffect(graphics) { VertexColorEnabled = true, LightingEnabled = false };
        }

        public void Update(float seconds, ItemInteraction inventory, FirstPersonCamera camera,
            Level level, Player player, Skeleton skeleton, bool firePressed, bool reloadPressed)
        {
            traceRemaining = Math.Max(0f, traceRemaining - seconds);
            WorldItem? held = player.IsDead ? null : inventory.HeldItem;
            foreach (WorldItem item in inventory.Items)
                item.Weapon?.Update(seconds, item == held);

            if (held?.Weapon is not { } weapon)
                return;
            if (reloadPressed && weapon.TryReload())
                audio?.Play("reload");
            if (firePressed && weapon.TryFire())
                Fire(weapon, inventory, camera, level, skeleton);
        }

        private void Fire(WeaponState weapon, ItemInteraction inventory, FirstPersonCamera camera,
            Level level, Skeleton skeleton)
        {
            audio?.Play("shot");

            var pose = inventory.GetHeldPose(camera.Position, camera.Forward);
            Vector3 muzzle = GetMuzzlePosition(weapon, pose);
            (shotOrigin, WeaponHit shot) = TraceShot(level, camera, muzzle, weapon.Definition.Range, skeleton);
            shotEnd = shot.Position;
            hitTarget = shot.HitTarget;
            traceRemaining = TraceDuration;
            if (hitTarget)
                skeleton.TakeDamage(weapon.Definition.Damage);
        }

        private static (Vector3 Origin, WeaponHit Hit) TraceShot(
            Level level, FirstPersonCamera camera, Vector3 muzzle, float range, Skeleton skeleton)
        {
            if (!level.HasItemLineOfSight(camera.Position, muzzle))
            {
                Vector3 toMuzzle = muzzle - camera.Position;
                WeaponHit obstruction = WeaponRaycast.Cast(level, camera.Position, toMuzzle, toMuzzle.Length(), null);
                return (obstruction.Position, obstruction);
            }

            BoundingBox? target = skeleton.IsDead ? null : GetTargetBounds(skeleton.Position);
            // Луч камеры выбирает цель, а выстрел идёт к ней от дула.
            WeaponHit aim = WeaponRaycast.Cast(level, camera.Position, camera.Forward, range, target);
            Vector3 direction = aim.Position - muzzle;
            float shotRange = Math.Min(direction.Length() + 0.02f, range);
            return (muzzle, WeaponRaycast.Cast(level, muzzle, direction, shotRange, target));
        }

        private static BoundingBox GetTargetBounds(Vector3 feet)
        {
            const float radius = GameplayCharacters.CharacterCollisionRadius;
            const float height = GameplayCharacters.CharacterCollisionHeight;
            return new BoundingBox(feet - new Vector3(radius, 0f, radius), feet + new Vector3(radius, height, radius));
        }

        internal static Vector3 GetMuzzlePosition(WeaponState weapon, Ars3D.Items.ItemPose pose) =>
            Vector3.Transform(weapon.Muzzle, weapon.HeldAnimation * ItemMath.ToGame(pose.ToMatrix()));

        public void Draw(Matrix view, Matrix projection)
        {
            if (traceRemaining <= 0f)
                return;

            effect.View = view;
            effect.Projection = projection;
            effect.World = Matrix.Identity;
            Matrix cameraWorld = Matrix.Invert(view);
            Vector3 right = cameraWorld.Right * 0.055f;
            Vector3 up = cameraWorld.Up * 0.055f;
            BlendState blend = graphics.BlendState;
            DepthStencilState depth = graphics.DepthStencilState;
            try
            {
                graphics.BlendState = BlendState.AlphaBlend;
                graphics.DepthStencilState = DepthStencilState.None;
                foreach (EffectPass pass in effect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    DrawTrace(right, up);
                }
            }
            finally
            {
                graphics.BlendState = blend;
                graphics.DepthStencilState = depth;
            }
        }

        private void DrawTrace(Vector3 right, Vector3 up)
        {
            Color color = Color.Gold * (traceRemaining / TraceDuration);
            VertexPositionColor[] line = [new(shotOrigin, color), new(shotEnd, color)];
            graphics.DrawUserPrimitives(PrimitiveType.LineList, line, 0, 1);
            if (traceRemaining > 0.045f)
                DrawMuzzleFlash(right, up);
            if (hitTarget)
                DrawHitMarker(right, up);
        }

        private void DrawMuzzleFlash(Vector3 right, Vector3 up)
        {
            VertexPositionColor[] flash =
            [
                new(shotOrigin - right, Color.Orange), new(shotOrigin + up, Color.LightYellow),
            new(shotOrigin + right, Color.Orange),
            new(shotOrigin - right, Color.Orange), new(shotOrigin + right, Color.Orange),
            new(shotOrigin - up, Color.LightYellow)
            ];
            graphics.DrawUserPrimitives(PrimitiveType.TriangleList, flash, 0, 2);
        }

        private void DrawHitMarker(Vector3 right, Vector3 up)
        {
            Color hit = Color.OrangeRed;
            VertexPositionColor[] marker =
            [
                new(shotEnd - right - up, hit), new(shotEnd + right + up, hit),
            new(shotEnd - right + up, hit), new(shotEnd + right - up, hit)
            ];
            graphics.DrawUserPrimitives(PrimitiveType.LineList, marker, 0, 2);
        }

        public void Dispose() => effect.Dispose();
    }
}
