using System;
using System.Collections.Generic;
using System.Threading;

namespace MyThreading
{
    public partial class MyThreadPool
    {
        private readonly int _minThreads;
        private readonly int _maxThreads;
        private readonly TimeSpan _idleTimeout;
        private readonly int _queueScaleThreshold;
        private readonly TimeSpan _scaleCheckInterval;

        private readonly Queue<Action> _taskQueue = new();
        private readonly object _queueLock = new();

        private readonly List<Worker> _workers = [];
        private readonly Lock _workersLock = new();

        private volatile bool _isRunning;
        private volatile int _activeWorkers;
        private volatile int _pendingTasks;
        private Thread _scalerThread;

        public Action<string> Log { get; set; } = msg => Console.WriteLine();

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

            OnPoolStarted(new ThreadPoolEventArgs("PoolStarted"));
        }

        public void Enqueue(Action task)
        {
            if (!_isRunning) throw new InvalidOperationException("Пул не запущен.");
            ArgumentNullException.ThrowIfNull(task);

            lock (_queueLock)
            {
                _taskQueue.Enqueue(task);
                _pendingTasks++;
                Monitor.Pulse(_queueLock);
            }

            OnTaskEnqueued(new ThreadPoolEventArgs("TaskEnqueued", -1, $"Задача добавлена. Очередь: {_taskQueue.Count}"));

            int current = _activeWorkers;
            lock (_queueLock)
            {
                if (_taskQueue.Count >= _queueScaleThreshold && current < _maxThreads)
                {
                    CreateWorker();
                    OnScalingUp(new ThreadPoolEventArgs("ScalingUp", -1, $"Масштабирование: {_activeWorkers}/{_maxThreads}"));
                }
            }
        }

        public void Stop(bool waitForWorkers = true, int timeoutMs = 5000)
        {
            if (!_isRunning) return;

            OnPoolStopping(new ThreadPoolEventArgs("PoolStopping"));
            _isRunning = false;

            lock (_queueLock)
            {
                _taskQueue.Clear();
                _pendingTasks = 0;
                Monitor.PulseAll(_queueLock);
            }

            List<Worker> workersCopy;
            lock (_workersLock)
            {
                workersCopy = _workers.ToList();
                foreach (var w in workersCopy)
                    w.RequestExit();
            }

            lock (_queueLock)
            {
                Monitor.PulseAll(_queueLock);
            }

            if (waitForWorkers)
            {
                DateTime deadline = DateTime.Now.AddMilliseconds(timeoutMs);

                foreach (var worker in workersCopy)
                {
                    if (!worker.Thread.IsAlive) continue;

                    int remaining = (int)(deadline - DateTime.Now).TotalMilliseconds;
                    if (remaining > 0)
                    {
                        if (!worker.Thread.Join(remaining))
                        {
                            if (worker.Thread.IsAlive)
                                worker.Thread.Interrupt();
                            worker.Thread.Join(100);
                        }
                    }
                    else if (worker.Thread.IsAlive)
                    {
                        worker.Thread.Interrupt();
                        worker.Thread.Join(100);
                    }
                }
            }

            _scalerThread?.Join(2000);
            _scalerThread?.Interrupt();

            lock (_workersLock)
            {
                _workers.Clear();
                _activeWorkers = 0;
            }

            OnPoolStopped(new ThreadPoolEventArgs("PoolStopped"));
        }

        public void StopAndWait(int timeoutMs = 5000)
        {
            Stop(true, timeoutMs);
        }

        public void StopImmediately()
        {
            Stop(false, 0);
        }

        public bool AreAllWorkersCompleted()
        {
            lock (_workersLock)
            {
                return _workers.All(w => !w.Thread.IsAlive);
            }
        }

        public void WaitForAllTasks(int timeoutMs = -1)
        {
            DateTime deadline = timeoutMs > 0 ? DateTime.Now.AddMilliseconds(timeoutMs) : DateTime.MaxValue;

            while (_pendingTasks > 0 || _taskQueue.Count > 0)
            {
                if (timeoutMs > 0 && DateTime.Now > deadline)
                    throw new TimeoutException($"Ожидание задач превысило {timeoutMs} мс");

                Thread.Sleep(50);
            }
        }

        public string GetStatus()
        {
            int queue, active, pending;
            lock (_queueLock) { queue = _taskQueue.Count; pending = _pendingTasks; }
            lock (_workersLock) active = _activeWorkers;
            return $"Активных потоков: {active}/{_maxThreads} | В очереди: {queue} | Ожидают: {pending}";
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

            OnWorkerCreated(new ThreadPoolEventArgs("WorkerCreated", worker.Id, $"Создан Worker #{worker.Id}"));
        }
    }
}