# Awaitable

Throughout these discussions I use the term *Threading Context* to describe the environment DotNetCore builds on the operating system threading infrastructure.  This is either the `Synchronisation Context` or the `Threadpool`.  These provide functionality based on the context:

1. Management of `System.Threading.Timers` to call callbacks when timers expire.
2. Management of `Awaiters` which we'll cover here.

Blazor adds extra functionality to manage posting Renderer activity, such as servicing the Render queue and UI generated events.

The core functionality of async behaviour is the implementation of `GetAwaiter`.  Any object implementing a `GetAwaiter` can be awaited by the threading context. 

`GetAwaiter` must implement this functionality:

```csharp
public struct MyAwaiter : INotifyCompletion
{
    public bool IsCompleted;
    public void OnCompleted(Action continuation);
    // Returns what you need
    public void GetResult();
}
```
`Task` implements these three methods and it's `GetAwaiter` returns itself.

When an object yields the threading context calls `GetAwaiter` to get an object that has: 

1. A way to detect when the awaitable is complete.
2. A context to execute the continuation.
3. A return result on completion.

We'll look at how this works in practice in the *Async/Await* section.

Implementing a customer awaiter is complex and beyond the scope of this article.

### Some Key Points

1. A call to a method returning a Task returns a `Task<T>`, not `T`.  The way you write the code:

```csharp
var result = await DoSomeAsyncWork();
```

suggests result is `T`.  The Dev environment even tells you so.  That's just syntactic sugar.  Behind the scenes the code is calling `GetResult()` on the completed `Task<T>`.

Miss out the `await` and `result` with now be a `Task<T>.
  
2. An awaiter needs to know where to run it's continuation.  While a custom awaiter does not need to respect `SynchronizationContext.Current` all the `Task` and `ValueTask` awaiter do if one exists.  Note that this only applies where `ConfigureAwait` is set to true [the default]. 

3. A Task method that actually yields control will be running on a separate thread.  It's waiting on a result so blocks that thread until it completes. 

4. It should be clear why calling `GetResult` blocks the current thread and causes deadlocks.