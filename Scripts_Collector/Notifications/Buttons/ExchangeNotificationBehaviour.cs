public class ExchangeNotificationBehaviour : UpdateButtonNotificationBehaviour
{
    protected override bool ShouldNotify()
    {
        return Notify();
    }

    public static bool Notify()
    {
        return Database.instance.activeLocation.data.rocks >= ExchangeBehaviour.GetNextDiamondPrice() && ExchangeBehaviour.GetNextDiamondPrice() <= ExchangeBehaviour.MAX_DIAMOND_PRICE;
    }
}