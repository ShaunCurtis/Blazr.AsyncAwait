This Repo contains the following short articles about Asynchronous programming in Blazor and DotNetCore. 

1. [Async/Await](./Async-Await.md).
1. [Delay](./Delay.md).
1. [Yield](./Yield.md).
2. [Awaitable](./Awaitable.md).

Throughout these discussions I use the term *Threading Context* to describe the environment DotNetCore builds on the operating system threading infrastructure.  There are two:

The `Synchronisation Context`. 
The `Threadpool Context`.

It's important to understand the differences between them.

A `Synchronisation Context` provides an abstract threading context that guarantees a single thread of execution.  Two operations, such as updating two components at the same time, is guaranteed not to happen.

The `Threadpool Context` manages operations across a group of threads.  Code blocks are switched between threads based on load. 

.  These provide functionality based on the context:

1. Management of `System.Threading.Timers` to call callbacks when timers expire.
2. Management of `Awaiters` which we'll cover here.

Blazor adds extra functionality to manage posting  Renderer activity, such as servicing the Render queue and UI generated events.
