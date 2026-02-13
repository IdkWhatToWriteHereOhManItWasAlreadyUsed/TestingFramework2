
using System.Collections.Concurrent;

namespace TestsFramework.SharedContext
{
    public static class SharedContextManager
    {
        private static readonly ConcurrentDictionary<string, object> _contexts = new();
        private static readonly ConcurrentDictionary<string, Stack<object>> _contextSnapshots = new();
        
        public static T GetContext<T>(string contextName) where T : new()
        {
            return (T)_contexts.GetOrAdd(contextName, _ => new T());
        }
        
        public static void SaveSnapshot<T>(string contextName, T context)
        {
            var snapshotStack = _contextSnapshots.GetOrAdd(contextName, _ => new Stack<object>());
            
            if (context is ICloneable cloneable)
            {
                snapshotStack.Push(cloneable.Clone());
            }
            else
            {
                snapshotStack.Push(context);
            }
        }
        
        public static bool RestoreSnapshot<T>(string contextName, out T? restoredContext)
        {
            if (_contextSnapshots.TryGetValue(contextName, out var snapshotStack) && snapshotStack.Count > 0)
            {
                restoredContext = (T)snapshotStack.Pop();
                _contexts[contextName] = restoredContext;
                return true;
            }
            
            restoredContext = default;
            return false;
        }
        
        public static void ClearContext(string contextName)
        {
            _contexts.TryRemove(contextName, out _);
            _contextSnapshots.TryRemove(contextName, out _);
        }
        
        public static bool HasContext(string contextName)
        {
            return _contexts.ContainsKey(contextName);
        }
    }
}
