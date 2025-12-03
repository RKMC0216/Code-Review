using System;

public class Stat
{
    public string title { get; private set; }
    public double value { get; private set; }
    public Func<double> valueFunc { get; private set; }

    public Stat(string title, double value)
    {
        this.title = title;
        this.value = value;
    }

    public Stat(string title, Func<double> valueFunc)
    {
        this.title = title;
        this.valueFunc = valueFunc;
    }

    public bool IsDynamicValue()
    {
        return valueFunc != null;
    }
}