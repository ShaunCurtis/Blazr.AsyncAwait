namespace Blazr.SyncronisationContext;

public class BlazrSynchronisationContext : SynchronizationContext
{
    MessageQueue _messageQueue = new MessageQueue();

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

    public void Start()
    {
        ThreadPool.QueueUserWorkItem((state) => {
            SynchronizationContext.SetSynchronizationContext(this);
            RunLoop();
        });
    }

    public void Stop()
    {
        _messageQueue.Post(WorkMessage.StopMessage);
    }

    private void RunLoop()
    {
        Console.WriteLine($"Message Loop running on Thread: {Thread.CurrentThread.ManagedThreadId} - SC : {SynchronizationContext.Current?.GetHashCode()} ");

        WorkMessage message = WorkMessage.StopMessage;

        do
        {
            Console.WriteLine($"Queue fetching Message on Thread: {Thread.CurrentThread.ManagedThreadId} - SC : {SynchronizationContext.Current?.GetHashCode()} ");
            message = _messageQueue.Fetch();
            //SynchronizationContext.SetSynchronizationContext(this);
            message.Callback?.Invoke(message.State);

        } while (message.IsRunMessage);

        Console.WriteLine($"Loop stopped on Thread: {Thread.CurrentThread.ManagedThreadId} - SC : {SynchronizationContext.Current?.GetHashCode()} ");
    }
}
