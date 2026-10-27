using _3DLight.Interaction;
using Microsoft.Xna.Framework;

internal static class WorldInteractionChecks
{
    public static void Run(Action<string, Action> run)
    {
        run("world interaction selects the nearest button and executes only a new press", () =>
        {
            int nearPresses = 0, farPresses = 0;
            var near = new WorldButton(new(new(-1f, -1f, -1f), new(1f, 1f, -0.9f)),
                new HashSet<int> { 1 }, () => nearPresses++);
            var far = new WorldButton(new(new(-1f, -1f, -2f), new(1f, 1f, -1.9f)),
                new HashSet<int> { 2 }, () => farPresses++);
            var interaction = new InteractionSystem((_, _, _) => true);
            interaction.Update([far, near], Vector3.Zero, Vector3.Forward, true, true);
            Require(nearPresses == 1 && farPresses == 0 && interaction.Target == near,
                "The nearest button was not selected");
            Require(interaction.Hint == "[E] НАЖАТЬ КНОПКУ", "The hint was hardcoded to doors or inventory");
            interaction.Update([near], Vector3.Zero, Vector3.Forward, false, true);
            Require(nearPresses == 1, "Holding the key repeated the button action");
            interaction.Update([near], Vector3.Zero, Vector3.Forward, true, false);
            Require(nearPresses == 1 && interaction.Target is null, "Paused input activated a button");
        });
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
