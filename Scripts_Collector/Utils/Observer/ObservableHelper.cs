using System.Collections.Generic;

public static class ObservableHelper
{
    public static void Notify(List<IObserver> observers)
    {
        foreach (IObserver observer in observers)
        {
            observer.OnValueChanged();
        }
    }
}