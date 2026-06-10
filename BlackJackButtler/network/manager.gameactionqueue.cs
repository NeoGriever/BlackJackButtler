using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using BlackJackButtler.Chat;

namespace BlackJackButtler;

public static class GameActionQueueManager
{
    private sealed record QueueItem(
        string Context,
        string Key,
        int Generation,
        Func<bool>? IsValid,
        Func<Task> Action,
        Action? OnCompleted);

    private static readonly ConcurrentQueue<QueueItem> Queue = new();
    private static readonly HashSet<string> PendingKeys = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object Gate = new();
    private static int _isProcessing;
    private static int _activeContinuations;
    private static int _generation;
    private static bool _suspended;
    private static string _currentContext = string.Empty;

    public static bool IsBusy => Volatile.Read(ref _isProcessing) != 0;
    public static bool IsSuspended
    {
        get { lock (Gate) return _suspended; }
    }
    public static int PendingCount => Queue.Count;
    public static string CurrentContext => _currentContext;
    public static int Generation => Volatile.Read(ref _generation);

    public static bool Enqueue(
        string context,
        Func<Task> action,
        string? key = null,
        Func<bool>? isValid = null,
        Action? onCompleted = null)
    {
        var normalizedKey = string.IsNullOrWhiteSpace(key) ? string.Empty : key.Trim();
        lock (Gate)
        {
            if (_suspended)
            {
                Log($"Rejected enqueue '{context}': queue suspended");
                return false;
            }

            if (normalizedKey.Length > 0 && !PendingKeys.Add(normalizedKey))
            {
                Log($"Rejected enqueue '{context}': duplicate key '{normalizedKey}'");
                return false;
            }

            Queue.Enqueue(new QueueItem(context, normalizedKey, Generation, isValid, action, onCompleted));
            Log($"Enqueued '{context}' | Key='{normalizedKey}' | Generation={Generation} | Pending={Queue.Count}");
        }

        EnsureProcessing();
        return true;
    }

    public static void CancelAll(bool cancelCurrentCommand)
    {
        Log($"CancelAll requested | CancelCurrent={cancelCurrentCommand} | Pending={Queue.Count}");
        lock (Gate)
        {
            Interlocked.Increment(ref _generation);
            while (Queue.TryDequeue(out var discarded))
                InvokeCompletion(discarded);
            PendingKeys.Clear();
        }

        if (cancelCurrentCommand && CommandExecutor.IsRunning)
            CommandExecutor.CancelCurrentGroup();
        CommandExecutor.ClearFollowUpPending();
    }

    public static async Task SuspendCancelAndDrainAsync(bool cancelCurrentCommand)
    {
        lock (Gate)
            _suspended = true;

        CancelAll(cancelCurrentCommand);
        while (Volatile.Read(ref _isProcessing) != 0 || Volatile.Read(ref _activeContinuations) != 0)
            await Task.Delay(10);
    }

    public static void Resume()
    {
        lock (Gate)
            _suspended = false;
    }

    public static void RunContinuation(string context, Func<Task> action)
    {
        int generation;
        lock (Gate)
        {
            if (_suspended)
            {
                Log($"Rejected continuation '{context}': queue suspended");
                return;
            }
            generation = Generation;
            Interlocked.Increment(ref _activeContinuations);
            Log($"Continuation scheduled '{context}' | Generation={generation} | Active={_activeContinuations}");
        }

        _ = Task.Run(async () =>
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                if (generation != Generation)
                {
                    Log($"Continuation skipped '{context}': generation {generation} != {Generation}");
                    return;
                }
                Log($"Continuation starting '{context}'");
                await action();
                Log($"Continuation action returned '{context}' | Elapsed={stopwatch.ElapsedMilliseconds}ms");
            }
            catch (Exception ex)
            {
                Plugin.Log.Error(ex, $"[ActionQueue] Continuation '{context}' failed");
                Log($"Continuation failed '{context}' | {ex.GetType().Name}: {ex.Message}");
            }
            finally
            {
                var active = Interlocked.Decrement(ref _activeContinuations);
                Log($"Continuation finished '{context}' | Elapsed={stopwatch.ElapsedMilliseconds}ms | Active={active}");
            }
        });
    }

    private static void EnsureProcessing()
    {
        if (Interlocked.CompareExchange(ref _isProcessing, 1, 0) == 0)
        {
            Log($"Processor scheduled | Pending={Queue.Count}");
            _ = Task.Run(ProcessQueue);
        }
        else
        {
            Log($"Processor already active | Current='{_currentContext}' | Pending={Queue.Count}");
        }
    }

    private static async Task ProcessQueue()
    {
        try
        {
            while (Queue.TryDequeue(out var item))
            {
                var stopwatch = Stopwatch.StartNew();
                _currentContext = item.Context;
                Log($"Dequeued '{item.Context}' | ItemGeneration={item.Generation} | CurrentGeneration={Generation} | Pending={Queue.Count}");
                try
                {
                    var generationValid = item.Generation == Generation;
                    var actionValid = item.IsValid?.Invoke() ?? true;
                    Log($"Validated '{item.Context}' | GenerationValid={generationValid} | ActionValid={actionValid}");
                    if (!generationValid || !actionValid)
                    {
                        Log($"Skipped '{item.Context}' (stale or invalid)");
                        continue;
                    }

                    Log($"Starting action '{item.Context}'");
                    await item.Action();
                    Log($"Action returned '{item.Context}' | Elapsed={stopwatch.ElapsedMilliseconds}ms | " +
                        $"ExecutorRunning={CommandExecutor.IsRunning} | FollowUpPending={CommandExecutor.IsFollowUpPending}");

                    var waitLogged = false;
                    while (CommandExecutor.IsRunning || CommandExecutor.IsFollowUpPending)
                    {
                        if (!waitLogged)
                        {
                            Log($"Waiting after '{item.Context}' | ExecutorRunning={CommandExecutor.IsRunning} | " +
                                $"FollowUpPending={CommandExecutor.IsFollowUpPending} | Continuations={_activeContinuations}");
                            waitLogged = true;
                        }
                        if (item.Generation != Generation)
                        {
                            Log($"Wait aborted '{item.Context}': generation changed");
                            break;
                        }
                        if (!CommandExecutor.IsRunning
                            && CommandExecutor.IsFollowUpPending
                            && Volatile.Read(ref _activeContinuations) == 0)
                        {
                            Log($"Cleared stale follow-up flag after '{item.Context}'");
                            CommandExecutor.ClearFollowUpPending();
                            break;
                        }
                        await Task.Delay(25);
                    }
                    if (waitLogged)
                        Log($"Wait completed '{item.Context}' | Elapsed={stopwatch.ElapsedMilliseconds}ms");
                }
                catch (Exception ex)
                {
                    Plugin.Log.Error(ex, $"[ActionQueue] '{item.Context}' failed");
                    Log($"Action failed '{item.Context}' | {ex.GetType().Name}: {ex.Message}");
                }
                finally
                {
                    if (item.Key.Length > 0 && item.Generation == Generation)
                    {
                        lock (Gate)
                            PendingKeys.Remove(item.Key);
                    }
                    InvokeCompletion(item);
                    Log($"Finished '{item.Context}' | Elapsed={stopwatch.ElapsedMilliseconds}ms | Pending={Queue.Count}");
                }
            }
        }
        finally
        {
            _currentContext = string.Empty;
            Interlocked.Exchange(ref _isProcessing, 0);
            Log($"Processor stopped | Pending={Queue.Count} | Continuations={_activeContinuations}");
            if (!Queue.IsEmpty)
                EnsureProcessing();
        }
    }

    private static void InvokeCompletion(QueueItem item)
    {
        try
        {
            item.OnCompleted?.Invoke();
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, $"[ActionQueue] Completion for '{item.Context}' failed");
        }
    }

    private static void Log(string message)
    {
        Plugin.Instance.GetMainWindow().AddDebugLog(
            $"[ActionQueue T{Environment.CurrentManagedThreadId}/Task{Task.CurrentId?.ToString() ?? "-"}] {message}");
    }
}
