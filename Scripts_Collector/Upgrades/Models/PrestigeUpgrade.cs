public class PrestigeUpgrade : Upgrade
{
    public const double SAFE_PURCHASE_THRESHOLD = 0.01;

    public static int GENERATED_ID = 1;

    public PrestigeUpgrade(double price, int upgradeTarget, UpgradeType upgradeType, double upgradeValue, LocationData data) : 
        base(GENERATED_ID, price, upgradeTarget, upgradeType, upgradeValue)
    {
        GENERATED_ID++;

        isBought = data.prestigeUpgradesBought.Contains(ID);
    }

    public override bool HasSufficientFunds()
    {
        return Database.instance.activeLocation.data.prestigePoints >= price;
    }

    protected override bool SubstractFunds(bool playSound)
    {
        Database.instance.activeLocation.SubstractPrestigePoints(price, playSound);
        return true;
    }

    protected override void SaveBought()
    {
        Database.instance.activeLocation.data.prestigeUpgradesBought.Add(ID);
    }

    public override bool IsSafePurchase()
    {
        return price <= Database.instance.activeLocation.data.prestigePoints * SAFE_PURCHASE_THRESHOLD;
    }

    public override bool IsSafePurchase(double funds)
    {
        return price <= funds * SAFE_PURCHASE_THRESHOLD;
    }
}