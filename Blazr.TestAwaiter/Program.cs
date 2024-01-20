using Blazr.AsyncAwait.Async;
using Blazr.SyncronisationContext;

Utilities.WriteToConsole("Application started");

Console.ReadLine();

//var task = Task.Run(() =>
//{
//    var result = DoWorkAsync();
//});

var sc = new BlazrSynchronisationContext();
SynchronizationContext.SetSynchronizationContext(sc);

sc.Start();

sc.Post(DoWorkAsyncVoid, null);

sc.Post((state) =>
{
    Utilities.WriteToConsole("Application running after DoWorkAsyn Called");
}, 
null);

//Utilities.WriteToConsole("Application running after DoWorkAsyn Called");

Console.ReadLine();

async Task DoWorkAsync()
{
    Utilities.WriteToConsole("DoWorkAsync started");
    var testAwaiter = new TestAwaiter();
    await testAwaiter.IdleAsync(2000);
    Utilities.WriteToConsole("DoWorkAsync completed");
}

async void DoWorkAsyncVoid(object? state)
{
    Utilities.WriteToConsole("DoWorkAsync started");
    var testAwaiter = new TestAwaiter();
    await testAwaiter.IdleAsync(10000);
    Utilities.WriteToConsole("DoWorkAsync completed");
}
