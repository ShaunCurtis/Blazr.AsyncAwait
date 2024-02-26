using Blazr.Async;

var sc = new BlazrSynchronisationContext();
SynchronizationContext.SetSynchronizationContext(sc);
sc.Start();

PostToUI("Application started");

Console.ReadLine();

sc.Post(DoWorkAsyncVoid, null);

PostToUI("Application running after DoWorkAsyn Called");

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
