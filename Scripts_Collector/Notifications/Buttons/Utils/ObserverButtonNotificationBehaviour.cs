using System.Collections.Generic;

public abstract class ObserverButtonNotificationBehaviour : ButtonNotificationBehaviour, IObserver
{
    public static List<IObserver> observers = new List<IObserver>();

    private void OnEnable()
    {
        InitNotification();
        observers.Add(this);
    }

    public void OnValueChanged()
    {
        UpdateNotification();
    }

    private void OnDisable()
    {
        observers.Remove(this);
    }

    public static void ValueChanged()
    {
        foreach(IObserver observer in observers)
        {
            observer.OnValueChanged();
        }
    }
}