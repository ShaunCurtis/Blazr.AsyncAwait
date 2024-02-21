using Blazr.SyncronisationContext;
using System.Runtime.CompilerServices;

//var sc = new BlazrSynchronisationContext();
var sc = new MySynchronizationContext();
SynchronizationContext.SetSynchronizationContext(sc);


var awaitable = new BabyAwaitable(false);

var timer = new Timer(_ => awaitable.Finish(), null, 3000, -1);

var result = await awaitable;

Utilities.WriteToConsole($"{result}");

Utilities.WriteToConsole($"Main Complete");


Console.ReadLine();

//===========================================

public class BabyAwaitable
{
    private volatile bool finished;
    public bool IsFinished => finished;
    public BabyAwaitable(bool finished) => this.finished = finished;
    public void Finish()
    {
        Utilities.WriteToConsole("Finish Set");
        finished = true;
    }
    public BabyAwaiter GetAwaiter() => new BabyAwaiter(this);
}

public class BabyAwaiter : INotifyCompletion
{
    private readonly BabyAwaitable awaitable;
    private readonly SynchronizationContext? capturedContext = SynchronizationContext.Current;

    public BabyAwaiter(BabyAwaitable awaitable)
        => this.awaitable = awaitable;

    public bool IsCompleted => awaitable.IsFinished;

    public int GetResult()
    {
        Utilities.WriteToConsole("Get Result Spinning");
        SpinWait.SpinUntil(() => awaitable.IsFinished);
        Utilities.WriteToConsole("Get Result complete");
        return new Random().Next();
    }

    public void OnCompleted(Action continuation)
    {
        //SpinWait.SpinUntil(() => awaitable.IsFinished);
        Utilities.WriteToConsole("OnCompleted");
        if (capturedContext != null) capturedContext.Post(state => continuation(), null);
        else continuation();
    }
}

public class MySynchronizationContext : SynchronizationContext
{
    public override void Post(SendOrPostCallback d, object? state)
    {
        Utilities.WriteToConsole("Posted to synchronization context");
        d(state);
    }
}
