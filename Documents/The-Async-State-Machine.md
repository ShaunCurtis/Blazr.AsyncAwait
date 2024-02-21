# The Async Series - The Async State Machine

*Async/Await* is high level syntatic sugar: an instruction to the compiler to refactor the method into an *Async State Machine*.

*Async/Await* methods are refactored into:

1. A private *async state machine* object within the parent class.
1. A refactored `MethodAsync` to configure and start the state machine.

In this document I'll build a demonstration state machine using the same patterns and primitives used by the compiler.  Hopefully it will be a lot more legible than the real thing.  Armed with this knowledge, you should be able to pick apart and understand how the real code works. 

## Our Async/Await Code

This is the high level code we'll transform.

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

## The State Machine

### The Skeleton

The skeleton looks like this.

```csharp
private class AsyncStateMachine :IAsyncStateMachine
{
    public AsyncTaskMethodBuilder Builder;
    public MyClass Parent = new();
    public int State = -2;

   public void SetStateMachine(IAsyncStateMachine stateMachine) {}

    public void MoveNext() {}
}
```

`AsyncTaskMethodBuilder` provides a lot of the boiler plate functionality.  It maintains the `Task`  representing the running state of the state machine.

The class implements the `IAsyncStateMachine` interface: it's a requirement of `AsyncTaskMethodBuilder`.

State is tracked using a simple integer.  
 - `-1` is the intitial state.  
 - `-2` is completed.  

`SetStateMachine` is required by the interface but bot used.  It's implemented as an empty method.

`MoveNext` is the method called to start and run the state machine.  We'll look at it's implementation next.

### States 

The compiler breaks the original method into steps, split on `await`.  Each step is given a state number.  The basic template looks like this.

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
The last step executes the *await* instruction, gets the returned *awaiter* and tests for completion.

If:

 - The *awaiter* is not completed, the background process yielded and is still running on a background thread.  We need to wait for it to complete before moving on the next step.  We do that by adding a continuation to the awaiter to call this method when it completes.  We, this execution thread, are done. We return to the caller.

 - The *awaiter* is completed.  The background process ran to completion. We can safely get the result. There's no requirment for a continuation: move on synchronously to the next step.      

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


### The Original Method

The calling method gets refactored into:

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

It creates a new instance of the state machine and sets the Builder instance on the state machine.  It sets the state and starts the builder which makes the first call to `MoveNext`.

The method returns the Builder's Task when the state machine returns, either because a state yielded, or the state machine completed.

### The Continuation Context

The context in which continuations run is the responsibility of the background process.  The state machine posts the continuation to the awaiter, but there's no built in mechanism within the *awaiter* pattern to provide direction.

Task based *awaiters* add extra information through `ConfigureAwait`.  Most async processes honour the information provided.

Background processes normally capture the *synchronisation context* when they start.  They run the continuation on the context or the threadpool depending on the configuration of the `ConfiguredTaskAwaiter` *awaiter* returned by `ContinueWith`.

> This topic is covered in more detail in the [Awaitable document](./Awaitable.md).

## The Compiler Generated State Machine

Note:

1. The code is optimized for a sequential synchronous operation.  No yields.
2. The code is not generated for humans consumption.
3. The compiler uses characters illegal in human generated code to ensure no naming conflicts.
4. The compiler switches from an `if` driven `MoveNext` to a `switch` driven version when there are more than 2 steps.
5. `MoveNext` looks back-to-front because it's generated from the end backwards.
6. A state machine is compiled as a class in debug mode and a struct in release mode.
7. The code looks clucky and breaks best practices.  It's generated by a builder to a formula.  It uses builders to provide a lot of boilerplate functionality.  It's optimized for speed not elegance.

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
