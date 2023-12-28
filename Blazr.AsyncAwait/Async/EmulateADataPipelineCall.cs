using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Blazr.AsyncAwait.Async;

public class EmulateADataPipelineCall
{
    private int _millisecs = 10;
    private EmulateADataPipelineCallState _state;

    public EmulateADataPipelineCall(int millisecs)
    {
        _millisecs = millisecs;

        _state = new EmulateADataPipelineCallState();

        if (millisecs < 0)
            throw new ArgumentOutOfRangeException(nameof(millisecs));

        _state.Timer = new Timer(_state.Complete, _state, millisecs, -1);
    }

    public EmulateADataPipelineCallState GetAwaiter()
        => _state;

    public static EmulateADataPipelineCall Create(int millisecs)
    {
        return new(millisecs);
    }
}

public sealed class EmulateADataPipelineCallState : INotifyCompletion
{
    private Stopwatch _stopwatch = new Stopwatch();

    internal Timer? Timer;

    public EmulateADataPipelineCallState()
    {
        _stopwatch.Start();
        Console.WriteLine($"Created at {DateTime.Now.ToLongTimeString()}.");
        Console.WriteLine($"Delay was: {_stopwatch.ElapsedMilliseconds} milliseconds.");
    }

    internal void Complete(object? statusInfo)
    {
        Console.WriteLine($"Complete called: {_stopwatch.ElapsedMilliseconds} milliseconds.");
        if (!this.IsCompleted)
        {
            this.IsCompleted = true;
            // dispose and release the timer for GC.
            this.Timer?.Dispose();
            //this.Timer = null;
        }
    }

    public bool IsCompleted { get; set; }

    public void OnCompleted(Action continuation)
    {
        Console.WriteLine($"OnCompleted called: {_stopwatch.ElapsedMilliseconds} milliseconds.");
        if (IsCompleted)
        {
            continuation();
            return;
        }

        var capturedContext = SynchronizationContext.Current;

        if (capturedContext != null)
            capturedContext.Post(_ => continuation(), null);
        else
            continuation();
    }

    public void GetResult()
    {
        if (!this.IsCompleted)
        {
            var wait = new SpinWait();
            while (!this.IsCompleted)
                wait.SpinOnce();
        }
        return;
    }
}

