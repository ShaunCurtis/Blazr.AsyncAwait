namespace Blazr.Async;

public readonly record struct WorkMessage
{
    public readonly SendOrPostCallback? Callback;
    public readonly object? State;
    public readonly ManualResetEventSlim? FinishedEvent;

    public WorkMessage(SendOrPostCallback callback, object? state, ManualResetEventSlim? finishedEvent = null)
    {
        Callback = callback;
        State = state;
        FinishedEvent = finishedEvent;
    }

    public WorkMessage(SendOrPostCallback callback, object? state) : this(callback, state, null) {}

    private WorkMessage(SendOrPostCallback? callback) { }

    public static WorkMessage StopMessage => new WorkMessage(null);

    public bool IsRunMessage => this.Callback is not null; 
}
