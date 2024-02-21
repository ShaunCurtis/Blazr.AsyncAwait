namespace Blazr.SyncronisationContext;

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
