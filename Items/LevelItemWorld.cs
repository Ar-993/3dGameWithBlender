using Ars3D.Items;
using Ars3D.Items.Physics;
using System.Numerics;

namespace _3DLight.Items;

// Item physics asks for contacts; this adapter translates those requests to the level.
internal sealed class LevelItemWorld(Level level) : IItemInteractionWorld
{
    private readonly List<RigidItemContact> gameContacts = [];

    public bool SweepSphere(Vector3 center, Vector3 movement, float radius,
        Vector3 contactVelocity, out SweepHit hit)
    {
        if (!level.SweepRigidItemSphere(ItemMath.ToGame(center), ItemMath.ToGame(movement),
                radius, ItemMath.ToGame(contactVelocity), out RigidItemSweepHit gameHit))
        {
            hit = default;
            return false;
        }
        RigidItemContact contact = gameHit.Contact;
        hit = new(gameHit.Fraction, new(ItemMath.ToLibrary(contact.Point),
            ItemMath.ToLibrary(contact.Normal), contact.Penetration));
        return true;
    }

    public void GetSphereContacts(Vector3 center, float radius, List<BodyContact> contacts)
    {
        gameContacts.Clear();
        level.GetRigidItemSphereContacts(ItemMath.ToGame(center), radius, gameContacts);
        foreach (RigidItemContact contact in gameContacts)
        {
            BodyContact translated = new(ItemMath.ToLibrary(contact.Point),
                ItemMath.ToLibrary(contact.Normal), contact.Penetration);
            bool duplicate = contacts.Any(previous =>
                Vector3.DistanceSquared(previous.Point, translated.Point) < 0.00000001f &&
                Vector3.Dot(previous.Normal, translated.Normal) > 0.999f);
            if (!duplicate)
                contacts.Add(translated);
        }
    }

    public bool HasLineOfSight(Vector3 eyePosition, Vector3 target) =>
        level.HasItemLineOfSight(ItemMath.ToGame(eyePosition), ItemMath.ToGame(target));

    public Vector3 ResolveSpherePlacement(Vector3 sourceCenter, Vector3 desiredCenter, float radius)
    {
        var up = Microsoft.Xna.Framework.Vector3.Up * radius;
        var bottom = level.ResolveItemReleasePosition(ItemMath.ToGame(sourceCenter) - up,
            ItemMath.ToGame(desiredCenter) - up, radius, 2f * radius);
        return ItemMath.ToLibrary(bottom + up);
    }
}
