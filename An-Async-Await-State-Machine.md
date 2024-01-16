# An Async Await State Machine

The *Async Await State Machine* is a fundimental building block of the *Task Parallel Library*.

When you write this high level code:

```csharp
public async Task Clicked()
{
    Console.WriteLine($"State Machine Processing completed at {DateTime.Now.ToLongTimeString()}");
    var task1 = Task.Delay(3000);
    Console.WriteLine($"State Machine Processing completed at {DateTime.Now.ToLongTimeString()}");
}
```

It gets morphed into:

1. A state machine object within the container class.
1. A refactored `Clicked`.

A simplistic task machine based on the above code looks like this.  I've added comments to help understand it.

```csharp
public class MyComponent
{
    private class Clicked_StateMachine
    {
        private readonly Demo _parent;

        private readonly TaskCompletionSource _taskManager = new();
        private int _state = 0;

        private Task _state0_Task = Task.CompletedTask;
        //.. Task for each state

        public Task Task => _taskManager.Task;

        public Clicked_StateMachine(Demo parent)
        {
            _parent = parent;
        }

        public void Execute()
        {
            try
            {
                // Initial State - run on first pass
                if (_state == 0)
                {
                    // first sync code block
                    Console.WriteLine($"State Machine Processing completed at {DateTime.Now.ToLongTimeString()}");

                    // First await Task.  We just start it.
                    var task1 = Task.Delay(3000);

                    // Sets the state to the next step
                    _state = 1;

                    // If the task yielded then the awaiter is running on a difffernt thread
                    // We add a continuation to call this Execute method when it completes
                    // and complete.  Our work is done.
                    if (!_state0_Task.IsCompleted)
                    {
                        _state0_Task.ContinueWith(_ => Execute());
                        return;
                    }

                    // If the task was sync with no yield i.e. it ran to completion, we drop out to the next state and continue execution
                }

                // We reach this point either because we fell thro from the previous task because it executed synchronously
                // Or the task in the previous state completed and the continuation ran and called us.
                if (_state == 1)
                {
                    // The second Sync Block of code
                    Console.WriteLine($"State Machine Processing completed at {DateTime.Now.ToLongTimeString()}");
                }

                // As many states as there are awaits in the original code block

                // The last state block doesn't run any async code so falls out the bottom

                // Set the state machine's Task to complete
                _taskManager.SetResult();
                // and finish

            }
            // Something went wrong.  Pass the error to the caller through the completion task
            catch (Exception e)
            {
                _taskManager.SetException(e);
            }
        }
    }
}
```

`Clicked` has been carved up into two code blocks, separated by the await.  Each is a state. If the code had contained two awaits there would be three state code blocks.

Here's the refactored `Clicked`.

```csharp
private Task Clicked()
{
    var stateMachine = new Clicked_StateMachine(this);
    stateMachine.Execute();
    return stateMachine.Task;
}
```

The first call to `Execute` will run the code to the first yield - in this case the Task.Delay(3000).  The returned task is not complete, so `Execute` adds a continuation to call itself, and returns.

`Clicked` returns the the state machine task (which is not complete) and completes.

At this point we have an *awaitable* object waiting on a separate thread for `Task.Delay` to complete.  It holds a reference to `Execute` in the `Clicked_StateMachine` instance created in the `Home` page.  When Task.Delay completes, it sets the Task to complete and posts the continuation to the synchronisation context.

`Execute` runs with a state of `1` on the synchronisation context.  It executes the final code block, sets the state machine task to complete and exits.  Job done.

### Take Aways

There are no `async` or `await` statements in the morphed code.  There's just code running on threads (and blocking them), and getting posted to the synchronisation context to be run sequentially.