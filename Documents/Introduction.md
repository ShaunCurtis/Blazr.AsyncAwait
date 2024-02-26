# The Async Series - An Introduction

Asynchronous behaviour is a difficult concept to grasp.  I'll try.

Let's look at a practical example most of us have come across.

We receive audio-video streamed as a composite signal.  A receiver decodes and splits the signal into audio and video streams.  These two streams are processed separately to produce the moving images we see on the screen and the associated sound track.  

If these two processes run independantly, they quickly get out of sequence: it's extremely unlikely they run at the same speed.  We see lag between the lip movement and the sound coming from the speakers.

We have asynchronous behaviour: two or more related synchronous processes running in parallel.  And the need for synchronisation: a mechanism to keep the two streams in sync.  The lips moving on the screen and the sound coming from the speakers at the same time.

## The Task Parallel Library

Welcome to the `Task Parallel Library` - The DotNet core code library to program asynchronous behaviour in our applications.

> There's a [Microsoft article on making a cold serial breakfast](https://learn.microsoft.com/en-us/dotnet/csharp/asynchronous-programming/) or hot asynchronous breakfast that makes interesting reading.  

The library provides a set of primitives and high level functionality for build applications.  It has matured over time.  Today *Async/Await* is the ubiquitous *go to* for writing asynchronous code.

Unless you write backend low level libraries, you'll have little cause to use anything else.  That said, you shouldn't neglect the need to understand at a conceptual level how asynchronous behaviour is implemented.  There are times when you will get it wrong.  Without the knowledge, you're fishing in the dark or resorting to asking a question on Stack Overflow.

This series of articles provides an in depth look at many of the key building blocks that make *Async/Await* such a great tool that we rarely have cause to question what's going on below the surface.

The articles are *Blazor* centric and cover some of the async quirks that Blazor throws up.

## Rules of Physics

Before we begin, it's worth emphasising a few *rules of physics*.

1. Code [methods or anonymous methods] executes on threads in synchronous blocks.
2. A thread only does one thing at once.  It can't monitor the state of a database call and run code updating the UI.  That's two processes each running on their own thread.
3. There's no black magic.  Everything has a logical explanation.
4. Queues are serviced by message loops running on their own dedicated threads.

## Compilation

A significant barrier to understanding *Async/Await* is the difference between what you see and what you get.

The compiler refactors *Async/Await* methods you write into almost unrecognisable low level code.  You need to understand this low level code to understand *async/await*.

To demonstrate, go to [C# Lab](https://sharplab.io/) and paste the following code into the Code pane:

```csharp
using System;
using System.Threading.Tasks;
public class C {
    public async Task M() {
        await Task.Delay(100);
    }
}
```

 Look at the Result pane (set to C#).  I haven't shown the output here. It's pretty long and, at first sight, unintelligible.  
 
 After reading these documents, you will understand what that code does. 