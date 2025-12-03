using UnityEngine;

public enum UpgradeType
{
    SPEED,
    PROFITS,
    EFFECTIVENESS,
    FREE_PURCHASE,
    DISCOUNT
}

public abstract class Upgrade
{
    public const int TARGET_PRESTIGE_POINTS_ID = -2;

    public int ID { get; private set; }
    public double price { get; private set; }

    // Target ints above 0 are linked to the Collector ID
    public int upgradeTargetId { get; private set; }
    public UpgradeType upgradeType { get; private set; }
    public double upgradeValue { get; private set; }

    public bool isBought;

    public abstract bool HasSufficientFunds();
    protected abstract bool SubstractFunds(bool playSound);
    protected abstract void SaveBought();

    protected Upgrade(int ID, double price, int upgradeTargetId, UpgradeType upgradeType, double upgradeValue)
    {
        this.ID = ID;
        this.price = price;

        this.upgradeTargetId = upgradeTargetId;
        this.upgradeType = upgradeType;
        this.upgradeValue = upgradeValue;
    }

    public bool Buy(bool playSound)
    {
        // If the needed funds can be successfuly substracted from the current amount of rocks, then proceed with the purchase
        if(HasSufficientFunds())
        {
            if (SubstractFunds(playSound))
            {
                isBought = true;
                SaveBought();
                return true;
            }
        }

        return false;
    }

    public bool IsNormalUpgrade()
    {
        return GetType() == typeof(NormalUpgrade) || GetType().IsSubclassOf(typeof(NormalUpgrade));
    }

    public bool IsPrestigeUpgrade()
    {
        return GetType() == typeof(PrestigeUpgrade) || GetType().IsSubclassOf(typeof(PrestigeUpgrade));
    }

    public Sprite GetTargetSprite()
    {
        if (upgradeTargetId >= -1)
        {
            return Database.instance.activeLocation.GetSpriteForCollector(upgradeTargetId);
        }
        else if(upgradeTargetId == TARGET_PRESTIGE_POINTS_ID)
        {
            return Game.GetSpriteForMineral(Mineral.PRESTIGE_POINT);
        }

        // Return this by default
        return Database.instance.activeLocation.GetSpriteForCollector(Collector.TARGET_ALL_ID);
    }

    public string GetNameForTarget(bool shortName)
    {
        if (upgradeTargetId > 0)
        {
            return Database.instance.activeLocation.GetCollectorForId(upgradeTargetId).GetName(shortName);
        }
        else
        {
            switch (upgradeTargetId)
            {
                case Collector.TARGET_ALL_ID:
                    return Translator.GetTranslationForId(Translation_Script.ALL);
                case Collector.TARGET_CLICK_ID:
                    return Translator.GetTranslationForId(Translation_Script.CLICK);
                case TARGET_PRESTIGE_POINTS_ID:
                    return Translator.GetTranslationForId(Translation_Inspector.PRESTIGE_POINTS);
            }
        }

        return "???";
    }

    public string GetRewardString()
    {
        switch(upgradeType)
        {
            case UpgradeType.PROFITS:
                return Translator.GetTranslationForId(Translation_Script.PROFITS) + " x" + upgradeValue;
            case UpgradeType.SPEED:
                return Translator.GetTranslationForId(Translation_Script.SPEED) + " x" + upgradeValue;
            case UpgradeType.EFFECTIVENESS:
                return Translator.GetTranslationForId(Translation_Script.EFFECTIVENESS) + " +" + (upgradeValue * 100) + "%";
            case UpgradeType.FREE_PURCHASE:
                return "+" + upgradeValue + " " + GetNameForTarget(true);
            case UpgradeType.DISCOUNT:
                return (upgradeValue * 100) + "% " + Translator.GetTranslationForId(Translation_Script.CHEAPER);
            default:
                return "???";
        }
    }

    public void Reset()
    {
        isBought = false;
    }

    public virtual bool IsSafePurchase()
    {
        return true;
    }

    public virtual bool IsSafePurchase(double funds)
    {
        return true;
    }
}