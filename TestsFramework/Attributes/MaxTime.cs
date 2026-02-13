namespace TestsFramework.Attributes;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class MaxTimeAttribute(int milliseconds) : Attribute
{
    public int Milliseconds { get; set; } = milliseconds;
}