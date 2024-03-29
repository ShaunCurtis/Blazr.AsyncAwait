## Hans Passant

[SO Answer 52687947/13065781](https://stackoverflow.com/a/52687947/13065781)

The word "capture" is too opaque, it sounds too much like that is something that the framework is supposed to. Misleading, since it normally does in a program that uses one of the default SynchronizationContext implementations. Like the one you get in a Winforms app. But when you write your own then the framework no longer helps and it becomes your job to do it.

The async/await plumbing gives the context an opportunity to run the continuation (the code after the await) on a specific thread. That sounds like a trivial thing to do, since you've done it so often before, but it is in fact quite difficult. It is not possible to arbitrarily interrupt the code that this thread is executing, that would cause horrible re-entrancy bugs. The thread has to help, it needs to solve the standard producer-consumer problem. Takes a thread-safe queue and a loop that empties that queue, handling invoke requests. The job of the overridden Post and Send methods is to add requests to the queue, the job of the thread is to use a loop to empty it and execute the requests.

The main thread of a Winforms, WPF or UWP app has such a loop, it is executed by Application.Run(). With a corresponding SynchronizationContext that knows how to feed it with invoke requests, respectively WindowsFormsSynchronizationContext, DispatcherSynchronizationContext and WinRTSynchronizationContext. ASP.NET can do it too, uses AspNetSynchronizationContext. All provided by the framework and automagically installed by the class library plumbing. They capture the sync context in their constructor and use Begin/Invoke in their Post and Send methods.

When you write your own SynchronizationContext then you must now take care of these details. In your snippet you did not override Post and Send but inherited the base methods. They know nothing and can only execute the request on an arbitrary threadpool thread. So SynchronizationContext.Current is now null on that thread, a threadpool thread does not know where the request came from.

Creating your own isn't that difficult, ConcurrentQueue and delegates help a lot of cut down on the code. Lots of programmers have done so, this library is often quoted. But there is a severe price to pay, that dispatcher loop fundamentally alters the way a console mode app behaves. It blocks the thread until the loop ends. Just like Application.Run() does.

You need a very different programming style, the kind that you'd be familiar with from a GUI app. Code cannot take too long since it gums up the dispatcher loop, preventing invoke requests from getting dispatched. In a GUI app pretty noticeable by the UI becoming unresponsive, in your sample code you'll notice that your method is slow to complete since the continuation can't run for a while. You need a worker thread to spin-off slow code, there is no free lunch.

Worthwhile to note why this stuff exists. GUI apps have a severe problem, their class libraries are never thread-safe and can't be made safe by using lock either. The only way to use them correctly is to make all the calls from the same thread. InvalidOperationException when you don't. Their dispatcher loop help you do this, powering Begin/Invoke and async/await. A console does not have this problem, any thread can write something to the console and lock can help to prevent their output from getting intermingled. So a console app shouldn't need a custom SynchronizationContext. YMMV.



ME

============================

Lets assume out initial code is a block of code posted to the synchronisation context.

The async code executes it runs code synchronously until it reaches a real wait: a step that needs to wait for some external action to happen and return a result.  In the synchronous world, this blocks the synchronisation context until the external action completes and unblocks.

In an asynchronous context, the waiter is run on separate background thread.  There's more.  The awaiter is also responsible for scheduling the rest of the the code in the posted block.    