using System.Collections.Generic;

public class SapphireCounter : ObservingCounter
{
    protected override string CurrentValue()
    {
        return Database.instance.Sapphires.ToString();
    }

    protected override List<IObserver> Registry()
    {
        return Database.instance.sapphiresObservers;
    }
}