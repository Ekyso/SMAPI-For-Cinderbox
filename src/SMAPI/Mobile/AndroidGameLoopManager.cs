using System;
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

    private static PerformanceMetricsSession? _performanceMetrics;

    /// <summary>Begin timing a new frame.</summary>
    internal static void BeginFrame()
    {
        _performanceMetrics?.BeginFrame();
    }

    /// <summary>Mark the update phase as complete and record timing.</summary>
    internal static void MarkUpdateComplete()
    {
        _performanceMetrics?.MarkUpdateComplete();
    }

    /// <summary>Mark the complete frame and record its total duration.</summary>
    internal static void MarkFrameComplete()
    {
        _performanceMetrics?.MarkFrameComplete();
    }

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
            while (queueOnGameUpdatingToAdd.TryDequeue(out OnGameUpdatingDelegate? item))
            {
                listOnGameUpdating.Add(item);
            }
        }

        if (queueOnGameUpdatingToRemove.Count > 0)
        {
            while (queueOnGameUpdatingToRemove.TryDequeue(out OnGameUpdatingDelegate? item))
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

    static float KbToMB(this long val) => (float)val / (1024f * 1024f);

    /// <summary>Enable periodic performance metrics logging.</summary>
    /// <param name="monitor">The monitor to log to.</param>
    internal static void EnablePerformanceLogging(IMonitor monitor)
    {
        _performanceMetrics = new PerformanceMetricsSession(monitor);
    }

    private sealed class PerformanceMetricsSession
    {
        private const int FrameWindowSize = 60;
        private const double LogIntervalMilliseconds = 60000;

        private readonly IMonitor _monitor;
        private readonly Stopwatch _frameTimer = new();
        private readonly Stopwatch _logTimer = Stopwatch.StartNew();
        private readonly Queue<double> _recentFrameTimes = new(FrameWindowSize);
        private double _lastUpdateMilliseconds;
        private double _lastFrameMilliseconds;

        public PerformanceMetricsSession(IMonitor monitor)
        {
            _monitor = monitor;
        }

        public void BeginFrame()
        {
            _frameTimer.Restart();
        }

        public void MarkUpdateComplete()
        {
            _lastUpdateMilliseconds = _frameTimer.Elapsed.TotalMilliseconds;
        }

        public void MarkFrameComplete()
        {
            _lastFrameMilliseconds = _frameTimer.Elapsed.TotalMilliseconds;
            _recentFrameTimes.Enqueue(_lastFrameMilliseconds);
            while (_recentFrameTimes.Count > FrameWindowSize)
                _recentFrameTimes.Dequeue();

            TryLog();
        }

        private void TryLog()
        {
            if (_logTimer.Elapsed.TotalMilliseconds < LogIntervalMilliseconds)
                return;

            _logTimer.Restart();

            double averageFrameMilliseconds =
                _recentFrameTimes.Count > 0 ? _recentFrameTimes.Average() : 0;
            var log = new StringBuilder();
            log.AppendLine("[Performance Metrics]");
            log.AppendLine(
                $"  Frame Timing: Update: {_lastUpdateMilliseconds:F1}ms, Frame: {_lastFrameMilliseconds:F1}ms, Avg frame: {averageFrameMilliseconds:F1}ms"
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
            catch { }

            _monitor.Log(log.ToString().TrimEnd(), LogLevel.Info);
        }
    }
}
