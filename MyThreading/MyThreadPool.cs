using System;
using System.Collections.Generic;
using System.Threading;

namespace MyThreading
{
    public class MyThreadPool
    {
        private readonly int _minThreads;
        private readonly int _maxThreads;
        private readonly TimeSpan _idleTimeout;
        private readonly int _queueScaleThreshold;
        private readonly TimeSpan _scaleCheckInterval;

        private readonly Queue<Action> _taskQueue = new Queue<Action>();
        private readonly object _queueLock = new object();

        private readonly List<Worker> _workers = new List<Worker>();
        private readonly Lock _workersLock = new Lock();

        private volatile bool _isRunning;
        private volatile int _activeWorkers;
        private volatile int _pendingTasks;
        private Thread _scalerThread;

        public Action<string> Log { get; set; } = msg => Console.WriteLine($"[Pool] {DateTime.Now:HH:mm:ss.fff} {msg}");

        public MyThreadPool(int minThreads, int maxThreads, TimeSpan idleTimeout, int queueScaleThreshold = 3, TimeSpan? scaleCheckInterval = null)
        {
            _minThreads = Math.Max(1, minThreads);
            _maxThreads = Math.Max(minThreads, maxThreads);
            _idleTimeout = idleTimeout;
            _queueScaleThreshold = queueScaleThreshold;
            _scaleCheckInterval = scaleCheckInterval ?? TimeSpan.FromMilliseconds(500);
           
        }

        public void Start()
        {
            if (_isRunning) return;
            _isRunning = true;

            for (int i = 0; i < _minThreads; i++)
                CreateWorker();

            _scalerThread = new Thread(ScalerLoop) { IsBackground = true, Name = "PoolScaler" };
            _scalerThread.Start();

            Log($"Пул запущен. Min: {_minThreads}, Max: {_maxThreads}, IdleTimeout: {_idleTimeout.TotalSeconds}s");
        }

        public void Enqueue(Action task)
        {
            if (!_isRunning) throw new InvalidOperationException("Пул не запущен.");
            if (task == null) throw new ArgumentNullException(nameof(task));

            lock (_queueLock)
            {
                _taskQueue.Enqueue(task);
                _pendingTasks++;
                Monitor.Pulse(_queueLock);
            }

            int current = _activeWorkers;
            lock (_queueLock)
            {
                if (_taskQueue.Count >= _queueScaleThreshold && current < _maxThreads)
                {
                    CreateWorker();
                    Log($"Масштабирование ВВЕРХ: создано поток. Активно: {_activeWorkers}, В очереди: {_taskQueue.Count}");
                }
            }
        }

        public void Stop()
        {
            if (!_isRunning) return;
            _isRunning = false;

            lock (_queueLock)
            {
                _taskQueue.Clear();
                _pendingTasks = 0;
                Monitor.PulseAll(_queueLock);
            }

            lock (_workersLock)
            {
                foreach (var w in _workers)
                    w.RequestExit();
            }

            _scalerThread?.Join(2000);
            Log("Пул остановлен.");
        }

        public string GetStatus()
        {
            int queue, active, pending;
            lock (_queueLock) { queue = _taskQueue.Count; pending = _pendingTasks; }
            lock (_workersLock) active = _activeWorkers;
            return $"[Статус] Активных потоков: {active}/{_maxThreads} | В очереди: {queue} | Ожидают: {pending}";
        }

        private void CreateWorker()
        {
            var worker = new Worker(this);
            lock (_workersLock)
            {
                _workers.Add(worker);
                _activeWorkers++;
            }
            worker.Thread.IsBackground = true;
            worker.Thread.Name = $"Worker-{worker.Id}";
            worker.Thread.Start();
        }

        private void ScalerLoop()
        {
            while (_isRunning)
            {
                Thread.Sleep(_scaleCheckInterval);

                int queueSize, activeCount;
                lock (_queueLock) queueSize = _taskQueue.Count;
                lock (_workersLock) activeCount = _activeWorkers;

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
                            CreateWorker();
                    }
                }

                if (queueSize > 0 || activeCount != _minThreads)
                    Log(GetStatus());
            }
        }

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
                                bool signaled = Monitor.Wait(_pool._queueLock, (int)_pool._idleTimeout.TotalMilliseconds);
                                if (!signaled && _pool._taskQueue.Count == 0)
                                {
                                    int current = Interlocked.Decrement(ref _pool._activeWorkers);
                                    if (current >= _pool._minThreads)
                                    {
                                        _pool.Log($"Поток {Id} завершил работу по таймауту простоя. Осталось: {current}");
                                        lock (_pool._workersLock) _pool._workers.Remove(this);
                                        return;
                                    }
                                    else
                                    {
                                        Interlocked.Increment(ref _pool._activeWorkers);
                                        continue;
                                    }
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
                            try
                            {
                                task();
                            }
                            catch (Exception ex)
                            {
                                _pool.Log($"Ошибка в задаче (Поток {Id}): {ex.Message}");
                            }
                        }
                    }
                }
                catch (ThreadAbortException) 
                { 
                }
                catch (Exception ex)
                {
                    _pool.Log($"Критическая ошибка потока {Id}: {ex.Message}");
                }
            }
        }
    }
}