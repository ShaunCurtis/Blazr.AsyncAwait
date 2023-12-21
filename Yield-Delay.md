# What is Task.Yield?

It's high level code.  It abstracts the programmer from the nitty gritty of the *Task Processing Library*.

Let's look at a simple Blazor example: a button click handler.

Here's a simple home page:

```csharp
@page "/"

<PageTitle>Home</PageTitle>

<h1>Hello, world!</h1>

Welcome to your new app.

<div class="mb-3">
    <button class="btn btn-primary" @onclick="Clicked">Click</button>
</div>

<div class="bg-dark text-white m-2 p-2">
    @_message
</div>

@code {
    private string? _message;

    private async Task Clicked()
    {
        _message = $"Processing at {DateTime.Now.ToLongTimeString()}";
        await Task.Yield();
        _message = $"Completed Processing at {DateTime.Now.ToLongTimeString()}";
    }
}
```

Run this and you will see the UI stay responsive, the first message flashes before the second message displays.  There's no delay, but the `Task.Yield()` did yield control to the UI for the intermediate render.

Those three lines in `Clicked` aren't what actually gets compiled into runtime code.


```csharp
private Task Clicked()
{
    var tcs = new TaskCompletionSource<object>();

    try
    {
        // execute the before code
        _message = $"Processing at {DateTime.Now.ToLongTimeString()}";

        // create a task with nothing to do and start it
        var yieldingTask = new Task(() => { });
        yieldingTask.Start();

        yieldingTask.ContinueWith(await =>
            {
                try
                {
                    // the coninuation code
                    _message = $"Completed Processing at {DateTime.Now.ToLongTimeString()}";
                    // finally set the TaskCompletionSource as complete
                    tcs.SetResult(null);
                }
                catch (Exception exception)
                {
                    tcs.SetException(exception);
                }
            });
    }
    catch (Exception exception)
    {
        tcs.SetException(exception);
    }

    return taskCompletionSource.Task;
}
```
