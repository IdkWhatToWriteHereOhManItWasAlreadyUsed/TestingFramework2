using System;
using System.Threading;

namespace MyThreading
{
    public partial class MyThreadPool
    {
        private void ScalerLoop()
        {
            while (_isRunning)
            {
                Thread.Sleep(_scaleCheckInterval);

                int queueSize, activeCount;
                lock (_queueLock)
                {
                    queueSize = _taskQueue.Count;
                }
                lock (_workersLock)
                {
                    activeCount = _activeWorkers;
                }

                lock (_workersLock)
                {
                    for (int i = _workers.Count - 1; i >= 0; i--)
                    {
                        var w = _workers[i];
                        if (w.Thread.IsAlive) continue;

                        Log($"Поток {w.Id} аварийно завершился. Восстановление...");
                        _workers.RemoveAt(i);
                        Interlocked.Decrement(ref _activeWorkers);

                        if (_isRunning && _activeWorkers < _minThreads)
                        {
                            CreateWorker();
                            OnScalingDown(new ThreadPoolEventArgs("ScalingDown", -1, $"Восстановление после аварийного завершения"));
                        }
                    }
                }

                if (queueSize > 0 || activeCount != _minThreads)
                    Log(GetStatus());
            }
        }
    }
}