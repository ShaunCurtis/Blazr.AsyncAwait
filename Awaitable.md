# The Async Series - Awaitables and Awaiters

The core functionality of async behaviour is the implementation of `GetAwaiter`.  Any object implementing a `GetAwaiter` can be awaited by the *Async/Await*. 

I'll use the following terms:

 - An *Awaitable* is an object that executes some form of asynchronous behaviour and implements a `GetAwaiter` method. 
 - An *Awaiter* is an object returned by `GetAwaiter`.

An *Awaiter* must implement the following functionality:

```csharp
public struct MyAwaiter : INotifyCompletion
{
    public bool IsCompleted;
    public void OnCompleted(Action continuation);
    public void GetResult();
}
```

The awaiter providea: 

1. A bool property to detect if the awaitable is complete.
2. A method to post a continuation to be run when the awaitable is complete.
3. A method to get the result on completion.

`Task` in it's various guises implements this functionality.  It's `GetAwaiter` returns itself.

## Code and Repo

The code for this article is in the *Blazr.Async* and *Blazr.SyncronisationContext* libraries and the example code is in *Blazr.Awaitable.Demo* project. 

## Implementation

Implementing a customer awaiter is complex.  This one re-invents the wheel: an alternative version of `Task.Delay`.  The code is for demonstration only.  Do not use this in a production system.

### MyAwaiter

This is the awaiter.  It's implemented used the *Awaiter* pattern.

```csharp
public readonly struct MyAwaiter : INotifyCompletion
{
    private readonly MyAwaitable awaitable;

    public bool IsCompleted => awaitable.IsCompleted;

    public MyAwaiter(MyAwaitable awaitable) =>  this.awaitable = awaitable;

    public MyAwaiter GetAwaiter() => this;

    public int GetResult() => awaitable.GetResult();

    public void OnCompleted(Action continuation) =>  awaitable.OnCompleted(continuation);
}
```

### MyAwaitable

The code is fairly self-explanatory.  I've added some inline comments where appropriate.

The public interface looks like this:

```csharp
public interface IMyAwaitable
{
    public bool IsCompleted { get; }
    public int Result { get; }
    public MyAwaiter GetAwaiter();
    public MyAwaiter ConfigureAwait(bool useContext);
    public void OnCompleted(Action continuation);
    public abstract static MyAwaiter Idle(int period);
}
```

1. `volatile` is used to identify fields that may be used by multiple threads.
1. There are only private constructors: the only way to get a `MyAwaitable` instance is through static methods.
1. Various methods are `internal`: only available to objects within the assembly i.e. `MyAwaiter`.

The public skeleton of `MyAwaitable`.
```csharp
public class MyAwaitable
{
    public bool IsCompleted { get; }
    public int Result { get; }
    public MyAwaiter GetAwaiter();
    public MyAwaiter ConfigureAwait(bool useContext);
    public void OnCompleted(Action continuation);
    public abstract static MyAwaiter Idle(int period);
}
```

`MyAwaitable`

```csharp
public class MyAwaitable
{
    private volatile int _result;
    private volatile Queue<Action> _continuations = new();
    private Timer? _timer;
    private volatile SynchronizationContext? _capturedContext;
    private volatile bool _completed;
    private volatile bool _runOnCapturedContext;

    public bool IsCompleted => _completed;

    // Private constructor.  An instance can onky bw created through static methods
    private MyAwaitable()
    {
        // Capture the current sync context so we can run the continuation in the correct context
        _capturedContext = SynchronizationContext.Current;
    }

    public MyAwaiter GetAwaiter() => ConfigureAwait(true);

    public MyAwaiter ConfigureAwait(bool useContext)
    {
        _runOnCapturedContext = useContext;
        // Return a new instance of the awaiter
        return new MyAwaiter(this);
    }

    public void OnCompleted(Action continuation)
    {
        _continuations.Enqueue(continuation);
        this.ScheduleContinuationIfCompleted();
    }

    private void ScheduleContinuationIfCompleted()
    {
        // Do nothing if the awaitable is still running
        if (!_completed)
            return;

        // The awaitable has completed.
        // Run the continuations in the correct context based on _runOnCapturedContext
        while (_continuations.Count > 0)
        {
            var continuation = _continuations.Dequeue();
            if (_continuations.Count() > 0)
            {
                var completedContinuations = new List<Action>();

                if (_runOnCapturedContext && _capturedContext != null)
                    _capturedContext.Post(_ => continuation(), null);

                else
                    continuation();
            }
        }
    }
```

The static method to get an Idle.  It sets up the timer and then spins off a thread to wait for `IsCompleted` to complete and returns the `awaiter`.  

The spun off thread uses a `SpinWait` loop to do the waiting.  When `IsCompleted` is true, it schedules the completion based on `_runOnCapturedContext`.  

1. If true [the default], on the captured synchonisation context [if it's not null] or 
1. on the current thread if false or no synchonisation context exists. 
 
`SpinWait` is a *TPL* primitive object to provide a low CPU utilization loop on the thread.  Check CPU utilization in the debugger.

```csharp
    public static MyAwaiter Idle(int period)
    {
        // Create an awaitable instance
        MyAwaitable awaitable = new MyAwaitable();
        // Set up the instance timer with the correct wait period
        awaitable._timer = new(awaitable.TimerExpired, null, period, Timeout.Infinite);

        // Spin off a waiter on a separate thread so we can pass control back [Yield] to the caller.
        // Check CPU usage to confirm low usage footprint
        ThreadPool.QueueUserWorkItem(awaitable.WaitOnCompletion);

        // Return the awaiter to the caller
        return awaitable.GetAwaiter();
    }

    // Schedule the continuation when the timer expires and sets _completed to true.
    internal void WaitOnCompletion(object? state)
    {
        SynchronizationContext.SetSynchronizationContext(_capturedContext);
        Utilities.LogToConsole("MyAwaitable waiting on timer to expire.");

        var wait = new SpinWait();
        while (!_completed)
            wait.SpinOnce();

        this.ScheduleContinuationIfCompleted();
    }
}
```

## Demo

Here's a demno `Program`.

It uses `BlazrSynchronisationContext` which is covered in another article.  It emulates a UI by posting consule writes to the the synchronisation context.

```csharp
BlazrSynchronisationContext sc = new BlazrSynchronisationContext();
SynchronizationContext.SetSynchronizationContext(sc);
sc.Start();

PostToUI("Application started - hit a key to start");

// wait for a keyboard click to start
Console.ReadLine();

PostToUI("Application => Start running DoWorkAsync.");

sc.Post((state) => { _ = DoWorkAsync(); }, null);

PostToUI("Application => After DoWorkAsync Yields.");

// wait for a keyboard click to start
Console.ReadLine();

PostToUI("Application => Start running DoWorkAsynVoid.");

sc.Post(DoWorkAsyncVoid, null);

PostToUI("Application => After DoWorkAsyncVoid Yields.");

Console.ReadLine();


void PostToUI(string message)
{
    sc.Post((state) =>
    {
        Utilities.WriteToConsole(message);
    }, null);
}

async void DoWorkAsyncVoid(object? state)
{
    PostToUI("DoWorkAsync started");
    await MyAwaitable.Idle(3000);
    PostToUI("DoWorkAsync completed");
}

async Task DoWorkAsync()
{
    PostToUI("DoWorkAsync started");
    await MyAwaitable.Idle(3000);
    PostToUI("DoWorkAsync completed");
}
```


What you'll see is:

1. The application running on thread 1.
1. The synchronisation context message loop running on thread 9.
1. The awaitable running on thread 10.
1. The timer callback running on thread 5.

All the UI work is done on the synchronisation context running on Thread 9. 

```text
         ===> Message Loop Running - ThreadId: 9 - SyncContext: 62476613
    Application started - hit a key to start - ThreadId: 9 - SyncContext: 62476613

--->[key press]

    Application => Start running  DoWorkAsync. - ThreadId: 9 - SyncContext: 62476613
         ===> MyAwaitable waiting on timer to expire. - ThreadId: 10 - SyncContext: 62476613
    Application => After DoWorkAsync Yields. - ThreadId: 9 - SyncContext: 62476613
    DoWorkAsync started - ThreadId: 9 - SyncContext: 62476613
--->[Pause while timer expires]
         ===> Timer Expired - ThreadId: 5 - SyncContext:  -- Not Set --
    DoWorkAsync completed - ThreadId: 9 - SyncContext: 62476613

--->[key press]

    Application => Start running  DoWorkAsynVoid. - ThreadId: 9 - SyncContext: 62476613
         ===> MyAwaitable waiting on timer to expire. - ThreadId: 10 - SyncContext: 62476613
    Application => After DoWorkAsyncVoid Yields. - ThreadId: 9 - SyncContext: 62476613
    DoWorkAsync started - ThreadId: 9 - SyncContext: 62476613
--->[Pause while timer expires]
         ===> Timer Expired - ThreadId: 5 - SyncContext:  -- Not Set --
    DoWorkAsync completed - ThreadId: 9 - SyncContext: 62476613
```

### Some Key Points

1. A call to a method returning a Task returns a `Task<T>`, not `T`.  The way you write the code:

```csharp
var result = await DoSomeAsyncWork();
```

suggests result is `T`.  The Dev environment even tells you so.  That's just syntactic sugar.  Behind the scenes the code is calling `GetResult()` on the completed `Task<T>`.

Miss out the `await` and `result` with now be a `Task<T>.
  
2. `Task` and all it incarnations respect `SynchronizationContext.Current`, and run the continuation on that context if `ConfigureAwait` is true [the default]. 

3. Async methods that need to await a result [from another process] must run on a separate background thread.  The action of awaiting blocks the thread.  Switching this await, along with responsibility to schedule the continuations, to a separate thread frees the initial thread.  This is the process of yielding.  You can see this in the example above. 

4. You can set more than one continuation on an awaitable, and you can pass a continuation to a completed awaiter and it will be executed.  

5. It should be clear from the above code why calling `GetResult` blocks the current thread and causes deadlocks.