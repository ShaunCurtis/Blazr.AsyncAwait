# Timers

Timers are one of those *Hazy* topics.  Most programmers know how to use them and the various implementations.  But. how do they actually work?  

## One Timer to Rule them All

There's a class called `TimerQueue`.  It implements the *Singleton* pattern : one instance exists in the AppDomain and manages all the application timers. 

There are two sorts of timers:

1. Operational Timeouts.  These are system timers created and destroyed frequently and only fire if somnethig goes wrong.

2. Scheduled Background Tasks.  These are designed to fire.  They include all the timers we create and consume in pour code.

`TimerQueue` maintains a list of active timers.

### The Timer Loop

`TimerQueue` uses a single native timer provided by the Virtual Machine.

The basic operation can be summarised as the following pseudo-code:

```csharp
void Loop()
{
    FireAllExpiredTimers();
    var nextTimeSpan = UnexpiredTimers.ShortestTimeSpanToExpiration();
    AppDomainTimerSafeHandle.Callback(Loop, nextTimeSpan);
}
```
 

```text
//
    // TimerQueue maintains a list of active timers in this AppDomain.  We use a single native timer, supplied by the VM,
    // to schedule all managed timers in the AppDomain.
    //
    // Perf assumptions:  We assume that timers are created and destroyed frequently, but rarely actually fire.
    // There are roughly two types of timer:
    //
    //  - timeouts for operations.  These are created and destroyed very frequently, but almost never fire, because
    //    the whole point is that the timer only fires if something has gone wrong.
    //
    //  - scheduled background tasks.  These typically do fire, but they usually have quite long durations.
    //    So the impact of spending a few extra cycles to fire these is negligible.
    //
    // Because of this, we want to choose a data structure with very fast insert and delete times, but we can live
    // with linear traversal times when firing timers.
    //
    // The data structure we've chosen is an unordered doubly-linked list of active timers.  This gives O(1) insertion
    // and removal, and O(N) traversal when finding expired timers.
    //
    // Note that all instance methods of this class require that the caller hold a lock on TimerQueue.Instance.
    //
```