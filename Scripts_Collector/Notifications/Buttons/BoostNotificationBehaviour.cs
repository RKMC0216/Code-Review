public class BoostNotificationBehaviour : ObserverButtonNotificationBehaviour
{
    protected override bool ShouldNotify()
    {
        return Database.instance.activeLocation.data.adBoostTime == 0;
    }
}