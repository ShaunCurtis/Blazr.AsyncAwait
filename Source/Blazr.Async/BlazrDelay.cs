﻿using System.Runtime.CompilerServices;

namespace Blazr.Async;

public class BlazrDelay : INotifyCompletion
{
    public bool IsCompleted { get; private set; }

    private volatile SynchronizationContext? _synchronizationContext = SynchronizationContext.Current;
    private Timer _timer;
    private volatile Queue<Action> _continuations = new();

    private BlazrDelay(int delay)
    {
        _timer = new Timer(this.OnTimerExpired, null, delay, -1);
    }

    /// <summary>
    /// Method called when the Timer expires
    /// This will be executed on a threadpool thread,
    /// but ScheduleContinuationsIfCompleted will switch execution to the correct context
    /// </summary>
    /// <param name="state"></param>
    private void OnTimerExpired(object? state)
    {
        _timer.Dispose();
        this.IsCompleted = true;
        this.ScheduleContinuationsIfCompleted();
    }

    public BlazrDelay GetAwaiter()
        => this;

    /// <summary>
    /// This is the method called to add a continuation to the awaiter
    /// </summary>
    /// <param name="continuation"></param>
    public void OnCompleted(Action continuation)
    {
        _continuations.Enqueue(continuation);
        // Run the queued completion immediately if the awaitable has already completed
        this.ScheduleContinuationsIfCompleted();
    }
    
    /// <summary>
    /// This will run the cointinuations if the state is Completed
    /// </summary>
     private void ScheduleContinuationsIfCompleted()
    {
        // Do nothing if the awaitable has not completed
        if (!this.IsCompleted)
            return;

        // If the awaitable has completed.
        // Run the continuations in the correct context based on _synchronizationContext
        while (_continuations.Count > 0)
        {
            var continuation = _continuations.Dequeue();

            if (_synchronizationContext != null)
                _synchronizationContext.Post(_ => continuation(), null);

            else
                continuation();
        }
    }

    public void GetResult() { }

    /// <summary>
    /// Static Constructor
    /// </summary>
    /// <param name="delay"></param>
    /// <returns></returns>
    public static BlazrDelay Delay(int delay)
    {
        var instance = new BlazrDelay(delay);
        return instance;
    }
}
