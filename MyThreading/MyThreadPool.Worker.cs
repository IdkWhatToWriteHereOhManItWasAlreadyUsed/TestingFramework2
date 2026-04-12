using System;
using System.Threading;

namespace MyThreading
{
    public partial class MyThreadPool
    {
        private class Worker
        {
            private static int _idCounter;
            public int Id { get; } = Interlocked.Increment(ref _idCounter);
            public Thread Thread { get; private set; }
            private readonly MyThreadPool _pool;
            private volatile bool _shouldExit;

            public Worker(MyThreadPool pool)
            {
                _pool = pool;
                Thread = new Thread(Execute);
            }

            public void RequestExit()
            {
                _shouldExit = true;
                lock (_pool._queueLock) Monitor.Pulse(_pool._queueLock);
            }

            private void Execute()
            {
                try
                {
                    while (!_shouldExit && _pool._isRunning)
                    {
                        Action? task = null;

                        lock (_pool._queueLock)
                        {
                            while (_pool._taskQueue.Count == 0 && !_shouldExit && _pool._isRunning)
                            {
                                try
                                {
                                    bool signaled = Monitor.Wait(_pool._queueLock, (int)_pool._idleTimeout.TotalMilliseconds);
                                    if (!signaled && _pool._taskQueue.Count == 0)
                                    {
                                        int current = Interlocked.Decrement(ref _pool._activeWorkers);
                                        if (current >= _pool._minThreads)
                                        {
                                            lock (_pool._workersLock) _pool._workers.Remove(this);
                                            _pool.OnWorkerTerminated(new ThreadPoolEventArgs("WorkerTerminated", Id, $"Поток #{Id} остановлен"));
                                            return;
                                        }
                                        else
                                        {
                                            Interlocked.Increment(ref _pool._activeWorkers);
                                            continue;
                                        }
                                    }
                                }
                                catch (ThreadInterruptedException)
                                {
                                    _pool.OnWorkerTerminated(new ThreadPoolEventArgs("WorkerTerminated", Id, $"Поток #{Id} прерван во время ожидания"));
                                    return;
                                }
                            }

                            if (_pool._taskQueue.Count > 0)
                            {
                                task = _pool._taskQueue.Dequeue();
                                Interlocked.Decrement(ref _pool._pendingTasks);
                            }
                        }

                        if (task != null)
                        {
                            _pool.OnTaskStarted(new ThreadPoolEventArgs("TaskStarted", Id));

                            try
                            {
                                task();
                                _pool.OnTaskCompleted(new ThreadPoolEventArgs("TaskCompleted", Id));
                            }
                            catch (Exception ex)
                            {
                                _pool.OnTaskFailed(new ThreadPoolEventArgs("TaskFailed", Id, ex.Message, ex));
                            }
                        }
                    }
                }
                catch (ThreadInterruptedException)
                {
                    _pool.OnWorkerTerminated(new ThreadPoolEventArgs("WorkerTerminated", Id, $"Поток #{Id} прерван"));
                }
                catch (Exception ex)
                {
                    _pool.OnWorkerCrashed(new ThreadPoolEventArgs("WorkerCrashed", Id, ex.Message, ex));
                }
                finally
                {
                    lock (_pool._workersLock)
                    {
                        if (_pool._workers.Contains(this))
                            _pool._workers.Remove(this);
                    }
                    _pool.OnWorkerTerminated(new ThreadPoolEventArgs("WorkerTerminated", Id, $"Поток #{Id} завершен"));
                }
            }
        }
    }
}