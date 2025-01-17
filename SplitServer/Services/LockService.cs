using System.Collections.Concurrent;

namespace SplitServer.Services;

public class LockService
{
    private readonly ConcurrentDictionary<string, bool> _locks = new();

    public IDisposable AcquireLock(string key)
    {
        if (!_locks.TryAdd(key, true))
        {
            throw new ResourceLockedException();
        }

        return new Lock(() => _locks.TryRemove(key, out _));
    }

    private class Lock : IDisposable
    {
        private readonly Action _releaseAction;

        public Lock(Action releaseAction)
        {
            _releaseAction = releaseAction;
        }

        public void Dispose()
        {
            _releaseAction?.Invoke();
        }
    }
}