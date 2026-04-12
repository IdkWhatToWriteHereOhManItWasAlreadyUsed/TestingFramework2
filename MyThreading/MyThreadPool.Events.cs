using System;

namespace MyThreading
{
    public partial class MyThreadPool
    {
        public event EventHandler<ThreadPoolEventArgs>? PoolStarted;
        public event EventHandler<ThreadPoolEventArgs>? PoolStopping;
        public event EventHandler<ThreadPoolEventArgs>? PoolStopped;

        public event EventHandler<ThreadPoolEventArgs>? WorkerCreated;
        public event EventHandler<ThreadPoolEventArgs>? WorkerIdleTimeout;
        public event EventHandler<ThreadPoolEventArgs>? WorkerTerminated;
        public event EventHandler<ThreadPoolEventArgs>? WorkerCrashed;

        public event EventHandler<ThreadPoolEventArgs>? TaskEnqueued;
        public event EventHandler<ThreadPoolEventArgs>? TaskStarted;
        public event EventHandler<ThreadPoolEventArgs>? TaskCompleted;
        public event EventHandler<ThreadPoolEventArgs>? TaskFailed;

        public event EventHandler<ThreadPoolEventArgs>? ScalingUp;
        public event EventHandler<ThreadPoolEventArgs>? ScalingDown;


        protected virtual void OnPoolStarted(ThreadPoolEventArgs e)
        {
            UpdateEventArgsStats(e);
            PoolStarted?.Invoke(this, e);
        }

        protected virtual void OnPoolStopping(ThreadPoolEventArgs e)
        {
            UpdateEventArgsStats(e);
            PoolStopping?.Invoke(this, e);
        }

        protected virtual void OnPoolStopped(ThreadPoolEventArgs e)
        {
            UpdateEventArgsStats(e);
            PoolStopped?.Invoke(this, e);
        }

        protected virtual void OnWorkerCreated(ThreadPoolEventArgs e)
        {
            UpdateEventArgsStats(e);
            WorkerCreated?.Invoke(this, e);
        }

        protected virtual void OnWorkerIdleTimeout(ThreadPoolEventArgs e)
        {
            UpdateEventArgsStats(e);
            WorkerIdleTimeout?.Invoke(this, e);
        }

        protected virtual void OnWorkerTerminated(ThreadPoolEventArgs e)
        {
            UpdateEventArgsStats(e);
            WorkerTerminated?.Invoke(this, e);
        }

        protected virtual void OnWorkerCrashed(ThreadPoolEventArgs e)
        {
            UpdateEventArgsStats(e);
            WorkerCrashed?.Invoke(this, e);
        }

        protected virtual void OnTaskEnqueued(ThreadPoolEventArgs e)
        {
            UpdateEventArgsStats(e);
            TaskEnqueued?.Invoke(this, e);
        }

        protected virtual void OnTaskStarted(ThreadPoolEventArgs e)
        {
            UpdateEventArgsStats(e);
            TaskStarted?.Invoke(this, e);
        }

        protected virtual void OnTaskCompleted(ThreadPoolEventArgs e)
        {
            UpdateEventArgsStats(e);
            TaskCompleted?.Invoke(this, e);
        }

        protected virtual void OnTaskFailed(ThreadPoolEventArgs e)
        {
            UpdateEventArgsStats(e);
            TaskFailed?.Invoke(this, e);
        }

        protected virtual void OnScalingUp(ThreadPoolEventArgs e)
        {
            UpdateEventArgsStats(e);
            ScalingUp?.Invoke(this, e);
        }

        protected virtual void OnScalingDown(ThreadPoolEventArgs e)
        {
            UpdateEventArgsStats(e);
            ScalingDown?.Invoke(this, e);
        }

        private void UpdateEventArgsStats(ThreadPoolEventArgs e)
        {
            lock (_workersLock) e.ActiveWorkers = _activeWorkers;
            lock (_queueLock)
            {
                e.QueueSize = _taskQueue.Count;
                e.PendingTasks = _pendingTasks;
            }
        }
    }
}