using System.Runtime.CompilerServices;

namespace Blazr.Async;

public static class Awaiters
{
    public static TaskAwaiter GetAwaiter(this TimeSpan timeSpan)
    {
        return Task.Delay(timeSpan).GetAwaiter();
    }

    public static TaskAwaiter GetAwaiter(this int millsecondsToElapse)
    {
        return TimeSpan.FromMilliseconds(millsecondsToElapse).GetAwaiter();
    }
}
