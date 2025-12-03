using System.Collections.Generic;

public class RubyCounter : ObservingCounter
{
    protected override string CurrentValue()
    {
        return Database.instance.Rubies.ToString();
    }

    protected override List<IObserver> Registry()
    {
        return Database.instance.rubiesObservers;
    }
}