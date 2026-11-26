using Ars3D.Items.Physics;
using Ars3D.Items.Physics.Collision;
using System.Numerics;

namespace _3DLight.Items;

// Item physics asks for contacts; this adapter translates those requests to the level.
// Преобразует векторы и результаты между физикой предметов и игровым уровнем.
internal sealed class LevelItemWorld(Level level) : ICapsuleCollisionWorld
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
        hit = new(gameHit.Fraction, TranslateContact(gameHit.Contact));
        return true;
    }

    public bool SweepCapsule(Vector3 start, Vector3 end, Vector3 startMovement, Vector3 endMovement,
        float radius, Vector3 startVelocity, Vector3 endVelocity, out SweepHit hit)
    {
        if (!level.SweepRigidItemCapsule(ItemMath.ToGame(start), ItemMath.ToGame(end),
            ItemMath.ToGame(startMovement), ItemMath.ToGame(endMovement), radius,
            ItemMath.ToGame(startVelocity), ItemMath.ToGame(endVelocity), out var gameHit))
        {
            hit = default;
            return false;
        }
        hit = new(gameHit.Fraction, TranslateContact(gameHit.Contact));
        return true;
    }

    public void GetCapsuleContacts(Vector3 start, Vector3 end, float radius, List<BodyContact> contacts)
    {
        gameContacts.Clear();
        level.GetRigidItemCapsuleContacts(ItemMath.ToGame(start), ItemMath.ToGame(end), radius, gameContacts);
        AppendContacts(contacts);
    }

    private void AppendContacts(List<BodyContact> contacts)
    {
        foreach (RigidItemContact contact in gameContacts)
        {
            BodyContact translated = TranslateContact(contact);
            bool duplicate = contacts.Any(previous =>
                Vector3.DistanceSquared(previous.Point, translated.Point) < 0.00000001f &&
                Vector3.Dot(previous.Normal, translated.Normal) > 0.999f);
            if (!duplicate)
                contacts.Add(translated);
        }
    }

    public void GetSphereContacts(Vector3 center, float radius, List<BodyContact> contacts)
    {
        gameContacts.Clear();
        level.GetRigidItemSphereContacts(ItemMath.ToGame(center), radius, gameContacts);
        AppendContacts(contacts);
    }

    private static BodyContact TranslateContact(RigidItemContact contact) =>
        new(ItemMath.ToLibrary(contact.Point), ItemMath.ToLibrary(contact.Normal), contact.Penetration);

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
