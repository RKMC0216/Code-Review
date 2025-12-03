public class NormalUpgrade : Upgrade
{
    public static int GENERATED_ID = 1;

    public NormalUpgrade(double price, int upgradeTarget, UpgradeType upgradeType, double upgradeValue, LocationData data) : 
        base(GENERATED_ID, price, upgradeTarget, upgradeType, upgradeValue)
    {
        GENERATED_ID++;

        isBought = data.normalUpgradesBought.Contains(ID);
    }

    public override bool HasSufficientFunds()
    {
        return Database.instance.activeLocation.data.rocks >= price;
    }

    protected override bool SubstractFunds(bool playSound)
    {
        return Database.instance.activeLocation.SubstractRocks(price, playSound);
    }

    protected override void SaveBought()
    {
        Database.instance.activeLocation.data.normalUpgradesBought.Add(ID);
    }
}