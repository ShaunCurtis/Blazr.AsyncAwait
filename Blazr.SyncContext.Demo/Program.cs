using Blazr.SyncronisationContext;
using System.Runtime.CompilerServices;

var sc = new BlazrSynchronisationContext();
//SynchronizationContext.SetSynchronizationContext(sc);

WriteToConsole("Application started");

sc.Start();

//sc.Post(DoWorkAsync, null);

var task = Task.Run(async () =>
{
    SynchronizationContext.SetSynchronizationContext(sc);
    await DoWork1Async();
});

//ThreadPool.QueueUserWorkItem((state) =>
//{
//    SynchronizationContext.SetSynchronizationContext(sc);
//    WriteToConsole("ThreadPool run");
//    DoWork2Async(null);
//});

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


async void DoWork2Async(object? state)
{
    WriteToConsole("Threadpool DoWorkAsync started");
    await Task.Delay(3000);
    WriteToConsole("Threadpool DoWorkAsync continuation");
}
