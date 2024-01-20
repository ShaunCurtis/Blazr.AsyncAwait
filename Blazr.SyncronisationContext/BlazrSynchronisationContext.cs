namespace Blazr.SyncronisationContext;

public class BlazrSynchronisationContext : SynchronizationContext
{
    MessageQueue _messageQueue = new MessageQueue();

    public override void Post(SendOrPostCallback callback, object? state)
    {
        Utilities.LogToConsole("SyncContext Post");
        _messageQueue.Post(new WorkMessage(callback, state));
    }

    public override void Send(SendOrPostCallback callback, object? state)
    {
        Utilities.LogToConsole("SyncContext Send");
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
}
