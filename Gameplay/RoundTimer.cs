namespace Gameplay;

internal sealed class RoundTimer
{
    private readonly float duration;

    public float TimeLeft { get; private set; }
    public bool IsRunning { get; private set; }
    public event Action? Elapsed;

    public RoundTimer(float durationSeconds = 60f) => duration = durationSeconds;

    public void Start()
    {
        TimeLeft = duration;
        IsRunning = true;
    }

    public void Update(float seconds)
    {
        if (!IsRunning)
            return;

        TimeLeft -= seconds;
        if (TimeLeft > 0f)
            return;

        TimeLeft = 0f;
        IsRunning = false;
        Elapsed?.Invoke();
    }

    public string Text => TimeSpan.FromSeconds(Math.Ceiling(TimeLeft)).ToString(@"mm\:ss");
}