using Blazr.Async;
using System.Runtime.CompilerServices;

AsyncUtilities.WriteToConsole("Application started");

Console.ReadLine();

await new MyClass().Run();

class MyClass
{
    string message = "Idle";

    public Task Run()
    {
        var stateMachine = new AsyncStateMachine();
        stateMachine.Builder = AsyncTaskMethodBuilder.Create();
        stateMachine.Parent = this;
        stateMachine.State = -1;
        stateMachine.Builder.Start(ref stateMachine);
        return stateMachine.Builder.Task;
    }

    class AsyncStateMachine : IAsyncStateMachine
    {
        public AsyncTaskMethodBuilder Builder;
        public MyClass Parent = new();
        public int State = -2;

        public void SetStateMachine(IAsyncStateMachine stateMachine) { }

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
                    var _awaiter = Task.Delay(600).ConfigureAwait(ConfigureAwaitOptions.None).GetAwaiter();

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
    }
}