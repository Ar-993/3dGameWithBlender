using Microsoft.Xna.Framework;

namespace _3DLight.Items
{
    internal sealed class WeaponState
    {
        public WeaponData Definition { get; }
        public int LoadedRounds { get; private set; }
        public int ReserveRounds { get; private set; }
        private float ReloadRemaining { get; set; }
        private float Cooldown { get; set; }
        private float Kick { get; set; }
        public bool IsReloading => ReloadRemaining > 0f;
        public Vector3 Muzzle { get; }

        public WeaponState(WeaponData definition)
        {
            definition.Validate();
            Definition = definition;
            LoadedRounds = definition.MagazineSize;
            ReserveRounds = definition.ReserveRounds;
            Muzzle = ItemMath.ToGame(ItemCatalogVectors.Read(definition.Muzzle, "muzzle"));
        }

        public void Update(float seconds, bool equipped)
        {
            seconds = Math.Max(0f, seconds);
            Cooldown = Math.Max(0f, Cooldown - seconds);
            Kick = Math.Max(0f, Kick - seconds * 9f);
            if (!equipped)
            {
                ReloadRemaining = 0f;
                return;
            }
            if (!IsReloading)
                return;
            ReloadRemaining = Math.Max(0f, ReloadRemaining - seconds);
            if (ReloadRemaining == 0f)
            {
                int count = Math.Min(Definition.MagazineSize - LoadedRounds, ReserveRounds);
                LoadedRounds += count;
                ReserveRounds -= count;
            }
        }

        public bool TryReload()
        {
            if (IsReloading || LoadedRounds == Definition.MagazineSize || ReserveRounds == 0)
                return false;
            ReloadRemaining = Definition.ReloadSeconds;
            return true;
        }

        public bool TryFire()
        {
            if (IsReloading || Cooldown > 0f || LoadedRounds == 0)
                return false;
            LoadedRounds--;
            Cooldown = Definition.FireInterval;
            Kick = 1f;
            return true;
        }

        public Matrix HeldAnimation
        {
            get
            {
                float reload = IsReloading
                    ? MathF.Sin(MathF.PI * ReloadRemaining / Definition.ReloadSeconds) : 0f;
                return Matrix.CreateRotationX(Kick * 0.045f - reload * 0.55f) *
                    Matrix.CreateTranslation(0f, -reload * 0.15f, Kick * 0.055f);
            }
        }
    }

    internal sealed class WeaponData
    {
        public float[] Muzzle { get; init; } = [0f, 0f, -0.5f];
        public int MagazineSize { get; init; } = 5;
        public int ReserveRounds { get; init; } = 25;
        public int Damage { get; init; } = 40;
        public float Range { get; init; } = 120f;
        public float FireInterval { get; init; } = 0.65f;
        public float ReloadSeconds { get; init; } = 1.8f;

        public void Validate()
        {
            if (MagazineSize <= 0 || ReserveRounds < 0 || Damage <= 0 ||
                !float.IsFinite(Range) || Range <= 0f ||
                !float.IsFinite(FireInterval) || FireInterval <= 0f ||
                !float.IsFinite(ReloadSeconds) || ReloadSeconds <= 0f)
                throw new InvalidDataException("Invalid weapon settings in item catalog.");
        }
    }
}
