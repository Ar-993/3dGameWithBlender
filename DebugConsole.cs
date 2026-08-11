using Microsoft.Xna.Framework;
using System;
using System.Runtime.InteropServices;

internal static class DebugConsole
{
    private const double RefreshIntervalSeconds = 0.1;
    private static double elapsedSinceRefresh;
    private static int previousLineLength;
    private static bool isOpen;

    public static void Open()
    {
        if (!OperatingSystem.IsWindows() || !AllocConsole())
            return;

        isOpen = true;
        Console.Title = "Ars3D Debug";
        Console.WriteLine("Координаты персонажей (обновление 10 раз/с):");
    }

    public static void Update(GameTime gameTime, Player player, Skeleton skeleton)
    {
        if (!isOpen)
            return;

        elapsedSinceRefresh += gameTime.ElapsedGameTime.TotalSeconds;
        if (elapsedSinceRefresh < RefreshIntervalSeconds)
            return;

        elapsedSinceRefresh = 0;
        string playerPlatform = player.CurrentPlatform?.Id.ToString() ?? "AIR";
        string skeletonPlatform = skeleton.CurrentPlatform?.Id.ToString() ?? "AIR";
        string line =
            $"PLAYER {Format(player.Position)} [{playerPlatform}]  " +
            $"SKELETON {Format(skeleton.Position)} [{skeletonPlatform}]";

        Console.Write('\r');
        Console.Write(line.PadRight(previousLineLength));
        previousLineLength = line.Length;
    }

    public static void Close()
    {
        if (!isOpen)
            return;

        Console.WriteLine();
        FreeConsole();
        isOpen = false;
    }

    private static string Format(Vector3 position) =>
        $"X:{position.X,8:F2} Y:{position.Y,8:F2} Z:{position.Z,8:F2}";

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AllocConsole();

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FreeConsole();
}
