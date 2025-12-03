using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public enum QuickBuyType
{
    NORMAL_UPGRADES,
    PRESTIGE_UPGRADES
}

public class QuickBuyBehaviour : MonoBehaviour
{
    [SerializeField]
    private TMP_Text priceTxt;

    [SerializeField]
    private Button buyButton;

    [SerializeField]
    private UpgradesBehaviour callbackListener;

    [SerializeField]
    private Image paymentTypeImg;

    public QuickBuyType upgradesType;
    private List<Upgrade> upgradesToCheck;
    private Func<double> GetAvailableFunds;

    private void Start()
    {
        switch(upgradesType)
        {
            case QuickBuyType.NORMAL_UPGRADES:
                paymentTypeImg.sprite = Game.GetSpriteForMineral(Mineral.ROCK);
                upgradesToCheck = Database.instance.activeLocation.normalUpgrades;
                GetAvailableFunds = () => Database.instance.activeLocation.data.rocks;
                break;
            case QuickBuyType.PRESTIGE_UPGRADES:
                paymentTypeImg.sprite = Game.GetSpriteForMineral(Mineral.PRESTIGE_POINT);
                upgradesToCheck = Database.instance.activeLocation.prestigeUpgrades;
                GetAvailableFunds = () => Database.instance.activeLocation.data.prestigePoints;
                break;
            default:
                Destroy(gameObject);
                return;
        }
    }

    private void OnEnable()
    {
        StartCoroutine(CheckUpgrades());
    }

    public void OnBuyButtonClick()
    {
        List<Upgrade> toBuy = GetAffordableUpgrades();

        if (toBuy.Count > 0)
        {
            List<Upgrade> bought = new List<Upgrade>();

            foreach (Upgrade upgrade in toBuy)
            {
                if(upgrade.Buy(false))
                {
                    bought.Add(upgrade);
                }
            }

            // Play bought sound once
            AudioManager.instance.Play(Sound.KA_CHING);

            // Remove the bought upgrades from the list of shown upgrades and add new upgrades to replace them
            callbackListener.RemoveUpgrades(bought);
            callbackListener.UpgradesBought(bought);

            UpdatePrice();
        }
    }

    private IEnumerator CheckUpgrades()
    {
        yield return null;

        while(true)
        {
            UpdatePrice();
            yield return DelayWait.oneFifthSecond;
        }
    }

    private void UpdatePrice()
    {
        List<Upgrade> affordables = GetAffordableUpgrades();
        buyButton.interactable = affordables.Count > 0;
        priceTxt.text = NumberFormatter.Format(GetPriceForAffordableUpgrades(affordables));
    }

    private List<Upgrade> GetAffordableUpgrades()
    {
        List<Upgrade> affords = new List<Upgrade>();

        double funds = GetAvailableFunds();

        foreach (Upgrade upgrade in upgradesToCheck)
        {
            if (!upgrade.isBought)
            {
                if (upgrade.price <= funds && upgrade.IsSafePurchase(funds))
                {
                    affords.Add(upgrade);
                    funds -= upgrade.price;
                }
                else
                {
                    break;
                }
            }
        }

        return affords;
    }

    private double GetPriceForAffordableUpgrades(List<Upgrade> affords)
    {
        double price = 0;

        foreach (Upgrade upgrade in affords)
        {
            price += upgrade.price;
        }

        return price;
    }
}
