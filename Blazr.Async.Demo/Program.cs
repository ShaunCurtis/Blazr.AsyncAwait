using Blazr.Async;
using Blazr.SyncronisationContext;

var sc = new BlazrSynchronisationContext();
SynchronizationContext.SetSynchronizationContext(sc);

Utilities.WriteToConsole("Application started");

sc.Start();
//ThreadPool.QueueUserWorkItem(async (state) =>
//{
//    //SynchronizationContext.SetSynchronizationContext(sc);
//    await Utilities.DoWorkThreadpoolAsync(null);
//});

var awaitable = MyAwaitable.Idle(2000);
await awaitable;

Console.ReadLine();
