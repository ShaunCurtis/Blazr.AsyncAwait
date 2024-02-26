using Blazr.Async;


BlazrSynchronisationContext sc = new BlazrSynchronisationContext();
SynchronizationContext.SetSynchronizationContext(sc);
sc.Start();

PostToUI("Application started - hit a key to start");

// wait for a keyboard click to start
Console.ReadLine();

PostToUI("Application => Start running DoWorkAsync.");

sc.Post((state) => { _ = DoWorkAsync(); }, null);

PostToUI("Application => After DoWorkAsync Yields.");

// wait for a keyboard click to start
Console.ReadLine();

PostToUI("Application => Start running DoWorkAsynVoid.");

sc.Post(DoWorkAsyncVoid, null);

PostToUI("Application => After DoWorkAsyncVoid Yields.");

Console.ReadLine();


void PostToUI(string message)
{
    sc.Post((state) =>
    {
        Utilities.WriteToConsole(message);
    }, null);
}

async void DoWorkAsyncVoid(object? state)
{
    PostToUI("DoWorkAsync started");
    await MyAwaitable.Idle(3000);
    PostToUI("DoWorkAsync completed");
}

async Task DoWorkAsync()
{
    PostToUI("DoWorkAsync started");
    await MyAwaitable.Idle(3000);
    PostToUI("DoWorkAsync completed");
}
