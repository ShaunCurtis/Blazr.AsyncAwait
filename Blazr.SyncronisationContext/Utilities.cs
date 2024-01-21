using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blazr.SyncronisationContext;

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
