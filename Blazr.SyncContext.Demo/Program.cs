using Blazr.SyncronisationContext;
using System.Runtime.CompilerServices;

var sc = new BlazrSynchronisationContext();
//SynchronizationContext.SetSynchronizationContext(sc);

Utilities.WriteToConsole("Application started");

sc.Start();

//sc.Post(DoWorkAsync, null);

//var task = Task.Run(async () =>
//{
//    SynchronizationContext.SetSynchronizationContext(sc);
//    await Utilities.DoWorkTaskAsync();
//});

ThreadPool.QueueUserWorkItem(async (state) =>
{
    SynchronizationContext.SetSynchronizationContext(sc);
    await Utilities.DoWorkThreadpoolAsync(null);
});

Console.ReadLine();
sc.Post(Utilities.DoWorkVoidAsync, null);
Console.ReadLine();
