using System;
using System.Collections;
using System.Threading.Tasks;
using TestsFramework.Assert;

namespace TestsFramework.Assert
{
    public static class Assert
    {
        // Базовые проверки
        public static void AreEqual(object expected, object actual, string message = "")
        {
            if (!object.Equals(expected, actual))
                throw new AssertFailedException($"Expected: {expected}, Actual: {actual}. {message}");
        }
        
        public static void AreNotEqual(object notExpected, object actual, string message = "")
        {
            if (object.Equals(notExpected, actual))
                throw new AssertFailedException($"Not expected: {notExpected}, Actual: {actual}. {message}");
        }
        
        public static void IsTrue(bool condition, string message = "")
        {
            if (!condition)
                throw new AssertFailedException($"Condition is false. {message}");
        }
        
        public static void IsFalse(bool condition, string message = "")
        {
            if (condition)
                throw new AssertFailedException($"Condition is true. {message}");
        }
        
        public static void IsNull(object value, string message = "")
        {
            if (value != null)
                throw new AssertFailedException($"Value is not null. {message}");
        }
        
        public static void IsNotNull(object value, string message = "")
        {
            if (value == null)
                throw new AssertFailedException($"Value is null. {message}");
        }
        
        // Проверки для коллекций
        public static void Contains(object expected, ICollection collection, string message = "")
        {
            foreach (var item in collection)
            {
                if (object.Equals(item, expected))
                    return;
            }
            throw new AssertFailedException($"Collection does not contain {expected}. {message}");
        }
        
        public static void DoesNotContain(object notExpected, ICollection collection, string message = "")
        {
            foreach (var item in collection)
            {
                if (object.Equals(item, notExpected))
                    throw new AssertFailedException($"Collection contains {notExpected}. {message}");
            }
        }
        
        // Проверка типов
        public static void IsInstanceOfType(object value, Type expectedType, string message = "")
        {
            if (value == null)
                throw new AssertFailedException($"Value is null. {message}");
            
            if (!expectedType.IsInstanceOfType(value))
                throw new AssertFailedException($"Expected type: {expectedType}, Actual type: {value.GetType()}. {message}");
        }
        
        // Проверка исключений
        public static void Throws<TException>(Action action, string message = "") where TException : Exception
        {
            try
            {
                action();
                throw new AssertFailedException($"Expected exception of type {typeof(TException)} was not thrown. {message}");
            }
            catch (TException)
            {
                // Успех - исключение ожидаемого типа было брошено
            }
            catch (Exception ex)
            {
                throw new AssertFailedException($"Expected exception of type {typeof(TException)}, but got {ex.GetType()}. {message}");
            }
        }
        
        // Асинхронные проверки
        public static async Task ThrowsAsync<TException>(Func<Task> action, string message = "") where TException : Exception
        {
            try
            {
                await action();
                throw new AssertFailedException($"Expected exception of type {typeof(TException)} was not thrown. {message}");
            }
            catch (TException)
            {
                // Успех
            }
            catch (Exception ex)
            {
                throw new AssertFailedException($"Expected exception of type {typeof(TException)}, but got {ex.GetType()}. {message}");
            }
        }
    }
}
