# Awaitable

Throughout these discussions I use the term *Threading Context* to describe the environment DotNetCore builds on the operating system threading infrastructure.  This is normally either a `Synchronisation Context` or the `Threadpool`.  

The core functionality of async behaviour is the implementation of `GetAwaiter`.  Any object implementing a `GetAwaiter` can be awaited by the *TPL*. 

I'll use the following terms:

 - An *Awaitable* is an object that executes some form of asynchronous behaviour and implements a `GetAwaiter` method. 
 - An *Awaiter* is an object returned by `GetAwaiter`.

An *Awaiter* must implement the following functionality:

```csharp
public struct MyAwaiter : INotifyCompletion
{
    public bool IsCompleted;
    public void OnCompleted(Action continuation);
    public void GetResult();
}
```

The awaiter providea: 

1. A bool property to detect if the awaitable is complete.
2. A method to post a continuation to be run when the awaitable is complete.
3. A method to get the result on completion.

`Task` in it's various guises implements this functionality.  It's `GetAwaiter` returns itself.

## Implementation

Implementing a customer awaiter is complex.  This one re-invents the wheel: an alternative version of `Task.Delay`.  The code is for demonstration only.  Do not use this in a production system.



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