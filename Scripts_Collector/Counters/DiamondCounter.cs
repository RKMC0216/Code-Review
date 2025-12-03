using System.Collections.Generic;

public class DiamondCounter : ObservingCounter
{
    protected override string CurrentValue()
    {
        return Database.instance.Diamonds.ToString();
    }

    protected override List<IObserver> Registry()
    {
        return Database.instance.diamondsObservers;
    }
}