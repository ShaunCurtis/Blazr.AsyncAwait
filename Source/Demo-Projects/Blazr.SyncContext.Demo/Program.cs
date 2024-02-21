using Blazr.SyncronisationContext;

BlazrSynchronisationContext sc = new BlazrSynchronisationContext();
sc.Start();

PostToUI("Application started.");

Console.ReadLine();

PostToUI("Application => Post Started.");

sc.Post(Utilities.DoWorkVoidAsync, null);

PostToUI("Application => Post After.");

Console.ReadLine();

PostToUI("Application => ThreadPool Started.");

ThreadPool.QueueUserWorkItem(async (state) =>
{
    SynchronizationContext.SetSynchronizationContext(sc);
    await Utilities.DoWorkThreadpoolAsync(null);
});

PostToUI("Application => ThreatPool After.");

Console.ReadLine();

PostToUI("Application => TaskRun Started.");

var task = Task.Run(async () =>
{
    SynchronizationContext.SetSynchronizationContext(sc);
    await Utilities.DoWorkTaskAsync();
});

PostToUI("Application => TaskRun After.");

Console.ReadLine();


void PostToUI(string message)
{
    sc.Post((state) =>
    {
        Utilities.WriteToConsole(message);
    }, null);
}
