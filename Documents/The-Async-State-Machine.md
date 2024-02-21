# The Async Series - The Async State Machine

*Async/Await* uses the *Async State Machine* to implement asynchronous behaviour.

This high level code:

```csharp
string message = "Idle";
    
public async Task MethodAsync() 
{
    Console.WriteLine("Step 1");
    message = "Step 1";
    await Task.Delay(500);

    Console.WriteLine("Step 2");
    message = "Step 2";
    await Task.Delay(600);
        
    Console.WriteLine("Step 3");
    message = "Step 3";
    await Task.Delay(700);

    Console.WriteLine("Final Step");
    message = "Finished";
}
```

Is refactored by the compiler into:

1. An async state machine object within the parent class.
1. A refactored `MethodAsync` to configure and start the state machine.

In this document I'll build a state machine, similar to that built by the compiler, to demonstrate the functionality *Async/Await* builds. 

## The State Machine

The state machine skeleton looks like this.

```csharp
private class AsyncStateMachine :IAsyncStateMachine
{
    public AsyncTaskMethodBuilder Builder;
    public MyClass Parent = new();
    public int State = -2;

   public void SetStateMachine(IAsyncStateMachine stateMachine) { }

    public void MoveNext() {}
}
```

It has:

   1. Implements the `IAsyncStateMachine` interface.
   2. Creates a `AsyncTaskMethodBuilder` instance.
   3. Uses an integer to maintain state.
   4. Has a `MoveNext` method to execute the current step and increment the state.
   1. `SetStateMachine` is part of the `IAsyncStateMachine` interface.

The compiler breaks the original code into steps, split on each `await`.  Each step is given a state number and at the end of each step the await statement is executed and tested for completion.

THe state machine uses more primitive *TPL* functionality.  It gets the awaiter for the async method, and adds continuations using the *awaiter* pattern method `OnCompleted`.

If:

 - The async method returns an incomplete awaiter, it's yielded.  Add a continuation to the awaiter to call this method and exit.

 - The async method runs to completion and returns a completed awaiter, `goto` the next state step.  Don't create a continuation, just continue synchronous execution of the code on the same thread.      

```csharp
case x:
{
    // run the synchronous code

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
    TaskAwaiter awaiter = default(TaskAwaiter);

    switch (State)
    {
        default:
            Console.WriteLine("Step 1");
            this.Parent.message = "Step 1";
            awaiter = Task.Delay(500).GetAwaiter();

            if (awaiter.IsCompleted == false)
            {
                this.State = 0;
                var stateMachine = this;
                this.Builder.AwaitUnsafeOnCompleted(ref awaiter, ref stateMachine);
                return;
            }
            goto case 0;

        case 0:
            Console.WriteLine("Step 2");
            this.Parent.message = "Step 2";
            awaiter = Task.Delay(600).GetAwaiter();

            if (awaiter.IsCompleted == false)
            {
                this.State = 1;
                var stateMachine = this;
                this.Builder.AwaitUnsafeOnCompleted(ref awaiter, ref stateMachine);
                return;
            }

            goto case 1;

        case 1:
            Console.WriteLine("Step 3");
            this.Parent.message = "Step 3";
            awaiter = Task.Delay(700).GetAwaiter();

            if (awaiter.IsCompleted == false)
            {
                this.State = 2;
                var stateMachine = this;
                this.Builder.AwaitUnsafeOnCompleted(ref awaiter, ref stateMachine);
                return;
            }

            goto case 2;

        case 2:
            Console.WriteLine("Final Step");
            this.Parent.message = "Finished";
            this.State = -2;

            break;
    };

    this.Builder.SetResult();
    return;
}
```

The calling method looks like this:

```csharp
    // Set up the state machine and Task builder
    var stateMachine = new AsyncStateMachine(this);
    stateMachine.Builder = AsyncTaskMethodBuilder.Create();

    // Set the initial state and make the first move
    stateMachine.State = -1;
    stateMachine.Builder.Start(ref stateMachine);

    // Return the Task associated with the AsyncTaskMethodBuilder.
    return stateMachine.Builder.Task;
```

If the step's async call returns an incomplete task, `MoveNext` queues a continuation on the awaiter to call itself and exits.  It returns control to the caller. Executing the next state is no longer the responsibility of the current executing code.  Restarting `MoveNext` at the next step is the responsibility of background process behind the awaiter.  Where that gets posted and executed is the responsibility of the *awaiter* and it's backing process.  Task based *awaiters* use the `ConfigureAwait` settings to determine the context on which to post the continuation.

Execution steps through each state until it reaches the final step.  It has no awaiter.  It breaks from the switch, sets the `AsyncTaskMethodBuilder` to complete and exists.

## The Compiler Generated State Machine

First:

1. The code is optimized for a sequential synchronous operation.  No yields.
2. The code is not written for humans to read.
3. The compiler applies naming conventions that use characters illegal in human generated code to  ensure there are no name conflicts.
4. THe compiler switches from an `if` driven `MoveNext` to a `switch` driven version when there are more than 2 steps.
5. `MoveNext` looks back-to-front because it's generated from the end backwards.
6. A state machine is compiled as a class in debug mode and a struct in release mode.
7. The code may seem a little clunking and in need of refactoring, but it's generated by a builder to a  pescription.  And designed for speed not elegance.

The compiler generated code is shown below.  You should now be able to unpick it and understand what it's doing.


```csharp
public class C
{
    [StructLayout(LayoutKind.Auto)]
    [CompilerGenerated]
    private struct <MethodAsync>d__1 : IAsyncStateMachine
    {
        public int <>1__state;

        public AsyncTaskMethodBuilder <>t__builder;

        [Nullable(0)]
        public C <>4__this;

        private TaskAwaiter <>u__1;

        private void MoveNext()
        {
            int num = <>1__state;
            C c = <>4__this;
            try
            {
                TaskAwaiter awaiter;
                switch (num)
                {
                    default:
                        Console.WriteLine("Step 1");
                        c.message = "Step 1";
                        awaiter = Task.Delay(500).GetAwaiter();
                        if (!awaiter.IsCompleted)
                        {
                            num = (<>1__state = 0);
                            <>u__1 = awaiter;
                            <>t__builder.AwaitUnsafeOnCompleted(ref awaiter, ref this);
                            return;
                        }
                        goto IL_008d;
                    case 0:
                        awaiter = <>u__1;
                        <>u__1 = default(TaskAwaiter);
                        num = (<>1__state = -1);
                        goto IL_008d;
                    case 1:
                        awaiter = <>u__1;
                        <>u__1 = default(TaskAwaiter);
                        num = (<>1__state = -1);
                        goto IL_0101;
                    case 2:
                        {
                            awaiter = <>u__1;
                            <>u__1 = default(TaskAwaiter);
                            num = (<>1__state = -1);
                            break;
                        }
                        IL_0101:
                        awaiter.GetResult();
                        Console.WriteLine("Step 3");
                        c.message = "Step 3";
                        awaiter = Task.Delay(700).GetAwaiter();
                        if (!awaiter.IsCompleted)
                        {
                            num = (<>1__state = 2);
                            <>u__1 = awaiter;
                            <>t__builder.AwaitUnsafeOnCompleted(ref awaiter, ref this);
                            return;
                        }
                        break;
                        IL_008d:
                        awaiter.GetResult();
                        Console.WriteLine("Step 2");
                        c.message = "Step 2";
                        awaiter = Task.Delay(600).GetAwaiter();
                        if (!awaiter.IsCompleted)
                        {
                            num = (<>1__state = 1);
                            <>u__1 = awaiter;
                            <>t__builder.AwaitUnsafeOnCompleted(ref awaiter, ref this);
                            return;
                        }
                        goto IL_0101;
                }
                awaiter.GetResult();
                Console.WriteLine("Final Step");
                c.message = "Finished";
            }
            catch (Exception exception)
            {
                <>1__state = -2;
                <>t__builder.SetException(exception);
                return;
            }
            <>1__state = -2;
            <>t__builder.SetResult();
        }

        void IAsyncStateMachine.MoveNext()
        {
            //ILSpy generated this explicit interface implementation from .override directive in MoveNext
            this.MoveNext();
        }

        [DebuggerHidden]
        private void SetStateMachine(IAsyncStateMachine stateMachine)
        {
            <>t__builder.SetStateMachine(stateMachine);
        }

        void IAsyncStateMachine.SetStateMachine(IAsyncStateMachine stateMachine)
        {
            //ILSpy generated this explicit interface implementation from .override directive in SetStateMachine
            this.SetStateMachine(stateMachine);
        }
    }

    private string message = "Idle";

    [AsyncStateMachine(typeof(<MethodAsync>d__1))]
    public Task MethodAsync()
    {
        <MethodAsync>d__1 stateMachine = default(<MethodAsync>d__1);
        stateMachine.<>t__builder = AsyncTaskMethodBuilder.Create();
        stateMachine.<>4__this = this;
        stateMachine.<>1__state = -1;
        stateMachine.<>t__builder.Start(ref stateMachine);
        return stateMachine.<>t__builder.Task;
    }
}
```
