public class UpgradeNotificationBehaviour : UpdateButtonNotificationBehaviour
{
    private static NormalUpgrade normalUpgrade;
    private static PrestigeUpgrade prestigeUpgrade;

    private void Start()
    {
        UpdateNextUpgrade();
    }

    protected override bool ShouldNotify()
    {
        return (normalUpgrade != null && normalUpgrade.HasSufficientFunds()) 
            || (prestigeUpgrade != null && prestigeUpgrade.HasSufficientFunds() && prestigeUpgrade.IsSafePurchase());
    }

    public static void UpdateNextUpgrade()
    {
        normalUpgrade = GetNextNormalUpgrade();
        prestigeUpgrade = GetNextPrestigeUpgrade();
    }

    private static NormalUpgrade GetNextNormalUpgrade()
    {
        foreach(NormalUpgrade upgrade in Database.instance.activeLocation.normalUpgrades)
        {
            if(!upgrade.isBought)
            {
                return upgrade;
            }
        }

        return null;
    }

    private static PrestigeUpgrade GetNextPrestigeUpgrade()
    {
        foreach (PrestigeUpgrade upgrade in Database.instance.activeLocation.prestigeUpgrades)
        {
            if (!upgrade.isBought)
            {
                return upgrade;
            }
        }

        return null;
    }
}