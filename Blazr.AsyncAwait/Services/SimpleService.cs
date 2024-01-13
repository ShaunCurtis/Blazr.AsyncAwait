namespace Blazr.AsyncAwait.Services;

public abstract class SimpleService
{
    public void LogMessage()
    {
        Console.WriteLine($"Thread: {Thread.CurrentThread.ManagedThreadId} - SC: {SynchronizationContext.Current?.GetHashCode()} - {this.GetHashCode()}:{this.GetType().Name} LogMessage");
    }

    public async Task LogMessageAsync(int delay = 100)
    {
        Console.WriteLine($"Thread: {Thread.CurrentThread.ManagedThreadId} - SC: {SynchronizationContext.Current?.GetHashCode()} - {this.GetHashCode()}:{this.GetType().Name} LogMessageAsync Pre Yield");
        await Task.Delay(delay);
        Console.WriteLine($"Thread: {Thread.CurrentThread.ManagedThreadId} - SC: {SynchronizationContext.Current?.GetHashCode()} - {this.GetHashCode()}:{this.GetType().Name} LogMessageAsync Post Yield");
    }
}

public class SingletonService : SimpleService { }
public class ScopedService : SimpleService {}
public class TransientService : SimpleService { }

