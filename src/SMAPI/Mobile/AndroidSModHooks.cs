using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using StardewModdingAPI.Framework;
using StardewModdingAPI.Internal;
using StardewValley.Extensions;

namespace StardewModdingAPI.Mobile;

/// <summary>
/// Manages task scheduling for SMAPI on Android.
/// Tasks are queued and run on the main thread during game updates.
/// </summary>
internal static class AndroidSModHooks
{
    static IMonitor Monitor => SCore.Instance.SMAPIMonitor;

    internal static void Init()
    {
        AndroidGameLoopManager.RegisterOnGameUpdating(OnGameUpdating_TaskUpdate);
    }

    internal static bool OnGameUpdating_TaskUpdate(GameTime time)
    {
        bool markSkipGameUpdating = false;

        double runTaskOnMainThreadTotalTime = 0;
        int runTaskOnMainThreadCount = 0;
        while (queueTaskNeedToStartOnMainThread.TryDequeue(out var task))
        {
            bool shouldShowLogTask = task.name is not null;
            markSkipGameUpdating = true;
            var stopwatch = Stopwatch.StartNew();
            task.task.RunSynchronously();
            stopwatch.Stop();
            runTaskOnMainThreadCount++;
            runTaskOnMainThreadTotalTime += stopwatch.Elapsed.TotalMilliseconds;
            if (shouldShowLogTask)
            {
                Monitor.Log(
                    $"Done taskOnMainThread: '{task.name}' in {stopwatch.Elapsed.TotalMilliseconds}ms"
                );
            }

            if (runTaskOnMainThreadTotalTime > 2000)
            {
                Monitor.Log(
                    $"Main thread task '{task.name}' took {runTaskOnMainThreadTotalTime:F3}ms (exceeds 2000ms threshold)",
                    LogLevel.Warn
                );
            }

            // Limit to ~2 frames (32ms) to avoid blocking the game loop
            if (runTaskOnMainThreadTotalTime > 32)
            {
                break;
            }
        }

        lock (listTaskOnThreadBackground)
        {
            if (listTaskOnThreadBackground.Count > 0)
            {
                int removeCount = listTaskOnThreadBackground.RemoveAll(task => task.IsCompleted);
            }
            if (listTaskOnThreadBackground.Count > 0)
            {
                markSkipGameUpdating = true;
            }
        }

        return markSkipGameUpdating;
    }

    internal class TaskOnMainThread
    {
        public readonly string? name;
        public readonly Task task;

        public TaskOnMainThread(Task task, string? name)
        {
            this.task = task;
            this.name = name;
        }
    }

    static List<Task> listTaskOnThreadBackground = new();
    static ConcurrentQueue<TaskOnMainThread> queueTaskNeedToStartOnMainThread = new();

    internal static Task AddTaskRunOnMainThread(Action callback, string name) =>
        AddTaskRunOnMainThread(new Task(callback), name);

    internal static Task AddTaskRunOnMainThread(Task yourTask, string? taskName)
    {
        var taskOnMainThread = new TaskOnMainThread(yourTask, taskName);
        queueTaskNeedToStartOnMainThread.Enqueue(taskOnMainThread);
        return yourTask;
    }

    internal static Task StartTaskBackground(Action callback, string nameID)
    {
        return StartTaskBackground(new Task(callback), nameID);
    }

    internal static Task StartTaskBackground(Task gameTask, string nameID)
    {
        Monitor.Log($"Starting background task: '{nameID}'");

        var currentModHookTask = new Task(() =>
        {
            try
            {
                var st = Stopwatch.StartNew();
                Monitor.Log($"Starting Task On Background id: '{nameID}'");
                gameTask.RunSynchronously();
                st.Stop();
                Monitor.Log(
                    $"Completed Task On Background id: {nameID} in {st.Elapsed.TotalMilliseconds}ms"
                );
            }
            catch (Exception ex)
            {
                Monitor.Log($"Exception on task id: {nameID}");
                Monitor.Log($"{ex.GetLogSummary()}");
            }
        });

        lock (listTaskOnThreadBackground)
        {
            listTaskOnThreadBackground.Add(currentModHookTask);
        }

        currentModHookTask.Start();
        return currentModHookTask;
    }
}
