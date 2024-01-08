# Awaitable

Throughout these discussions I use the term *Threading Context* to describe the environment DotNetCore builds on the operating system threading infrastructure.  This is either the `Synchronisation Context` or the `Threadpool`.  These provide functionality based on the context:

1. Management of `System.Threading.Timers` to call callbacks when timers expire.
2. Management of `Awaiters` which we'll cover here.

Blazor adds extra functionality to manage posting Renderer activity, such as servicing the Render queue and UI generated events.

The core functionality of async behaviour is the implementation of `GetAwaiter`.  Any object implementing a `GetAwaiter` which returns an object that implements *IsCompleted/OnCompleted/GetResult* can be awaited by the threading context. 

The core functionality of the returned object is:

```csharp
public struct MyAwaiter : INotifyCompletion
{
    public bool IsCompleted;
    public void OnCompleted(Action continuation);
    // Returns what you need
    public void GetResult();
}
```
`Task` implements these three methods and a `GetAwaiter` that returns itself.

When an object yields the threading context calls `GetAwaiter` to get an object that has: 

1. A way to detect when the awaitable is complete.
2. A context to execute the continuation.
3. A return result on completion.

We'll look at how this works in practice in the *Async/Await* section.

An important point is that an awaitable completes when it returns a result, not when `IsCompleted` is set. 

When you await a task, [by default] the awaiter will capture the current SynchronizationContext [if there was one] - note the awaiter will be running on a different thread,  When the task completes, it will `Post` the supplied continuation delegate back to the SynchronizationContext, rather than running the delegate on whatever thread the task completed [or scheduling it to run on the ThreadPool].

