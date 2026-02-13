namespace TestsFramework.Attributes;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class PriorityAttribute(int level) : Attribute
{
    public int Level { get; set; } = level;
}