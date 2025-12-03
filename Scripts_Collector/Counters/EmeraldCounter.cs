using System.Collections.Generic;

public class EmeraldCounter : ObservingCounter
{
    protected override string CurrentValue()
    {
        return Database.instance.Emeralds.ToString();
    }

    protected override List<IObserver> Registry()
    {
        return Database.instance.emeraldsObservers;
    }
}