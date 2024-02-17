# Understanding Asynchronous Behaviour

This is one of those *fuzzy* topics for many programmers.  To quote Stephen Tomb, one of the authors of Async/Await:

> [It's] both viable and extremely common to utilize the functionality without understanding exactly what is going on under the covers. You start with a synchronous method ...  sprinkle a few keywords, change a few method names, and you end up with [an] asynchronous method instead. 

There are a few very good articles available on the subject, I've included references to those I've used as source material for this article in the appendix.  Unfortunately most assume a level of knowledge most mortal programmers don't have.

> **Be aware**: there are also a lot of uninformative or simply bad articles along with the usual collection of regurgitated material.

In this article I'll attempt to bring that required knowledge down to a more normal level.

## Some Key Concepts

There are some key programming concepts not to loose sight of when trying to understand asynchronous behaviour.

1. There's no black magic going on.  It may seem so at times, but there's a logical explanation.

1. Code executes on threads synchronously.

2. A thread is blocked when the code it's running is waiting for something to happen.

3. A thread can do **one** thing at a time.  It can't wait for a timer to complete and excute code simultaneously..

4. When a block of code yields control back to the caller, it's finished:  there's no afters.  The thread is free for the next job.

If you've understood these points so far, there's a logical question.

> If I await a database call to say get a list, how does the code after the await get executed?

Consider this code [Task.Delay mocks the database call].

If `Task.Delay` returns, what happens to `_sb.AppendLine("Complete");`?

```csharp
    private async Task Clicked()
    {
        _sb.AppendLine("Started");
        await Task.Delay(1000);
        _sb.AppendLine("Complete");
    }
```

Conceptually, when a block of code yields control back to the caller, the *after* code is bundled up as a new block of code called the continuation, and passed to the background process to run when it completes.  `Clicked` has passed the buck to someonme else.

We can rewrite `Clicked` to make this a little clearer.

```csharp
    private Task Clicked()
    {
        _sb.AppendLine("Started");

        var task = Task.Delay(1000);
        task.ContinueWith((awaitable) =>
        {
            _sb.AppendLine("Complete");
        });

        return task;
    }
```

`Async/Await` has gone.  We have a block of synchronous code.  The database call running on a separate thread returns a task when it yields.  We pass the *continuation* to the task and return the Task to the caller. 

`Clicked` has completed on the *synchronisation context*.  The database operation completes at some point, and schedules the continuation.  In this case that's when the timer expires.  The continuation runs on either a threadpool thread or the *synchronisation context* dependant on the task configuration.  In this case on the *synchronisation context*. 

A logical question is:  

> How does all this code refactoring take place at runtime?

It isn't.  It's transformed at compile time.  The compiled code looks nothing like your original.

Our refactored code works for a single continuation, but would quickly gets complex with multiple awaits.  The compiler solves this problem with the async state machine.

## The Async State Machine

Here's a very simplistic state machine based on our code.  There's no exception handling or cancellation.

Initialization gets a reference to the owning object [the code blocks often access and mutate internal class objects], sets up a new `TaskCompletionSource` and the initial state.

```csharp
    private class StateMachine
    {
        private MyComponent _owner;
        private TaskCompletionSource _tcs;
        private int _state;

        public Task Task => _tcs.Task;

        public StateMachine(MyComponent demo)
        {
            _owner = demo;
            _tcs = new();
            _state = 0;
        }

        public void MoveNext()
        {
            if (_state == 0)
            {
                _owner._sb.AppendLine("Started");
                _state++;
                var awaiter = Task.Delay(1000).GetAwaiter();

                if (!awaiter.IsCompleted)
                {
                    awaiter.OnCompleted(MoveNext);
                    return;
                }
            }

            if (_state == 1)
            {
                _owner._sb.AppendLine("Complete");
            }

            _tcs.SetResult();
            return;
        }
    }
```

State 0 is the initial step.  It runs the first synchronous code block and then gets the awaiter for the awaitable process.  

At this point the process either:
1. Runs to completion synchronously [and returns a completed Task], or 
2. Yields and returns an incomplete Task.  
 
If it completes, the code falls through to the second state and continues execution synchronously on the same thread [in this case the synchronisation context].

If it yields, it adds a continuation to the awaiter that calls `MoveNext`, and returns.  The state machine is now at state 1, so when it invokes `MoveNext` in the continuation, it runs the code for state 1.

This continues, stepping through each await, until the final code block.  In our case state 1.  The code block has no awaitable, so falls through the bottom of the state logic.  It sets the result on the `TaskCompletionSource` and completes.

The calling method now looks like this.  It creates and starts the state machine and then returns the `Task` of the `TaskCompletionSource`.

```csharp
    private Task Click()
    {
        var stateMachine = new StateMachine(this);
        stateMachine.MoveNext();
        return stateMachine.Task;
    }
```

## Awaitables and Awaiters

The comnpiler will only let you `await` a method that implements the *awaitable* pattern.

```csharp
public class/struct MyAwaitable
{
    public Awaiter GetAwaiter();
}
```

*Awaiter* needs to implement the *Awaiter* pattern.

```csharp
public class/struct MyAwaiter : INotifyCompletion
{
    public bool IsCompleted;
    public void OnCompleted(Action continuation);
    public void GetResult();
}
```

You can't await an `Int32`.  Or can you?

Can:

```csharp
  await Task.Delay(500);
```

be coded as:

```csharp
   await 500;
```

It's certainly succinct.

It turns out you can.  You just need to implement the awaitable pattern on `Int32`.

It's this simple. 

```csharp
public static TaskAwaiter GetAwaiter(this Int32 milliseconds)
{
    return Task.Delay(milliseconds).GetAwaiter();
}
```

Add `GetAwaiter` as an extension method, call `Task.Delay(milliseconds)` and return it's awaiter.

## Tasks

`Task`, in all it's guises, is an implementation of an awaitable.  It returns a `TaskAwaiter` that implements the *awaiter* pattern.  It's designed to work closely with `Async/Await` and the *Async State Machine*.

A `Task` is a simple `struct` that represents an asynchronous operation. It's a handle that provides a communications channel between the caller and the asynchronous background operation.

A task is returned in one of four states:

1. Completed - probably the most common.  It's safe to get the result.
2. Not Completed - there's a background process running somewhere else that's in-process.  The Task's result isn't yet set.  If you try and get it, you will block your thread.
3. Faulted - A exception has occured which the task returns.
4. Cancelled - A cancellation token request was successful.  The operation was cancelled.

The asyncronous background operation controls the task's state.  When it completes, it:

1. Sets the task's state to Completed 
2. Sets the task's result [if there is one].
3. Schedules any registered continuations.

You can attach a continuation to any task regardless of who created it.  That continuation will be executed immediately if the task has completed, or added to the awaiter's continuation collection if not.

Where continuations run is based on ConfigureAwait: 

1. false - on any threadpool thread. 
2. true - on the synchronisation context if one exists. 

There are two facets to a`Task`.  The public provides state information and a method to add continuations. The internal, accessed by the background operation through `AsyncTaskMethodBuilder`.

The TPL library provides us with the `TaskCompletionSource` object to generate manually controlled tasks.

## A Real Async State Machine

Go to [SharpLab](https://sharplab.io/).  Set the output to *C#* and enter the following code:

```csharp
using System;
using System.Threading.Tasks;

public class C {
    public async Task DoSomeWorkAsync() {
        Console.WriteLine("Starting");
        await DoSomethingAsync();
        Console.WriteLine("Finished");
    }
    
    private Task DoSomethingAsync()
    {
        return Task.Delay(500);
    }
}
```

The generated code is complex and unrecognisable. You should be able to discern:

1. A private *Async State Machine* within you parent class.
2. A refactored `DoSomeWorkAsync`.
 
`Async` and `await` have disappeared.

Look at the state machine.  The original code block has been split into `n+1` states and code blocks split at the `await` statements.

The state machine provides a public Task object [through the `AsyncTaskMethodBuilder`] which is returned to the caller when the state machine yields control.

The refactored `DoSomeWorkAsync` creates and starts the state machine, and on a yield, returns the state machine's Task to the caller.

```csharp
    [AsyncStateMachine(typeof(<DoSomeWorkAsync>d__0))]
    [DebuggerStepThrough]
    public Task DoSomeWorkAsync()
    {
        <DoSomeWorkAsync>d__0 stateMachine = new <DoSomeWorkAsync>d__0();
        stateMachine.<>t__builder = AsyncTaskMethodBuilder.Create();
        stateMachine.<>4__this = this;
        stateMachine.<>1__state = -1;
        stateMachine.<>t__builder.Start(ref stateMachine);
        return stateMachine.<>t__builder.Task;
    }
```

`__builder.Start` internally calls `MoveNext`,  the first block runs synchronously to the final async operation [the *await* line] and increments the state. The block either completes or yields control.

If the async operation completes, then execution falls through to the next block, and so on... with everything executing synchronously on the same thread. 

If the async operation yields [returns a not complete awaitable such as a Task], the state machine adds a continuation to the awaitable to call `MoveNext` and completes.

When the async operation completes on it's background thread, it queues the continuation to run [normally on the synchronisation context].  The continuation "re-enters" the state machine and executes the next state code block.

The final state block has no final async operation so falls through to the bottom where it sets the state machine's own Task result and state to completed.

## So What Have We Learnt

All code executes synchronously on a thread.  We create asynchronous behaviour by combining:

1. Multiple threads executing sychronous code in parallel. 
2. Background threads running message loops to service work queues. 
3. Mechanisms such as the *synchronisation context* and the `Threadpool` to manage threads and control thread interaction.

`Async/Await` is just syntactic sugar. The compiler tranforms our high level code into *TPL* primitive code.  It takes the hard work our of building asynchronous behaviour into our applications, and stops us making a lot of [silly] mistakes.

## References

The primary resources for this article were:

[Stephen Toub's how await works](https://devblogs.microsoft.com/dotnet/how-async-await-really-works/)

[Sergey Tepliakov's dissecting async](https://devblogs.microsoft.com/premier-developer/dissecting-the-async-methods-in-c/)

[Stephen Toub's Blog await anything](https://devblogs.microsoft.com/pfxteam/await-anything/)

[Stephen Cleary's various airings on the topic such as this one](https://blog.stephencleary.com/2023/11/configureawait-in-net-8.html)

