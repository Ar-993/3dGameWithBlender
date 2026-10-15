using System.Diagnostics;

namespace _3DLight.Gameplay;

internal sealed class PerformanceMetrics
{
    private long sampleStarted = Stopwatch.GetTimestamp();
    private int framesInSample;

    public double Fps { get; private set; }
    public double WorkingSetMegabytes { get; private set; }

    public void RecordDraw()
    {
        const double sampleDurationSeconds = 0.5;

        framesInSample++;
        long now = Stopwatch.GetTimestamp();
        double elapsedSeconds = Stopwatch.GetElapsedTime(sampleStarted, now).TotalSeconds;
        if (elapsedSeconds >= sampleDurationSeconds)
        {
            Fps = framesInSample / elapsedSeconds;
            framesInSample = 0;
            sampleStarted = now;
        }

        WorkingSetMegabytes = Environment.WorkingSet / (1024d * 1024d);
    }
}
