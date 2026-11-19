using _3DLight.Interaction;
using Microsoft.Xna.Framework;

namespace _3DLight.Npcs;

internal sealed class NpcDialogue
{
    public string Name { get; init; } = "";
    public string[] Lines { get; init; } = [];

    public void Validate(string npcId)
    {
        if (string.IsNullOrWhiteSpace(Name))
            throw new InvalidDataException($"У диалога NPC '{npcId}' нужно указать name.");
        if (Lines is not { Length: > 0 } || Array.Exists(Lines, string.IsNullOrWhiteSpace))
            throw new InvalidDataException($"У диалога NPC '{npcId}' нужны заполненные lines.");
    }
}

internal sealed class DialogueSession
{
    private NpcController? speaker;
    private NpcDialogue? dialogue;
    private int lineIndex;

    public bool IsOpen => speaker is not null;
    public string SpeakerName => dialogue?.Name ?? "";
    public string Line => dialogue is null ? "" : dialogue.Lines[lineIndex];
    public Vector3? SpeakerAnchor => speaker?.DialogueAnchor;

    public void Open(NpcController actor, NpcDialogue script)
    {
        actor.BeginDialogue();
        speaker = actor;
        dialogue = script;
        lineIndex = 0;
    }

    public void Update(float seconds, bool advance, bool cancel)
    {
        if (speaker is null)
            return;

        if (cancel || advance && lineIndex == dialogue!.Lines.Length - 1)
        {
            Close();
            return;
        }

        if (advance)
            lineIndex++;

        speaker.UpdateDialogue(seconds);
    }

    public void Close()
    {
        speaker?.EndDialogue();
        speaker = null;
        dialogue = null;
        lineIndex = 0;
    }
}

internal sealed class NpcTalkTarget(
    NpcController actor, NpcDialogue script, DialogueSession session) : IInteractable
{
    private static readonly IReadOnlySet<int> NoMeshIds = new HashSet<int>();

    public string InteractionText => "ГОВОРИТЬ";
    public float InteractionDistance => 2.5f;
    public IReadOnlySet<int> MeshIds => NoMeshIds;
    public Vector3 BubbleAnchor => actor.DialogueAnchor;

    public float? GetHitDistance(Ray ray) => ray.Intersects(actor.Capsule.Bounds);

    public void Interact() => session.Open(actor, script);
}
