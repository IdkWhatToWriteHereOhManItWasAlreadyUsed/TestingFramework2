using System;

namespace MyThreading
{
    public class ThreadPoolEventArgs : EventArgs
    {
        public string EventType { get; }
        public int WorkerId { get; }
        public string? Message { get; }
        public Exception? Error { get; }
        public DateTime Timestamp { get; }
        public int ActiveWorkers { get; internal set; }
        public int QueueSize { get; internal set; }
        public int PendingTasks { get; internal set; }

        public ThreadPoolEventArgs(string eventType, int workerId = -1, string? message = null, Exception? error = null)
        {
            EventType = eventType;
            WorkerId = workerId;
            Message = message;
            Error = error;
            Timestamp = DateTime.Now;
        }

        public override string ToString()
        {
            return $"[{Timestamp:HH:mm:ss.fff}] {EventType} | Worker:{WorkerId} | {Message}";
        }
    }
}