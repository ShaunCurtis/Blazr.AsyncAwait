# Asynchrony

To understand how C# delivers asynchrony you need to understand some core concepts.

1. Synchronous blocks of code are executed on threads.

2. A thread blocks when it waits for something to happen.

3. A thread can only do one thing at a time.  It can't wait on a timer to see if it's finished and excute other code at the same time.

4. A block of code yields control back to the caller only when it completes.  Not before.  The thread that code was running on is free for the next job.

If you've understood these points so far, you'll have a logical question.  

> If I await say a database call to get a list, how does the code after the await get executed?

When a block of code yields control back to the caller, it bundles the code after the await as a new block of code [called the continuation] and hands it, and responsibility for running it, to the background task.

The next logical question is:  

> How does it rebundle the code in runtime?

It doesn't.  The compiled version of your code is very different.  We'll look at this in a while.




