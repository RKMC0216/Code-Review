public class SpinNotificationBehaviour : ObserverButtonNotificationBehaviour
{
    protected override bool ShouldNotify()
    {
        return SpinBehaviour.IsFreeSpinAvailable();
    }
}
