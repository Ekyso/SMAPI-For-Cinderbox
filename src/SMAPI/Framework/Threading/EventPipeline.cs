using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using Microsoft.Xna.Framework;

namespace StardewModdingAPI.Framework.Threading;

/// <summary>A producer-consumer event pipeline that processes mod events on background threads.</summary>
internal sealed class EventPipeline : IDisposable
{
    private readonly ConcurrentQueue<GameEvent> _gameEvents = new();
    private readonly ConcurrentQueue<ModResult> _modResults = new();
    private readonly Thread[] _workers;
    private readonly CancellationTokenSource _cts = new();
    private readonly Action<GameEvent> _processEvent;
    private volatile bool _disposed;

    /// <summary>Calculate optimal worker thread count based on device CPU cores.</summary>
    /// <param name="configuredCount">User-configured count (0 = auto).</param>
    public static int CalculateOptimalWorkerCount(int configuredCount = 0)
    {
        if (configuredCount > 0)
            return Math.Clamp(configuredCount, 1, 8);

        // reserve 2 cores for game thread and system, cap at 4
        int coreCount = Environment.ProcessorCount;
        return Math.Clamp(coreCount - 2, 1, 4);
    }

    /// <summary>Construct an instance.</summary>
    /// <param name="processEvent">Action to process events on worker threads.</param>
    /// <param name="configuredWorkerCount">Configured worker count (0 = auto).</param>
    public EventPipeline(Action<GameEvent> processEvent, int configuredWorkerCount = 0)
    {
        _processEvent = processEvent;

        int actualWorkers = CalculateOptimalWorkerCount(configuredWorkerCount);
        int coreCount = Environment.ProcessorCount;

        System.Diagnostics.Debug.WriteLine(
            $"[EventPipeline] Device has {coreCount} CPU cores, using {actualWorkers} worker threads");

        _workers = new Thread[actualWorkers];
        for (int i = 0; i < actualWorkers; i++)
        {
            _workers[i] = new Thread(WorkerLoop)
            {
                IsBackground = true,
                Name = $"SMAPI-EventWorker-{i}",
                Priority = ThreadPriority.BelowNormal
            };
            _workers[i].Start();
        }
    }

    /// <summary>Gets the number of worker threads.</summary>
    public int WorkerCount => _workers.Length;

    /// <summary>Gets the device CPU core count.</summary>
    public static int DeviceCoreCount => Environment.ProcessorCount;

    /// <summary>Enqueue a game event for background processing.</summary>
    public void EnqueueEvent(GameEvent evt)
    {
        if (!_disposed)
            _gameEvents.Enqueue(evt);
    }

    /// <summary>Enqueue a result to be applied on the game thread.</summary>
    public void EnqueueResult(ModResult result)
    {
        if (!_disposed)
            _modResults.Enqueue(result);
    }

    /// <summary>Apply pending results on the game thread.</summary>
    /// <param name="maxPerFrame">Maximum results to apply per frame.</param>
    /// <returns>Number of results applied.</returns>
    public int ApplyResults(int maxPerFrame = 20)
    {
        int applied = 0;
        while (applied < maxPerFrame && _modResults.TryDequeue(out var result))
        {
            try
            {
                result.Apply();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[EventPipeline] Error applying result: {ex.Message}");
            }
            applied++;
        }
        return applied;
    }

    /// <summary>Gets the number of pending game events.</summary>
    public int PendingEvents => _gameEvents.Count;

    /// <summary>Gets the number of pending results to apply.</summary>
    public int PendingResults => _modResults.Count;

    private void WorkerLoop()
    {
        while (!_cts.IsCancellationRequested)
        {
            if (_gameEvents.TryDequeue(out var evt))
            {
                try
                {
                    _processEvent(evt);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[EventPipeline] Worker error: {ex.Message}");
                }
            }
            else
            {
                // brief sleep to avoid spinning
                Thread.Sleep(1);
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _cts.Cancel();

        foreach (var worker in _workers)
        {
            if (worker.IsAlive)
                worker.Join(TimeSpan.FromSeconds(2));
        }

        _cts.Dispose();
    }
}

/// <summary>Base class for game events that can be processed on worker threads.</summary>
internal abstract class GameEvent
{
    /// <summary>The tick number when this event was created.</summary>
    public uint TickNumber { get; init; }

    /// <summary>The game time when this event was created.</summary>
    public GameTime GameTime { get; init; } = null!;

    /// <summary>Timestamp when this event was enqueued (for measuring queue latency).</summary>
    public long EnqueuedTimestamp { get; init; }
}

/// <summary>Event raised before the game updates.</summary>
internal sealed class UpdateTickingEvent : GameEvent
{
    /// <summary>Snapshot of game state for thread-safe access.</summary>
    public GameStateSnapshot Snapshot { get; init; } = null!;
}

/// <summary>Event raised after the game updates.</summary>
internal sealed class UpdateTickedEvent : GameEvent
{
    /// <summary>Snapshot of game state for thread-safe access.</summary>
    public GameStateSnapshot Snapshot { get; init; } = null!;
}

/// <summary>Base class for results from mod event handlers to be applied on the game thread.</summary>
internal abstract class ModResult
{
    /// <summary>The mod ID that created this result.</summary>
    public string ModId { get; init; } = "";

    /// <summary>Apply this result on the game thread.</summary>
    public abstract void Apply();
}

/// <summary>A result that executes an action on the game thread.</summary>
internal sealed class ActionResult : ModResult
{
    /// <summary>The action to execute on the game thread.</summary>
    public Action GameThreadAction { get; init; } = null!;

    public override void Apply() => GameThreadAction();
}

/// <summary>A result that records performance metrics.</summary>
internal sealed class MetricsResult : ModResult
{
    /// <summary>The event type that was processed.</summary>
    public string EventType { get; init; } = "";

    /// <summary>Queue latency in milliseconds (time from enqueue to dequeue).</summary>
    public double QueueLatencyMs { get; init; }

    /// <summary>Processing time in milliseconds.</summary>
    public double ProcessingTimeMs { get; init; }

    /// <summary>The tick number when this event was processed.</summary>
    public uint TickNumber { get; init; }

    public override void Apply()
    {
        EventPipelineMetrics.RecordEvent(EventType, QueueLatencyMs, ProcessingTimeMs);
    }
}

/// <summary>Tracks performance metrics for the event pipeline.</summary>
internal static class EventPipelineMetrics
{
    private static readonly object _lock = new();
    private static double _totalQueueLatencyMs;
    private static double _totalProcessingTimeMs;
    private static int _eventsProcessed;
    private static double _maxQueueLatencyMs;
    private static double _maxProcessingTimeMs;

    /// <summary>Record metrics for a processed event.</summary>
    public static void RecordEvent(string eventType, double queueLatencyMs, double processingTimeMs)
    {
        lock (_lock)
        {
            _totalQueueLatencyMs += queueLatencyMs;
            _totalProcessingTimeMs += processingTimeMs;
            _eventsProcessed++;
            _maxQueueLatencyMs = Math.Max(_maxQueueLatencyMs, queueLatencyMs);
            _maxProcessingTimeMs = Math.Max(_maxProcessingTimeMs, processingTimeMs);
        }
    }

    /// <summary>Get average queue latency in milliseconds.</summary>
    public static double AverageQueueLatencyMs
    {
        get
        {
            lock (_lock)
            {
                return _eventsProcessed > 0 ? _totalQueueLatencyMs / _eventsProcessed : 0;
            }
        }
    }

    /// <summary>Get average processing time in milliseconds.</summary>
    public static double AverageProcessingTimeMs
    {
        get
        {
            lock (_lock)
            {
                return _eventsProcessed > 0 ? _totalProcessingTimeMs / _eventsProcessed : 0;
            }
        }
    }

    /// <summary>Get the total number of events processed.</summary>
    public static int EventsProcessed
    {
        get
        {
            lock (_lock)
            {
                return _eventsProcessed;
            }
        }
    }

    /// <summary>Get max queue latency in milliseconds.</summary>
    public static double MaxQueueLatencyMs
    {
        get
        {
            lock (_lock)
            {
                return _maxQueueLatencyMs;
            }
        }
    }

    /// <summary>Get a metrics summary string.</summary>
    public static string GetSummary()
    {
        lock (_lock)
        {
            return $"Events: {_eventsProcessed}, AvgQueue: {AverageQueueLatencyMs:F2}ms, AvgProc: {AverageProcessingTimeMs:F2}ms, MaxQueue: {_maxQueueLatencyMs:F2}ms";
        }
    }

    /// <summary>Reset all metrics.</summary>
    public static void Reset()
    {
        lock (_lock)
        {
            _totalQueueLatencyMs = 0;
            _totalProcessingTimeMs = 0;
            _eventsProcessed = 0;
            _maxQueueLatencyMs = 0;
            _maxProcessingTimeMs = 0;
        }
    }
}
