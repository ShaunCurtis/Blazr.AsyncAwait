namespace Blazr.AsyncAwait;

public static class TaskHelper
{
    /// <summary>
    ///  Method that does some async work such as calling into the data pipeline
    /// in this instance it fakes it using Task.Delay.
    /// </summary>
    /// <returns></returns>
    public static Task DoSomethingAsync()
        => Task.Delay(1000);

    /// <summary>
    /// This is window dressing - a synchronous block of code wrapped in Task.
    /// You can await it, but it doesn't yield
    /// </summary>
    /// <returns></returns>
    public static Task PretendToDoSomethingAsync()
    {
        Thread.Sleep(1000);
        return Task.CompletedTask;
    }
}
