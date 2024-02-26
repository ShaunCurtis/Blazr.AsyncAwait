# Tasks

`Task` is an very interesting object.  Ask anyone what it is and you will get a variety of answers depending on how they use it.  Most of those answers, while not incorrect, will only express some of it's functionality.

In this article I'll try and cover most of it's multifaceted functionality

## Demo Environment

The demo code can be run in `Program` in a console application.  Console writing is encapsulated in a utility to write a message, a synchronisation context identifier and the execution thread.

```csharp
public static class Utilities
{
    public static void WriteToConsole(string startMessage)
    {
        string sc = SynchronizationContext.Current is null 
            ? " -- Not Set -- " 
            : SynchronizationContext.Current.GetHashCode().ToString();
 
        Console.WriteLine($"{startMessage} - ThreadId: {Thread.CurrentThread.ManagedThreadId} - SyncContext: {sc}");
    }
}
```

### Scheduling Work

Consider

```csharp
Utilities.WriteToConsole($"Main started - click to continue");

Console.ReadLine();

var task = Task.Run(() => {
    Utilities.WriteToConsole($"Scheduled Task");
});

Utilities.WriteToConsole($"Main Complete");
```

`Run` is a static method that schedules an action on the current scheduler.

You can write this manually like this:

```csharp
var task = new Task(() => {
    Utilities.WriteToConsole($"Scheduled Task");
});
task.Start();
```

### Providing Special Tasks

These include `Task.Yield`, `Task.Delay` and `Task.Completed`.

`Task.Delay` provides a Task where the continuation will run after the timer has expired.  While you can call `Task.Delay(1)`, the actual resolution of the wait will depend on the accuracy of the system clock.  On Windows systems this will be approximately 15ms. a delay of 1ms may be anywhere between 1 and 16 ms. 

`Task.Completed` is one of a group of static methods that return a Task in a specific state.  Theses include `Task.FromResult`, `Task.FromCancelled` and `Task.FromException`.  

`Task.CompletedTask` is a little special: there's a single instance in an application.  Everyone gets the same reference: there's no processing cost in creating an instance to return.

`Task.Yield` schedules the continuation immediately i.e. adds the contination to the end of the queue on the current context.  Note the subtle difference: there may be other scheduled blocks of code in front of the the continuatiuon. 

### Multi Task Handling

These provide functionality to wait on a group of tasks.  `Task.WhenAll` and `Task.WhenAny` yield by providing a Task as a return value. `Task.WaitAll` and `Task.WaitAny` block until true.

### Factory

`Task.Factory` provides ways to configure a lot of different options on a thread.  It's been superceeded by `Task.Run` in most instances [when running an action on a thread with the default settings].

> Quote from MS - should simply be thought of as a quick way to use Task.Factory.StartNew without needing to specify a bunch of parameters. It’s a shortcut

There are still places where it is useful.  The classic case is in starting a long running thread.  Using a threadpool thread for such a task is an abuse of the threadpool, so you need to get a dedicated thread like this:

```csharp
Task.Factory.StartNew(..., TaskCreationOptions.LongRunning);
```

## Awaitable

Task implements `GetAwait()` and therefore can be awaited.  `GetAwait` returns a `TaskAwaiter`.

### Continuations

Task.Continue with