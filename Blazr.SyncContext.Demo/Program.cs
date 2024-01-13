using Blazr.SyncronisationContext;
using System.Runtime.CompilerServices;

Console.WriteLine($"Application started thread {Thread.CurrentThread.ManagedThreadId}");

var sc = new BlazrSynchronisationContext();
//SynchronizationContext.SetSynchronizationContext(sc);

//var thread = new Thread(() =>
//{
//    SynchronizationContext.SetSynchronizationContext(sc);
//    WriteToConsole("Aux Thread");
//    sc.Post((state) => { WriteToConsole("Post from Aux Thread");}, null);
//});
//thread.Start();

var task = Task.Run(async () =>
{
    SynchronizationContext.SetSynchronizationContext(sc);
    Thread.Sleep(50);
    //Console.WriteLine("Awaiting task");
    Console.WriteLine($"Job Pre Yield thread {Thread.CurrentThread.ManagedThreadId} - SC : {SynchronizationContext.Current?.GetHashCode()}");
    await Task.Delay(50);
    // this will wake up on main thread or not
    // depending on the synchronization context
    Console.WriteLine($"Job Post Yield Continuation thread {Thread.CurrentThread.ManagedThreadId} - SC : {SynchronizationContext.Current?.GetHashCode()}");
});
sc.Start();


//sc.Post(DoWorkAsync, null);
//sc.Post(DoWorkAsync, null);
//sc.Post(DoWorkAsync, null);

sc.Stop();
Console.ReadLine();

void WriteToConsole(string startMessage)
    => Console.WriteLine($"{startMessage} {Thread.CurrentThread.ManagedThreadId} - SC : {SynchronizationContext.Current?.GetHashCode()}");

async void DoWorkAsync(object? state)
{
    Console.WriteLine($"Start DoWorkAsync on Thread: {Thread.CurrentThread.ManagedThreadId} - SC : {SynchronizationContext.Current?.GetHashCode()}  ");
    await Task.Delay(1000);
    Console.WriteLine($"DoWorkAsync Continuation on Thread: {Thread.CurrentThread.ManagedThreadId} - SC : {SynchronizationContext.Current?.GetHashCode()}  ");
}