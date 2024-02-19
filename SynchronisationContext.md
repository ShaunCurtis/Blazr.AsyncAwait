# The Async Series - Synchronisation Contexts

## A Little History

Applications originally ran on one thread.  Work blocked that thread while they executed.  UIs were unresponsive. 

Multithreading came along. Work could be shifted to other threads and the UI kept responsive.  This new functionality came at a price.  Three important issues are:

1. You need a framework to manage the threading environment.   
2. Objects passed around as part of work need to be thread safe.
3. You need a way to manage UI activity.

Two examples to illustrate these issues:

You have a `StringBuilder` instance that you're using as a log for some work.  If you schedule that work to run on different threads, you may have two threads calling `stringBuilder.WriteLine` at the same time.  `StringBuilder` isn't thread safe, so you end up with mixed up text in `StringBuilder`.   

Two blocks of work on the UI attempt to update the UI at the same time.  Will the UI handle this gracefully?  Often not.

To solve this problem, various frameworks (such as Windows Forms) provided thread management.  The problem was that different frameworks did it differently, so writing generic libraries for the different implementations was problematic. 

The **Synchronisation Context** was created to abstract the developer from the underlying implementation.  Each framework has it's own implementation of `SynchronisationContext`, but they all implement the base `SynchronisationContext` functionality.  And there's a common way to get the current `SynchronisationContext`.

So what is a **Synchronisation Context**?

The constructor looks like this.  A standalone object: nothing needed to get an instance up and running.

```csharp
SynchronizationContext()
```

It has a static nullable `Current` property, which returns the SC for the current thread.  That tells us a `Thread` object may have an SC attached to it, but as the return value is nullable, it also may not.

```csharp
//Gets the synchronization context for the current thread.
public SynchronizationContext? Current { get; }
```

It has a static Method `SetSynchronizationContext` which sets the SC for the current value to the provided instance.  The argument is nullable, so we can clear the thread's SC. 

```csharp
//Sets the current synchronization context for the current thread.
public static void SetSynchronizationContext (SynchronizationContext? syncContext);
```

The two most important instance methods are `Post` and `Send`, which we use to queue `SendOrPostCallback` delegates to the SC.

```csharp
//Dispatches an asynchronous message to a synchronization context.
Post(SendOrPostCallback, Object)	

//Dispatches a synchronous message to a synchronization context.
Send(SendOrPostCallback, Object)	
```

A `SendOrPostCallback` delegate is defined as:

```csharp
public delegate void SendOrPostCallback(object? state);
```

So `Post(SendOrPostCallback, Object)` posts any method that comforms to the delegate pattern with the `state` object the delegate requires.  Note `state` is nullable so may be `null`.


There are other methods for more complex interaction.

What this shows us is we can construct a concrete SC instance, attach it to a thread and pump delegate methods into it to execute.

The complexity of the Synchronisation Context is in the implementation: what functionality the specific framework needs.

I'll jump now to Blazor.

To quote from the official documentation:

> Blazor uses a synchronization context (SynchronizationContext) to enforce a single logical thread of execution. A component's lifecycle methods and event callbacks raised by Blazor are executed on the synchronization context.

At any given point in time, work is performed on exactly one thread, which yields the impression of a single logical thread. No two operations execute concurrently.

It's very important to understand that this doesn't dictate that a SC runs on a single thread.  It may run on more, with work executing on multiple threads, but sequentially, not concurrently.

There are two reasons for this single logical thread of execution:

1. Blazor WebAssembly runs in a single threaded environment.  For compatibility between Server and WASM, they both need to run under the same set of conditions.
1. All UI activity is sequential.  It's thread safe.

Blazor has built in functionality that detects if you are trying to execute UI update code outside the SC and throws an exception.

## Message Loop

So what happens when we `Post` a `SendOrPostCallback`.  That depends on the actual implementation, but within each framework there's a message queue buried somewhere that de-queues delegates from the queue and invokes them on a thread.

## Asynchronous Operations

While we can `Post` a `SendOrPostCallback`, there's no return value.  It's a one way operation.  There's nothing to await.  Our problem here is our misconception that this is what the compiled code looks like.

```csharp
private async Task Clicked()
{
    await MyAsyncRoutine();
}
```

The method in which this code runs is rebuilt into a private state machine class within the owning class and the actual method refactored to look like this:

```csharp
private Task Clicked()
{
    var stateMachine = new Clicked_StateMachine(this);
    stateMachine.Execute();
    return stateMachine.Task;
}
```

The Render process can then queue this onto the synchronisation context like this:

```csharp
SynchronizationContext.Current?.Post( async (state) => { await Clicked(); }, null);
```

Events handlers triggered in the render process are fire and forget events from the browser.  There's no need to await anything.  The delegate code applies whatever mutations it need to and then queues a render event onto the renderer to apply those changes to the UI. 

When a yield occurs within the state machine code, the awaiter runs on a separate thread. When the awaiter completes, the continuation is posted back to the synchronisation context.  The synchronisation context manages these outstanding asynchronous operations using a counter.  It increments when the synchronisation context is captured, and decrements when a completion is queued to the synchronisation context.

### Some Testing

A simple set of services to log threads and their synchronisation contexts to the console.

```csharp
public abstract class SimpleService
{
    public void LogMessage()
    {
        Console.WriteLine($"Thread: {Thread.CurrentThread.ManagedThreadId} - SC: {SynchronizationContext.Current?.GetHashCode()} - {this.GetHashCode()}:{this.GetType().Name} LogMessage");
    }

    public async Task LogMessageAsync()
    {
        await Task.Delay(100);
        Console.WriteLine($"Thread: {Thread.CurrentThread.ManagedThreadId} - SC: {SynchronizationContext.Current?.GetHashCode()} - {this.GetHashCode()}:{this.GetType().Name} LogMessage");
    }
}

public class SingletonService : SimpleService { }
public class ScopedService : SimpleService {}
public class TransientService : SimpleService { }
```

Program:

```csharp
builder.Services.AddSingleton<SingletonService>();
builder.Services.AddScoped<ScopedService>();
builder.Services.AddTransient<TransientService>();
```

Counter:

```csharp
@page "/counter"
@inject SingletonService SingletonService
@inject ScopedService ScopedService
@inject TransientService TransientService

<PageTitle>Counter</PageTitle>

<h1>Counter</h1>

<p role="status">Current count: @currentCount</p>

<button class="btn btn-primary" @onclick="IncrementCount">Click me</button>

@code {
    private int currentCount = 0;

    private async Task IncrementCount()
    {
        currentCount++;
        Console.WriteLine($"============================================================");
        Console.WriteLine($"Thread: {Thread.CurrentThread.ManagedThreadId} - SC: {SynchronizationContext.Current?.GetHashCode()} - {this.GetHashCode()}:{this.GetType().Name} IncrementCount");

        this.SingletonService.LogMessage();
        this.ScopedService.LogMessage();
        this.TransientService.LogMessage();

        await this.SingletonService.LogMessageAsync();
        await this.ScopedService.LogMessageAsync();
        await this.TransientService.LogMessageAsync();
    }
}
```

And some results:

```text
============================================================
Thread: 10 - SC: 65049747 - 45961583:Counter IncrementCount
Thread: 10 - SC: 65049747 - 1466578:SingletonService LogMessage
Thread: 10 - SC: 65049747 - 59247523:ScopedService LogMessage
Thread: 10 - SC: 65049747 - 61547269:TransientService LogMessage
Thread: 19 - SC: 65049747 - 1466578:SingletonService LogMessageASync
Thread: 9 - SC: 65049747 - 59247523:ScopedService LogMessageASync
Thread: 9 - SC: 65049747 - 61547269:TransientService LogMessageASync
============================================================
Thread: 7 - SC: 65049747 - 45961583:Counter IncrementCount
Thread: 7 - SC: 65049747 - 1466578:SingletonService LogMessage
Thread: 7 - SC: 65049747 - 59247523:ScopedService LogMessage
Thread: 7 - SC: 65049747 - 61547269:TransientService LogMessage
Thread: 10 - SC: 65049747 - 1466578:SingletonService LogMessageASync
Thread: 10 - SC: 65049747 - 59247523:ScopedService LogMessageASync
Thread: 10 - SC: 65049747 - 61547269:TransientService LogMessageASync

=> <F5> at this point to reset the SPA
============================================================
Thread: 21 - SC: 48907957 - 51812814:Counter IncrementCount
Thread: 21 - SC: 48907957 - 1466578:SingletonService LogMessage
Thread: 21 - SC: 48907957 - 57840904:ScopedService LogMessage
Thread: 21 - SC: 48907957 - 47566865:TransientService LogMessage
Thread: 19 - SC: 48907957 - 1466578:SingletonService LogMessageASync
Thread: 19 - SC: 48907957 - 57840904:ScopedService LogMessageASync
Thread: 19 - SC: 48907957 - 47566865:TransientService LogMessageASync

```

Some points:

1. The synchronisation context is the same instance on the various threads that used. 
1. The synchronisation context is the same instance throughout the SPA session.
1. The synchronous operations all run on the same thread as runs `IncrementCount`.
1. The thread changes between calls to `IncrementCount`.

The UI handler in the hub session receives button click event from the browser via JSInterop.  It `posts` the handler onto it's synchronisation context.  The synchronisation context acquires a thread from the threadpool and sets it's synchronisation context.  It invokes the `SendOrPostCallback` delegate on the acquired thread.


The code runs on that thread synchronously until it encounters the first yield from an await.

```text
Thread: 10 - SC: 65049747 - 45961583:Counter IncrementCount
Thread: 10 - SC: 65049747 - 1466578:SingletonService LogMessage
Thread: 10 - SC: 65049747 - 59247523:ScopedService LogMessage
Thread: 10 - SC: 65049747 - 61547269:TransientService LogMessage
```

At this point `SingletonService.LogMessageASync` yields and the synchronisation context runs the continuation on a separate thread it has acquired.

The continuation for the other two services `ScopedService.LogMessageASync` and `TransientService.LogMessageASync` are run on another thread acquired by the synchronisation context.

If you run the click event several times, you will see various variations on the async events.  The first sync methods always run on the same thread, but the continuations from the yielding methods may be run on other threads.

The key point is that although different threads may be being used, there's a single sequential execution of the code controlled by the synchronisation context, 



