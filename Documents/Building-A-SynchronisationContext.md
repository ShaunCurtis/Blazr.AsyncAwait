# Building a Synchronisation Context

In this article I'll demonstrate how to build a simplistic synchronisation context for a console application.

## What is a Synchronisation Context?

Applications with UI have a severe problwm with threading.  Their classes are rarely thread safe, and don't support mechanisms such as `lock` to make them so.  The solution is to run all UI code on the same thread.  Code executes sequentually: no need to worry about thread safety.

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

### WorkMessage

First a simple message object:

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

`StopMessage` contains a null `SendOrPostCallback` property, but you can only set it to null by getting the static property `StopMessage`.

### Utilities

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

    public static void LogToConsole(string startMessage)
    {
        string sc = SynchronizationContext.Current is null
            ? " -- Not Set -- "
            : SynchronizationContext.Current.GetHashCode().ToString();

        Console.WriteLine($"     ===> {startMessage} - ThreadId: {Thread.CurrentThread.ManagedThreadId} - SyncContext: {sc}");
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

    public static async Task DoWorkThreadpoolAsync(object? state)
    {
        WriteToConsole("Threadpool DoWorkAsync started");
        await Task.Delay(3000);
        WriteToConsole("Threadpool DoWorkAsync continuation");
    }
}
```

### MessageQueue

Next we need a message queue.  This uses a semaphore to control access to the queue.  The initial value of the semaphore is 0.  So the call to `_semaphore.Wait();` will block until a message is posted and sets to semaphore count to 1.  `_semaphore.Wait();` will be released to continue and de-queue a message.  Note when `_semaphore.Wait();` runs it decrements the semaphore count by 1, so sets it to 0. 

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

### BlazrSynchronisationContext

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
    Utilities.LogToConsole("Message Loop Running");
    WorkMessage message = WorkMessage.StopMessage;

    do
    {
        //Utilities.LogToConsole("Message Loop Fetch");
        message = _messageQueue.Fetch();
        //Utilities.LogToConsole("Message Loop Executing");
        message.Callback?.Invoke(message.State);

    } while (message.IsRunMessage);

    Utilities.LogToConsole("Message Loop Stopped");
}
```

`Post` and `Send` create `WorkMessage` instances and post them to the messge queue.  `Send` adds a `ManualResetEventSlim` which is used to block until the action completes.

```csharp
public override void Post(SendOrPostCallback callback, object? state)
{
    //Utilities.LogToConsole("SyncContext Post");
    _messageQueue.Post(new WorkMessage(callback, state));
}

public override void Send(SendOrPostCallback callback, object? state)
{
    //Utilities.LogToConsole("SyncContext Send");
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

### Demo

Code `Main`.

```csharp
BlazrSynchronisationContext sc = new BlazrSynchronisationContext();
sc.Start();

PostToUI("Application started.");

Console.ReadLine();

PostToUI("Application => Post Started.");

sc.Post(Utilities.DoWorkVoidAsync, null);

PostToUI("Application => Post After.");

Console.ReadLine();

PostToUI("Application => ThreadPool Started.");

ThreadPool.QueueUserWorkItem(async (state) =>
{
    SynchronizationContext.SetSynchronizationContext(sc);
    await Utilities.DoWorkThreadpoolAsync(null);
});

PostToUI("Application => ThreatPool After.");

Console.ReadLine();

PostToUI("Application => TaskRun Started.");

var task = Task.Run(async () =>
{
    SynchronizationContext.SetSynchronizationContext(sc);
    await Utilities.DoWorkTaskAsync();
});

PostToUI("Application => TaskRun After.");

Console.ReadLine();


void PostToUI(string message)
{
    sc.Post((state) =>
    {
        Utilities.WriteToConsole(message);
    }, null);
}
```

And when we run it, we see `main` running on thread 1 and the Message Loop running on thread 9.  All the work we post to the SC runs on the Message Loop thread.

Even when we spin off code to another thread, the continuation is run on the synchronisation context thread.

```text
         ===> Message Loop Running - ThreadId: 9 - SyncContext: 62476613
    Application started. - ThreadId: 9 - SyncContext: 62476613

    Application => Post Started. - ThreadId: 9 - SyncContext: 62476613
    DoWorkAsync started  - ThreadId: 9 - SyncContext: 62476613
    Application => Post After. - ThreadId: 9 - SyncContext: 62476613
--> Pause for timer to expire
    DoWorkAsync continuation - ThreadId: 9 - SyncContext: 62476613

    Application => ThreadPool Started. - ThreadId: 9 - SyncContext: 62476613
    Application => ThreatPool After. - ThreadId: 9 - SyncContext: 62476613
    Threadpool DoWorkAsync started - ThreadId: 3 - SyncContext: 62476613
--> Pause for timer to expire
    Threadpool DoWorkAsync continuation - ThreadId: 9 - SyncContext: 62476613

    Application => TaskRun Started. - ThreadId: 9 - SyncContext: 62476613
    Application => TaskRun After. - ThreadId: 9 - SyncContext: 62476613
    Task DoWorkAsync started - ThreadId: 3 - SyncContext: 62476613
--> Pause for timer to expire
    Task DoWorkAsync continuation - ThreadId: 9 - SyncContext: 62476613
```
