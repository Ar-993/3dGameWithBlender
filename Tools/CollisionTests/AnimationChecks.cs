using _3DLight;
using _3DLight.Assets;
using _3DLight.Models;
using Microsoft.Xna.Framework;
using NumericsMatrix = System.Numerics.Matrix4x4;
using NumericsVector = System.Numerics.Vector3;
using NumericsQuaternion = System.Numerics.Quaternion;

internal static class AnimationChecks
{
    public static void Run(Action<string, Action> run)
    {
        run("sampler interpolates translation, rotation and scale with bind fallbacks", () =>
        {
            ModelData data = Fixture();
            var sampler = new AnimationSampler(data.Nodes, data.Clips);
            var pose = new BonePose[sampler.NodeCount];
            sampler.SamplePose("lInEaR", 1f, false, pose);
            Near(pose[0].Position.X, 20f);
            Near(pose[0].Scale.X, 2f);
            Vector3 rotated = Vector3.Transform(Vector3.UnitX, pose[0].Rotation);
            Near(rotated.X, MathF.Sqrt(0.5f));
            Near(rotated.Y, MathF.Sqrt(0.5f));
            Require(pose[1].Position == new Vector3(0f, 2f, 0f), "Missing keys lost bind position");
            pose[2] = new BonePose(Vector3.One, Quaternion.Identity, Vector3.One);
            sampler.SamplePose("Linear", 1f, false, pose);
            Require(pose[2].Position == Vector3.Zero, "Missing channel retained a previous pose");
        });

        run("sampler clamps completed clips and wraps looping clips", () =>
        {
            ModelData data = Fixture();
            var sampler = new AnimationSampler(data.Nodes, data.Clips);
            var pose = new BonePose[sampler.NodeCount];
            Near(sampler.GetClipDuration("Linear"), 2f);
            sampler.SamplePose("Linear", 3f, false, pose);
            Near(pose[0].Position.X, 30f);
            sampler.SamplePose("Linear", 2.5f, true, pose);
            Near(pose[0].Position.X, 15f);
            sampler.SamplePose("Linear", 2f, true, pose);
            Near(pose[0].Position.X, 10f);
        });

        run("sampler rejects unknown clips and incorrectly sized pose buffers", () =>
        {
            ModelData data = Fixture();
            var sampler = new AnimationSampler(data.Nodes, data.Clips);
            Throws<InvalidOperationException>(() => sampler.SamplePose("Missing", 0f, false, new BonePose[3]));
            Throws<ArgumentException>(() => sampler.SamplePose("Idle", 0f, false, new BonePose[2]));
        });

        run("static models do not require animation bind pose decomposition", () =>
        {
            var shear = NumericsMatrix.Identity;
            shear.M12 = 3f;
            var sampler = new AnimationSampler(new[] { new NodeData("Static", -1, shear) }, Array.Empty<ClipData>());
            Require(sampler.NodeCount == 1 && !sampler.ClipNames.Any(), "Static model metadata changed");
        });

        run("animator preserves the current pose when a transition is interrupted", () =>
        {
            ModelData data = Fixture();
            var sampler = new AnimationSampler(data.Nodes, data.Clips);
            var animator = new CharacterAnimator(sampler, new ModelHierarchy(data), "Idle");
            var pose = new BonePose[sampler.NodeCount];
            animator.Play("Run", blendDuration: 1f);
            animator.Update(0.5f);
            animator.CopyPoseTo(pose);
            Near(pose[0].Position.X, 5f);
            animator.Play("Jump", blendDuration: 1f);
            animator.CopyPoseTo(pose);
            Near(pose[0].Position.X, 5f);
            animator.Update(0.5f);
            animator.CopyPoseTo(pose);
            Near(pose[0].Position.X, 12.5f);
            animator.Update(0.5f);
            animator.CopyPoseTo(pose);
            Near(pose[0].Position.X, 20f);
            Throws<ArgumentException>(() => animator.CopyPoseTo(new BonePose[2]));
        });

        run("animator clamps and loops within a selected clip segment", () =>
        {
            ModelData data = Fixture();
            var sampler = new AnimationSampler(data.Nodes, data.Clips);
            var animator = new CharacterAnimator(sampler, new ModelHierarchy(data), "Idle");
            var pose = new BonePose[sampler.NodeCount];
            animator.PlaySegment("Segment", "Linear", 0.25f, 0.75f, loop: false, blendDuration: 0f);
            animator.CopyPoseTo(pose);
            Near(pose[0].Position.X, 15f);
            animator.Update(2f);
            animator.CopyPoseTo(pose);
            Near(pose[0].Position.X, 25f);
            animator.PlaySegment("Segment", "Linear", 0.25f, 0.75f, loop: true, blendDuration: 0f, restart: true);
            animator.Update(2.5f);
            animator.CopyPoseTo(pose);
            Near(pose[0].Position.X, 20f);
        });

        run("upper body overlay affects its subtree and fades back to the base pose", () =>
        {
            ModelData data = Fixture();
            var sampler = new AnimationSampler(data.Nodes, data.Clips);
            var animator = new CharacterAnimator(sampler, new ModelHierarchy(data), "Idle");
            var pose = new BonePose[sampler.NodeCount];
            animator.SetUpperBodyOverlay("Attack", "Arm", 0f);
            animator.Update(0.08f);
            animator.CopyPoseTo(pose);
            Near(pose[0].Position.X, 0f);
            Near(pose[1].Position.X, 8f);
            Near(pose[2].Position.X, 0f);
            animator.ClearUpperBodyOverlay();
            animator.Update(0.08f);
            animator.CopyPoseTo(pose);
            Near(pose[1].Position.X, 0f);
        });
    }

    private static ModelData Fixture()
    {
        var data = new ModelData();
        data.Nodes.Add(new NodeData("Root", -1, NumericsMatrix.CreateTranslation(10f, 0f, 0f)));
        data.Nodes.Add(new NodeData("Arm", 0, NumericsMatrix.CreateTranslation(0f, 2f, 0f)));
        data.Nodes.Add(new NodeData("Leg", 0, NumericsMatrix.Identity));

        var linear = new ClipData { Name = "Linear", Duration = 20, TicksPerSecond = 10 };
        linear.Channels.Add(new ChannelData
        {
            Node = 0,
            Positions = [new VectorKey(0, new NumericsVector(10f, 0f, 0f)), new VectorKey(20, new NumericsVector(30f, 0f, 0f))],
            Rotations = [new QuaternionKey(0, NumericsQuaternion.Identity), new QuaternionKey(20, NumericsQuaternion.CreateFromAxisAngle(NumericsVector.UnitZ, MathF.PI / 2f))],
            Scales = [new VectorKey(0, NumericsVector.One), new VectorKey(20, new NumericsVector(3f))]
        });
        linear.Channels.Add(new ChannelData { Node = 1 });
        data.Clips.Add(linear);
        data.Clips.Add(ConstantClip("Idle", 0, NumericsVector.Zero));
        data.Clips.Add(ConstantClip("Run", 0, new NumericsVector(10f, 0f, 0f)));
        data.Clips.Add(ConstantClip("Jump", 0, new NumericsVector(20f, 0f, 0f)));
        data.Clips.Add(ConstantClip("Attack", 1, new NumericsVector(8f, 2f, 0f)));
        return data;
    }

    private static ClipData ConstantClip(string name, int node, NumericsVector position)
    {
        var clip = new ClipData { Name = name, Duration = 20, TicksPerSecond = 10 };
        clip.Channels.Add(new ChannelData { Node = node, Positions = [new VectorKey(0, position)] });
        return clip;
    }

    private static void Near(float actual, float expected)
    {
        Require(MathF.Abs(actual - expected) <= 0.0001f, $"Expected {expected}, got {actual}");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void Throws<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (T) { return; }
        throw new InvalidOperationException($"Expected {typeof(T).Name}");
    }
}
