public class PrestigeNotificationBehaviour : UpdateButtonNotificationBehaviour
{
    protected override bool ShouldNotify()
    {
        return Notify();
    }

    public static bool Notify()
    {
        return Database.instance.activeLocation.PrestigeAdviced();
    }
}