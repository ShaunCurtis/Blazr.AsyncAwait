# Synchronisation Context

A little history helps explain why the Synchronisation Context [SC from now on] exists.

Applications originally ran on one thread.  Work blocked the thread while they executed.  The UI was unresponsive.  

When multithreading came along you could shift work to other threads and keep the Ui responsive.  This new functionality came at a price.  Three important issues are:

1. You needed a framework to manage the threading environment.   
2. Objects passed around as part of work needed to be thread safe.
3. You need a way to manage UI activity.

Two examples to illustrate these issues:

You have a `StringBuilder` instance that you're using as a log for some work.  If you schedule that work to run on different threads, you may have two threads calling `stringBuilder.WriteLine` at the same time.  `StringBuilder` isn't thread safe, so you will be end up with mixed up text in `StringBuilder`.   

Two blocks of work on the UI attempt to update the UI at the same time.  Will the UI handle this gracefully?  Often not.

To solve this problem, various frameworks (such as Windows Forms) provided thread management.  The problem was that different frameworks did it differently, so writing generic libraries for the different was problematic. 

The **Synchronisation Context** was created to abstract the developer from the underlying implementation.  Each framework has it's own implementation of `SynchronisationContext`, but they all implement the base `SynchronisationContext` functionality.  And there's a common way to get the current `SynchronisationContext`.

So what is a **Synchronisation Context**?

The two primary statics are:

```csharp
//Gets the synchronization context for the current thread.
public static System.Threading.SynchronizationContext? Current { get; }

//Sets the current synchronization context for the current thread.
public static void SetSynchronizationContext (System.Threading.SynchronizationContext? syncContext);
```

And Instance methods

```csharp
//Dispatches an asynchronous message to a synchronization context.
Post(SendOrPostCallback, Object)	

//Dispatches a synchronous message to a synchronization context.
Send(SendOrPostCallback, Object)	
```
