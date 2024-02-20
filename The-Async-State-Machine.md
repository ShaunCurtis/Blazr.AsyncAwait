# The Async Series - The Async State Machine

The *Async State Machine* is at the heart of `Aync/Await`.

When you write this high level code:

```csharp
    public async Task MethodAsync() 
    {
        Console.WriteLine("Step 1");
        await Task.Delay(500);

        Console.WriteLine("Step 2");
        await Task.Delay(600);

        Console.WriteLine("Final Step");
    }
```

The compiler refactors it into:

1. An async state machine object within the container class.
1. A refactored `Clicked`.

The State Machine Skeleton looks like this.

It has:

   1. Captures the parent so it can access it's internal methods in each state step.
   2. Creates a `TaskCompletionSource` instance and exposes it's `Task` as a public readonly property.
   3. Uses an integer to maintain state.
   4. Has a `MoveNext` method to execute the current step and increment the state.
  

```csharp
private class AsyncStateMachine
{
    private readonly TaskCompletionSource _tcs = new();
    private readonly Program _program;
    private int _state;

    public Task Task => _tcs.Task;
    
    public AsyncStateMachine(Program program)
        => _program = program;

    public void MoveNext() {}
}
```

The compiler breaks the original code into steps, split on each `await`.  Each step is given a state number and at the end of each step the await statement is executed and tested for completion.

Note that we are stepping down into more primitive *TPL* functionality.  We get the awaiter for our async method, and we add the continuation with the *awaiter* pattern method `OnCompleted`.

If the async method returns an incomplete awaiter, it's yielded.  We add a continuation to the awaiter to call this method when it completes and we return as completed.

If the async method runs to completion and returns a completed awaiter, we `goto` the next state step.  We don't create a continuation, just continue synchronous execution of the code on the same thread.      

```csharp
case x:
{
    // run the synchronius code

    var awaiter = MyMethodAsync().GetAwaiter();

    if (awaiter.IsCompleted == false)
    {
        _state = x + 1;
        awaiter.OnCompleted(this.MoveNext);
        return;
    }

    goto case next;
}
```

The full `NextStep` looks like this:

```csharp
public void MoveNext()
{
    switch (_state)
    {
        default:
            Console.WriteLine("Step 1");
            var awaiter = Task.Delay(500).GetAwaiter();

            if (awaiter.IsCompleted == false)
            {
                _state = 0;
                awaiter.OnCompleted(this.MoveNext);
                return;
            }
            goto case 0;

        case 0:
            Console.WriteLine("Step 2");
            var awaiter0 = Task.Delay(600).GetAwaiter();

            if (awaiter0.IsCompleted == false)
            {
                _state = 1;
                awaiter0.OnCompleted(this.MoveNext);
                return;
            }

            goto case 1;

        case 1:
            Console.WriteLine("Final Step");
            _state = -2;

            break;
    };

    _tcs.SetResult();
    return;
}
```

The calling method does this:

```csharp
    var stateMachine = new AsyncStateMachine(this);
    stateMachine.MoveNext();
    return stateMachine.Task;
```

The most important concept to understand is that if the async call at the bottom of the step returns an incomplete task, `MoveNext` queues a continuation on the awaiter to call itself and exits.  It returns control to the caller.

Executing the next state is no longer the responsibility of the current executing block of code.  Restarting `MoveNext` at the next step is the responsibility of background process behind the awaiter.

Where that gets posted and executed is the responsibility of the awaiter and it's backing process.  Task based awaiters use the `ConfigureAwait` settings to determine the context on which to post the continuation.

Execution steps through each state until it reaches the final step.  Ih has no awaiter.  Instead it sets the `TaskCompletionSourrce` instance to complete and exists.



### Take Aways

There are no `async` or `await` statements in the morphed code.  There's just code running on threads (and blocking them), and getting posted to the synchronisation context to be run sequentially.