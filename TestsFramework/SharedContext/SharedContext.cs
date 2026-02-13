
using System.Collections.Concurrent;

namespace TestsFramework.SharedContext
{
    public class SharedContext<T> where T : new()
    {
        private readonly string _contextName;
        private readonly ConcurrentStack<T> _stateHistory = new();
        
        public T CurrentState { get; private set; }
        
        public SharedContext(string contextName)
        {
            _contextName = contextName;
            CurrentState = new T();
        }
        
        public SharedContext(string contextName, T initialState)
        {
            _contextName = contextName;
            CurrentState = initialState;
            SaveState();
        }
        
        public void SaveState()
        {
            _stateHistory.Push(CurrentState);
            SharedContextManager.SaveSnapshot(_contextName, this);
        }
        
        public bool RestoreState()
        {
            if (SharedContextManager.RestoreSnapshot<SharedContext<T>>(_contextName, out var restored))
            {
                CurrentState = restored.CurrentState;
                return true;
            }
            return false;
        }
        
        public void UpdateState(Action<T> updateAction)
        {
            updateAction(CurrentState);
            SaveState();
        }
        
        public TResult ExecuteWithState<TResult>(Func<T, TResult> func)
        {
            SaveState();
            return func(CurrentState);
        }
    }
}
