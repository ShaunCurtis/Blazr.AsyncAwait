# Building a Synchronisation Context

In this article I'll demonstrate how to build a simplistic synchronisation context for a console application.

## What is a Synchronisation Context?

Applications with UI have a severe problwm with Threading.  Their classes are rarely thread safe, and don't support mechanisms such as `lock` to make them so.  The solution to the problem is to run all UI code on the same thread.  Code executes sequentually: no need to worry about thread safety.

This is what a synchronisation context provides. Older UI frameworks have solved this problem in different ways.  The `SynchronisationContext` abstracts us and the TPL from this detail.  Different implementations for different frameworks.

By contrast, a console app doesn't have this problem.  You can use `lock` to ensure output isn't mixed from different threads.

Consider this code:

```csharp
async Task UIEvent()
{
await GetSomeDataFromAnAPI();
UpdateGrid();
}
```
The UI handler needs somewhere to post this code that will track the awaiter while the HttpClient gets the data [running on a different thread], and ensure the continuation [`UpdateGrid();`] runs on the UI context.

## Coding the Objects

First a message object:

```csharp
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
```

And some utilities:

```csharp
public static class Utilities
{
    public static void WriteToConsole(string startMessage)
    {
        string sc = SynchronizationContext.Current is null 
            ? " -- Not Set -- " 
            : SynchronizationContext.Current.GetHashCode().ToString();
 
        Console.WriteLine($"{startMessage} - ThreadId: {Thread.CurrentThread.ManagedThreadId} - SyncContext: {sc}");
    }

    public static async void DoWorkVoidAsync(object? state)
    {
        WriteToConsole("DoWorkAsync started ");
        await Task.Delay(1000);
        WriteToConsole("DoWorkAsync continuation");
    }

    public static async Task DoWorkTaskAsync()
    {
        WriteToConsole("Task DoWorkAsync started");
        await Task.Delay(1000);
        WriteToConsole("Task DoWorkAsync continuation");
    }

    public static async Task DoWorkAwaitAsync()
    {
        WriteToConsole("Await DoWorkAsync started");
        await Task.Delay(1000);
        WriteToConsole("Await DoWorkAsync continuation");
    }

    public static async void DoWorkThreadpoolAsync(object? state)
    {
        WriteToConsole("Threadpool DoWorkAsync started");
        await Task.Delay(3000);
        WriteToConsole("Threadpool DoWorkAsync continuation");
    }
}
```

The `StopMessage` contains a null `SendOrPostCallback` property, but you can only set it to null by getting the static property `StopMessage`.

Next we need a message queue.  This uses a semaphore to control access to the queue.  The initial value of the semaphore is 0.  So the call to `_semaphore.Wait();` will block until a message is posted and sets to semaphore count to 1.  `_semaphore.Wait();` will be released to continue and de-queue a message.  When `_semaphore.Wait();` runs it decrements to semaphore count to 0. 

```csharp
public class MessageQueue
{
    private ConcurrentQueue<WorkMessage> _queue = new();
    private SemaphoreSlim _semaphore = new(0);

    public void Post(WorkMessage Message)
    {
        _queue.Enqueue(Message);
        _semaphore.Release(1);
    }

    public WorkMessage Fetch()
    {
        _semaphore.Wait();
        if (_queue.TryDequeue(out WorkMessage result))
            return result;

        throw new InvalidOperationException("The queue is empty.  You can't dequeue an empty queue.");
    }
}
```

And our synchronisation context template:

```csharp
public class BlazrSynchronisationContext : SynchronizationContext
{
    MessageQueue _messageQueue = new MessageQueue();

    public override void Post(SendOrPostCallback callback, object? state);
    public override void Send(SendOrPostCallback callback, object? state);

    public void Start();
    public void Stop();

    private void RunLoop();
```

`Start` runs the message loop on a threadpool thread and sets the `SynchronizationContext` on that thread to this instance of the `BlazrSynchronisationContext`.

`RunLoop` gets a message from the queue, invokes it on the current thread, and loops. `_messageQueue.Fetch()` will block if the queue is empty and not reusume until a message is queued and releases the semaphore. 

```csharp
public void Start()
{
    ThreadPool.QueueUserWorkItem((state) => {
        SynchronizationContext.SetSynchronizationContext(this);
        RunLoop();
    });
}

private void RunLoop()
{
    Console.WriteLine($"Message Loop running on Thread: {Thread.CurrentThread.ManagedThreadId} - SC : {SynchronizationContext.Current?.GetHashCode()} ");

    WorkMessage message = WorkMessage.StopMessage;

    do
    {
        Console.WriteLine($"Queue fetching Message on Thread: {Thread.CurrentThread.ManagedThreadId} - SC : {SynchronizationContext.Current?.GetHashCode()} ");
        message = _messageQueue.Fetch();
        message.Callback?.Invoke(message.State);

    } while (message.IsRunMessage);

    Console.WriteLine($"Loop stopped on Thread: {Thread.CurrentThread.ManagedThreadId} - SC : {SynchronizationContext.Current?.GetHashCode()} ");
}
```

`Post` and `Send` create `WorkMessage` instances and post them to the messge queue.  `Send` adds a `ManualResetEventSlim` which is used to block until the action completes.

```csharp
    public override void Post(SendOrPostCallback callback, object? state)
    {
        _messageQueue.Post(new WorkMessage(callback, state));
    }

    public override void Send(SendOrPostCallback callback, object? state)
    {
        var resetEvent = new ManualResetEventSlim(false);
        try
        {
            _messageQueue.Post(new WorkMessage(callback, state, resetEvent));
            resetEvent.Wait();
        }
        finally
        {
            resetEvent.Dispose();
        }
    }
```
`Stop` posts a StopMmessage to the queue.
```
    public void Stop()
    {
        _messageQueue.Post(WorkMessage.StopMessage);
    }
```

### Posting

Code `Main`.

```csharp
var sc = new BlazrSynchronisationContext();
//SynchronizationContext.SetSynchronizationContext(sc);

WriteToConsole("Application started");

sc.Start();

sc.Post(DoWorkAsync, null);

Console.ReadLine();

sc.Post(DoWorkAsync, null);

Console.ReadLine();

async void DoWorkAsync(object? state)
{
    WriteToConsole("DoWorkAsync started ");
    await Task.Delay(1000);
    WriteToConsole("DoWorkAsync continuation");
}

void WriteToConsole(string startMessage)
    => Console.WriteLine($"{startMessage} {Thread.CurrentThread.ManagedThreadId} - SC : {SynchronizationContext.Current?.GetHashCode()}");
```

And when we run it, we see `main` running on thread 1 and the Message Loop running on thread 7.  All the work we post to the SC runs on the Message Loop thread.   

```text
Application started 1 - SC :
Message Loop running on Thread: 7 - SC : 12547953
Queue fetching Message on Thread: 7 - SC : 12547953
DoWorkAsync started  7 - SC : 12547953
Queue fetching Message on Thread: 7 - SC : 12547953
DoWorkAsync continuation 7 - SC : 12547953
Queue fetching Message on Thread: 7 - SC : 12547953

DoWorkAsync started  7 - SC : 12547953
Queue fetching Message on Thread: 7 - SC : 12547953
DoWorkAsync continuation 7 - SC : 12547953
Queue fetching Message on Thread: 7 - SC : 12547953
```

```csharp
var sc = new BlazrSynchronisationContext();

WriteToConsole("Application started");

sc.Start();

var task = Task.Run(async () =>
{
    SynchronizationContext.SetSynchronizationContext(sc);
    await DoWork1Async();
});

Console.ReadLine();
sc.Post(DoWorkAsync, null);
Console.ReadLine();

void WriteToConsole(string startMessage)
    => Console.WriteLine($"{startMessage} {Thread.CurrentThread.ManagedThreadId} - SC : {SynchronizationContext.Current?.GetHashCode()}");

async void DoWorkAsync(object? state)
{
    WriteToConsole("DoWorkAsync started ");
    await Task.Delay(1000);
    WriteToConsole("DoWorkAsync continuation");
}

async Task DoWork1Async()
{
    WriteToConsole("Await DoWorkAsync started");
    await Task.Delay(1000);
    WriteToConsole("Await DoWorkAsync continuation");
}
```

The result is `DoWorkAsync` starts on a different thread, but as that thread has a synchonisation context set, the continuation is posted back to the synchonisation context where it is run.

```csharp
Application started 1 - SC :
Message Loop running on Thread: 10 - SC : 45653674
Queue fetching Message on Thread: 10 - SC : 45653674
Await DoWorkAsync started 9 - SC : 45653674
Await DoWorkAsync continuation 10 - SC : 45653674
Queue fetching Message on Thread: 10 - SC : 45653674
```