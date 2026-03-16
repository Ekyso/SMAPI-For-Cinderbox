using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using Android.App;
using Android.OS;
using Microsoft.Xna.Framework;
using StardewModdingAPI.Framework;
using StardewValley;

namespace StardewModdingAPI.Mobile;

/// <summary>Manages Android-specific game loop callbacks and timing fixes.</summary>
internal static class AndroidGameLoopManager
{
    /// <summary>Cached field reference for _accumulatedElapsedTime in Game class.</summary>
    private static FieldInfo? _accumulatedElapsedTimeField;
    internal delegate bool OnGameUpdatingDelegate(GameTime gameTime);
    static HashSet<OnGameUpdatingDelegate> listOnGameUpdating = new();
    static Queue<OnGameUpdatingDelegate> queueOnGameUpdatingToAdd = new();
    static Queue<OnGameUpdatingDelegate> queueOnGameUpdatingToRemove = new();

    // Frame timing infrastructure
    private static readonly Stopwatch _frameTimer = new();
    private static double _lastUpdateMs;
    private static double _lastRenderMs;
    private static readonly Queue<double> _recentFrameTimes = new(60);

    private const double TargetFrameMs = 16.67; // 60fps
    private const double UpdateBudgetMs = 10.0;

    /// <summary>Average frame time in milliseconds over the last 60 frames.</summary>
    public static double AverageFrameMs =>
        _recentFrameTimes.Count > 0 ? _recentFrameTimes.Average() : 0;

    /// <summary>Last update phase duration in milliseconds.</summary>
    public static double LastUpdateMs => _lastUpdateMs;

    /// <summary>Last render phase duration in milliseconds.</summary>
    public static double LastRenderMs => _lastRenderMs;

    /// <summary>True if the last update exceeded the budget.</summary>
    public static bool IsOverBudget => _lastUpdateMs > UpdateBudgetMs;

    /// <summary>Begin timing a new frame.</summary>
    internal static void BeginFrame()
    {
        _frameTimer.Restart();
    }

    /// <summary>Mark the update phase as complete and record timing.</summary>
    internal static void MarkUpdateComplete()
    {
        _lastUpdateMs = _frameTimer.Elapsed.TotalMilliseconds;
    }

    /// <summary>Mark the render phase as complete and record timing.</summary>
    internal static void MarkRenderComplete()
    {
        _lastRenderMs = _frameTimer.Elapsed.TotalMilliseconds;
        _recentFrameTimes.Enqueue(_lastRenderMs);
        while (_recentFrameTimes.Count > 60)
            _recentFrameTimes.Dequeue();

        TryLogPerformanceMetrics();
    }

    /// <summary>Get a summary of frame timing metrics.</summary>
    internal static string GetMetricsSummary() =>
        $"Update: {_lastUpdateMs:F1}ms, Render: {_lastRenderMs:F1}ms, Avg: {AverageFrameMs:F1}ms";

    /// <summary>Register a callback to run during game updates. Must be called from the main thread.</summary>
    internal static void RegisterOnGameUpdating(OnGameUpdatingDelegate onGameUpdate)
    {
        queueOnGameUpdatingToAdd.Enqueue(onGameUpdate);
    }

    /// <summary>Unregister a game update callback. Must be called from the main thread.</summary>
    internal static void UnregisterOnGameUpdating(OnGameUpdatingDelegate onGameUpdate)
    {
        queueOnGameUpdatingToRemove.Enqueue(onGameUpdate);
    }

    public static bool IsSkipOriginalGameUpdating { get; private set; } = false;

    internal static void UpdateFrame_OnGameUpdating(GameTime gameTime)
    {
        IsSkipOriginalGameUpdating = false;

        if (queueOnGameUpdatingToAdd.Count > 0)
        {
            while (queueOnGameUpdatingToAdd.TryDequeue(out OnGameUpdatingDelegate item))
            {
                listOnGameUpdating.Add(item);
            }
        }

        if (queueOnGameUpdatingToRemove.Count > 0)
        {
            while (queueOnGameUpdatingToRemove.TryDequeue(out OnGameUpdatingDelegate item))
            {
                listOnGameUpdating.Remove(item);
            }
        }

        foreach (var callback in listOnGameUpdating)
        {
            if (callback(gameTime))
            {
                IsSkipOriginalGameUpdating = true;
            }
        }
    }

    /// <summary>Reset accumulated elapsed time if it exceeds 0.15s to prevent update freeze loops.</summary>
    internal static void ApplyTimingFix()
    {
        var game = SGameRunner.instance as Game;
        if (game == null)
            return;

        // Cache the field reference for performance
        _accumulatedElapsedTimeField ??= game.GetType()
            .GetField("_accumulatedElapsedTime", BindingFlags.Instance | BindingFlags.NonPublic);

        if (_accumulatedElapsedTimeField == null)
            return;

        var accumulatedElapsedTime = (TimeSpan?)_accumulatedElapsedTimeField.GetValue(game);
        if (accumulatedElapsedTime == null)
            return;

        if (accumulatedElapsedTime.Value.TotalSeconds > 0.15f)
        {
            _accumulatedElapsedTimeField.SetValue(game, TimeSpan.FromSeconds(0f));
        }
    }

    static Stopwatch TimerLogMemory = Stopwatch.StartNew();

    private static void PrintMemory()
    {
        const int refreshTime = 1000;
        if (TimerLogMemory.Elapsed.TotalMilliseconds < refreshTime)
            return;

        TimerLogMemory.Restart();

        var mainActivity = SMAPIActivityTool.MainActivity;
        ActivityManager activityManager =
            mainActivity.GetSystemService(Service.ActivityService) as ActivityManager;
        var memoryInfo = new ActivityManager.MemoryInfo();
        activityManager.GetMemoryInfo(memoryInfo);

        StringBuilder log = new();
        log.AppendLine(" Log Mem Info (Android):");
        log.AppendLine($"Total Mem: {memoryInfo.TotalMem.KbToMB():F3} MB");
        log.AppendLine($"Available  Mem: {memoryInfo.AvailMem.KbToMB():F3} MB");
        log.AppendLine($"Is Low Mem: {memoryInfo.LowMemory}");
        var monitor = SCore.Instance?.SMAPIMonitor;
        if (monitor != null)
            monitor.Log(log.ToString(), LogLevel.Trace);
    }

    static float KbToMB(this long val) => (float)val / (1024f * 1024f);

    // Performance logging infrastructure
    private static bool _performanceLoggingEnabled;
    private static IMonitor? _performanceMonitor;
    private static readonly Stopwatch _performanceLogTimer = new();
    private const double PerformanceLogIntervalMs = 60000; // 60 seconds

    /// <summary>Enable periodic performance metrics logging.</summary>
    /// <param name="monitor">The monitor to log to.</param>
    internal static void EnablePerformanceLogging(IMonitor monitor)
    {
        _performanceLoggingEnabled = true;
        _performanceMonitor = monitor;
        _performanceLogTimer.Restart();
    }

    /// <summary>Log performance metrics if enabled and interval has elapsed.</summary>
    internal static void TryLogPerformanceMetrics()
    {
        if (!_performanceLoggingEnabled || _performanceMonitor == null)
            return;

        if (_performanceLogTimer.Elapsed.TotalMilliseconds < PerformanceLogIntervalMs)
            return;

        _performanceLogTimer.Restart();

        var log = new StringBuilder();
        log.AppendLine("[Performance Metrics]");
        log.AppendLine($"  Frame Timing: {GetMetricsSummary()}");
        log.AppendLine(
            $"  Event Pipeline: {StardewModdingAPI.Framework.Threading.EventPipelineMetrics.GetSummary()}"
        );

        try
        {
            var mainActivity = SMAPIActivityTool.MainActivity;
            if (mainActivity != null)
            {
                ActivityManager? activityManager =
                    mainActivity.GetSystemService(Service.ActivityService) as ActivityManager;
                if (activityManager != null)
                {
                    var memoryInfo = new ActivityManager.MemoryInfo();
                    activityManager.GetMemoryInfo(memoryInfo);
                    log.AppendLine(
                        $"  Memory: {memoryInfo.AvailMem.KbToMB():F1}MB available / {memoryInfo.TotalMem.KbToMB():F1}MB total{(memoryInfo.LowMemory ? " [LOW]" : "")}"
                    );
                }
            }
        }
        catch
        {
        }

        _performanceMonitor.Log(log.ToString().TrimEnd(), LogLevel.Info);

        StardewModdingAPI.Framework.Threading.EventPipelineMetrics.Reset();
    }
}
