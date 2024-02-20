using Blazr.Async;
using Blazr.SyncronisationContext;
using System.Diagnostics;
using System.Runtime.CompilerServices;

var sc = new BlazrSynchronisationContext();
SynchronizationContext.SetSynchronizationContext(sc);

Utilities.WriteToConsole("Application started");

sc.Start();
//ThreadPool.QueueUserWorkItem(async (state) =>
//{
//    //SynchronizationContext.SetSynchronizationContext(sc);
//    await Utilities.DoWorkThreadpoolAsync(null);
//});

//var awaitable = MyAwaitable.Idle(2000);
//await awaitable;

Console.ReadLine();

await new MyClass().Run();

class MyClass
{
    public Task Run()
    {
        var stateMachine = new AsyncStateMachine(this);
        stateMachine.Builder = new AsyncTaskMethodBuilder();
        stateMachine.State = -1;
        stateMachine.Builder.Start(ref stateMachine);
        stateMachine.MoveNext();
        return stateMachine.Task;
    }

    class AsyncStateMachine :IAsyncStateMachine
    {
        public AsyncTaskMethodBuilder Builder;
        private readonly TaskCompletionSource _tcs = new();
        public MyClass Parent;
        public int State = -2;

        public Task Task => _tcs.Task;

        public AsyncStateMachine(MyClass program)
            => Parent = program;

        public void SetStateMachine(IAsyncStateMachine stateMachine) { }

        public void MoveNext()
        {
            switch (State)
            {
                default:
                    Console.WriteLine("Step 1");
                    var awaiter = Task.Delay(500).GetAwaiter();

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
                    var awaiter0 = Task.Delay(600).GetAwaiter();

                    if (awaiter0.IsCompleted == false)
                    {
                        this.State = 1;
                        var stateMachine = this;
                        this.Builder.AwaitUnsafeOnCompleted(ref awaiter0, ref stateMachine);
                        return;
                    }

                    goto case 1;

                case 1:
                    Console.WriteLine("Step 3");
                    var awaiter1 = Task.Delay(700).GetAwaiter();

                    if (awaiter1.IsCompleted == false)
                    {
                        this.State = 2;
                        var stateMachine = this;
                        this.Builder.AwaitUnsafeOnCompleted(ref awaiter1, ref stateMachine);
                        return;
                    }

                    goto case 2;

                case 2:
                    Console.WriteLine("Final Step");
                    this.State = -2;

                    break;
            };

            this.Builder.SetResult();
            return;
        }
    }
}