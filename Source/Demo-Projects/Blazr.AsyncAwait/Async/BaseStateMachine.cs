using Microsoft.AspNetCore.Components;

namespace Blazr.AsyncAwait;

public class MyComponent : ComponentBase
{

    private Task Clicked()
    {
        var stateMachine = new BaseStateMachine(this);
        stateMachine.MoveNext();
        return stateMachine.Task;
    }

    private class BaseStateMachine
    {
        private readonly MyComponent _parent;

        private readonly TaskCompletionSource _taskManager = new();
        private int _state = 0;

        public Task Task => _taskManager.Task;

        public BaseStateMachine(MyComponent parent)
        {
            _parent = parent;
        }

        public void MoveNext()
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
                    if (!task1.IsCompleted)
                    {
                        task1.ContinueWith(_ => MoveNext());
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