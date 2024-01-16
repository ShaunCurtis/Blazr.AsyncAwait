using Blazr.AsyncAwait.Async;
using Blazr.SyncronisationContext;
using System.Runtime.CompilerServices;

var sc = new BlazrSynchronisationContext();
SynchronizationContext.SetSynchronizationContext(sc);

Utilities.WriteToConsole("Application started");

sc.Start();
//ThreadPool.QueueUserWorkItem(async (state) =>
//{
//    //SynchronizationContext.SetSynchronizationContext(sc);
//    await Utilities.DoWorkThreadpoolAsync(null);
//});

var awaitable = new EmulateADataPipelineCall(2000);
await awaitable;

Console.ReadLine();
