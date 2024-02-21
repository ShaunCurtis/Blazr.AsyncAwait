# Timers

Timers are one of those *fuzzy* topics.  Most programmers know how to use them and the various implementations.  But, how do they actually work?  

## One Timer to Rule them All

Behind the scenes there's only one running timer.

`TimerQueue` implements the *Singleton* pattern : one instance per AppDomain.  It manages all the application timers and schedules the callbacks when timers expire.

When you create a timer, you queue a timer object to `TimerQueue`. 

Timers are created for two purposes:

1. Operational Timeouts.  These are system timers created and destroyed frequently. They only fire if something goes wrong.

2. Scheduled Background Tasks.  These are designed to fire.  They include all the timers we create and consume in our code.

`TimerQueue` runs on it's own thread.  The singleton creator method does this:

```csharp
new Thread(TimerQueue.Create().Run());
```

### The Timer Loop

`TimerQueue` uses a single native timer provided by the underlying operating system, normally provided by the Virtual Machine.

The basic operation can be summarised as:

```csharp
void Resume()
{
    FireAllExpiredTimers();
    nextTimeSpan = UnexpiredTimers.GetShortestTimeSpanToExpiration();
    NativeTimer.ScheduleCallback(Resume, nextTimeSpan);
}
```

The native timer triggers `Resume`.  `Resume`:

1. Enumerates the currently registered timers and fires the callbacks on any that have expired.  It fires the first on the current thread and any subsequent ones on a threadpool thread.  
1. Gets the shortest timespan for all the unexpired timers.

Once complete it schedules a callback from the Native timer for the minumim period.  Note that the resolution of `nextTimeSpan` will be based on the resolution of the system clock: approx 15 ms on a Windows Server.   

Adding a new timer looks like this:

```csharp
void AddTimer(Timer timer)
{
   AddTimerToList(timer);
   ResheduleNativeTimerIfRequired(Resume, nextTimeSpan);
}
```

This adds `timer` to the queue and reschedules the native timer callback if the new timer's timespan is shorter than the current scheduled callback.

## Takeaways

1. When you "run" a timer there's no black magic.  You add a new timer to the queue and carry on to the next task: fire and forget.  There's nothing happening "in the background" on your execution thread.  The tracking, management and callbacks are managed by `TimerQueue` running on its own thread.

1. You should `Dispose` a timer to remove it from the queue.

1. The callback (or event in a System.Timers.Timer object) runs in a threadpool context, not the context of the owning object.  There's automatic detection and switching to the synchronisation context.  You must manually switch to run any UI based code.



