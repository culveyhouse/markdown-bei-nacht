using System.IO;
using System.Diagnostics;
using System.Text.Json;
using MarkdownBeiNacht.Core.Models;
using MarkdownBeiNacht.Core.Services;

namespace MarkdownBeiNacht.Infrastructure;

public sealed class WindowPlacementCoordinator
{
    private const string PlacementMutexName = @"Local\MarkdownBeiNacht.WindowPlacement";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly string _stateFilePath;

    public WindowPlacementCoordinator(string stateFilePath)
    {
        _stateFilePath = stateFilePath;
    }

    public WindowPlacement? ResolveStartupPlacement(
        StartupOptions startupOptions,
        double windowWidth,
        double windowHeight,
        double cascadeOffset,
        double virtualLeft,
        double virtualTop,
        double virtualWidth,
        double virtualHeight)
    {
        if (startupOptions.HasWindowPlacement && startupOptions.WindowLeft is not null && startupOptions.WindowTop is not null)
        {
            var explicitPlacement = WindowPlacementPlanner.Clamp(
                new WindowPlacement(startupOptions.WindowLeft.Value, startupOptions.WindowTop.Value, windowWidth, windowHeight),
                virtualLeft,
                virtualTop,
                virtualWidth,
                virtualHeight);
            RememberWindowPlacement(explicitPlacement);
            return explicitPlacement;
        }

        if (HasOtherRunningInstances() is false)
        {
            return null;
        }

        return WithMutex(() =>
        {
            var previousPlacement = LoadUnsafe();
            if (previousPlacement is null)
            {
                return null;
            }

            var nextPlacement = WindowPlacementPlanner.Cascade(
                new WindowPlacement(previousPlacement.Left, previousPlacement.Top, windowWidth, windowHeight),
                cascadeOffset,
                virtualLeft,
                virtualTop,
                virtualWidth,
                virtualHeight);
            SaveUnsafe(nextPlacement);
            return nextPlacement;
        });
    }

    public void RememberWindowPlacement(WindowPlacement placement)
    {
        WithMutex(() =>
        {
            SaveUnsafe(placement);
            return true;
        });
    }

    private bool HasOtherRunningInstances()
    {
        var currentProcessName = Process.GetCurrentProcess().ProcessName;
        var currentProcessId = Environment.ProcessId;
        foreach (var process in Process.GetProcessesByName(currentProcessName))
        {
            using (process)
            {
                if (process.Id != currentProcessId)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private WindowPlacement? LoadUnsafe()
    {
        if (File.Exists(_stateFilePath) is false)
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(_stateFilePath);
            return JsonSerializer.Deserialize<WindowPlacement>(json, JsonOptions);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
            return null;
        }
    }

    private void SaveUnsafe(WindowPlacement placement)
    {
        var directory = Path.GetDirectoryName(_stateFilePath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }

        Directory.CreateDirectory(directory);
        var tempPath = _stateFilePath + ".tmp";
        var json = JsonSerializer.Serialize(placement, JsonOptions);
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, _stateFilePath, true);
    }

    private static T? WithLockFallback<T>(Func<T?> action)
    {
        return action();
    }

    private T? WithMutex<T>(Func<T?> action)
    {
        using var mutex = new Mutex(false, PlacementMutexName);
        var lockTaken = false;
        try
        {
            try
            {
                lockTaken = mutex.WaitOne(TimeSpan.FromSeconds(2));
            }
            catch (AbandonedMutexException)
            {
                lockTaken = true;
            }

            return lockTaken ? action() : WithLockFallback(action);
        }
        finally
        {
            if (lockTaken)
            {
                mutex.ReleaseMutex();
            }
        }
    }
}

