public class MenuNotificationBehaviour : UpdateButtonNotificationBehaviour
{
    private void Start()
    {
        // Checking every second is a bit of an overkill
        interval = 5;
    }

    protected override bool ShouldNotify()
    {
        // Notify when either the prestige or exchange has something to notify
        return PrestigeNotificationBehaviour.Notify() || ExchangeNotificationBehaviour.Notify() || ExpandNotificationBehaviour.Notify();
    }
}