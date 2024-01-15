# Building a Synchronisation Context

In this article I'll demonstrate how to build a simples synchronisation context for a console application.

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