using System.Collections.Concurrent;
using System.Reflection.Metadata;
using static System.Reflection.Metadata.BlobBuilder;

namespace Blazr.Async;

public class BlazrSynchronisationContext : SynchronizationContext
{
    private BlockingCollection<WorkMessage> _messageQueue = new();

    public override void Post(SendOrPostCallback callback, object? state)
    {
        //Utilities.LogToConsole("SyncContext Post");
        _messageQueue.Add(new WorkMessage(callback, state));
    }

    public override void Send(SendOrPostCallback callback, object? state)
    {
        //Utilities.LogToConsole("SyncContext Send");
        var resetEvent = new ManualResetEventSlim(false);
        try
        {
            _messageQueue.Add(new WorkMessage(callback, state, resetEvent));
            resetEvent.Wait();
        }
        finally
        {
            resetEvent.Dispose();
        }
    }

    public void Start()
    {
        var thread = new Thread(new ThreadStart(RunLoop));
        thread.IsBackground = true;
        thread.Start();
    }

    public void Stop()
    {
        _messageQueue.Add(WorkMessage.StopMessage);
    }

    private void RunLoop()
    {
        SynchronizationContext.SetSynchronizationContext(this);
        Utilities.LogToConsole("Message Loop Running");

        foreach (var message in _messageQueue.GetConsumingEnumerable(CancellationToken.None))
        {
            if (!message.IsRunMessage)
                break;

            message.Callback?.Invoke(message.State);
        }

        Utilities.LogToConsole("Message Loop Stopped");
    }
}
