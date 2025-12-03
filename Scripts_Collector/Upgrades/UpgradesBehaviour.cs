using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UpgradesBehaviour : GenericMenuItem
{
    private const int MAX_UPGRADES_SHOWN = 20;

    [SerializeField]
    private GameObject upgradePrefab;

    [SerializeField]
    private RectTransform normalContainer, prestigeContainer;

    [SerializeField]
    private GameObject normalQuickBuy, prestigeQuickBuy;

    private void Start()
    {
        // Apply safe area to the bottom padding of the scrollview's content
        normalContainer.GetComponent<VerticalLayoutGroup>().padding.bottom += (int) SafeAreaHelper.PaddingBottom(transform.root.GetComponent<RectTransform>());
        prestigeContainer.GetComponent<VerticalLayoutGroup>().padding.bottom += (int)SafeAreaHelper.PaddingBottom(transform.root.GetComponent<RectTransform>());

        normalQuickBuy.SetActive(Database.instance.activeLocation.data.prestiges >= 3);
        prestigeQuickBuy.SetActive(Database.instance.activeLocation.data.prestiges >= 3);

        Create(Database.instance.activeLocation.normalUpgrades, normalContainer);
        Create(Database.instance.activeLocation.prestigeUpgrades, prestigeContainer);
    }

    public override void OnOpened(int fragmentIndex)
    {
        // Check if location = earth, prestige = 0 and first upgrade isn't bought yet
        if(Database.instance.activeLocation.metaData.ID == Locations.EARTH && Database.instance.activeLocation.data.prestiges == 0 && !Database.instance.activeLocation.normalUpgrades[0].isBought)
        {
            // Check if the first upgrade can be bought
            if (Database.instance.activeLocation.normalUpgrades[0].HasSufficientFunds())
            {
                // Tell to buy upgrade
                menu.game.ShowTutorial(new Tutorial(new List<TutorialStep>
                {
                    new ExplainTutorialStep(Translation_Script.TUT_NORMAL_UPGRADES),
                    new ClickHintTutorialStep(normalContainer, () => Database.instance.activeLocation.normalUpgrades[0].isBought, () => normalContainer.GetChild(0).GetComponent<UpgradeBehaviour>().buyButton.transform.position)
                }));
            }
        }
    }

    public override void OnFragmentSwitched(int index)
    {
        // If prestige upgrades is opened
        if(index == 1)
        {
            // Check if location = earth and has never spent prestige points before
            if(Database.instance.activeLocation.metaData.ID == Locations.EARTH && Database.instance.activeLocation.data.lifetimePrestigePointsSacrificed == 0)
            {
                // Check if user has enough prestige points to afford the first upgrade
                if(Database.instance.activeLocation.prestigeUpgrades[0].HasSufficientFunds())
                {
                    // Check if the upgrade can be bought safely
                    if (Database.instance.activeLocation.prestigeUpgrades[0].IsSafePurchase())
                    {
                        // Tell him to buy it!
                        menu.game.ShowTutorial(new Tutorial(new List<TutorialStep>
                        {
                            new ExplainTutorialStep(Translation_Script.TUT_PRESTIGE_UPGRADES_UNLOCKED_1),
                            new ExplainTutorialStep(Translation_Script.TUT_PRESTIGE_UPGRADES_UNLOCKED_2),
                            new ExplainTutorialStep(Translation_Script.TUT_PRESTIGE_UPGRADES_UNLOCKED_3),
                        }));
                    }
                    else
                    {
                        // Tell him it's not wise to spend a large amount of prestige points
                        menu.game.ShowTutorial(new Tutorial(new List<TutorialStep>
                        {
                            new ExplainTutorialStep(Translation_Script.TUT_PRESTIGE_UPGRADES_UNSUFFICIENT_1),
                            new ExplainTutorialStep(Translation_Script.TUT_PRESTIGE_UPGRADES_UNSUFFICIENT_2),
                        }));
                    }
                }
                else
                {
                    // Check if he has any prestige points
                    if(Database.instance.activeLocation.data.prestigePoints == 0)
                    {
                        // Tell him that he doesnt have any prestige points yet
                        menu.game.ShowTutorial(new Tutorial(new List<TutorialStep>
                        {
                            new ExplainTutorialStep(Translation_Script.TUT_PRESTIGE_UPGRADES_LOCKED),
                        }));
                    }
                }
            }
        }
    }

    public void UpgradesBought(List<Upgrade> upgrades)
    {
        if(upgrades == null || upgrades.Count == 0)
        {
            return;
        }

        // Add new upgrades to the list to replace the bought ones
        if(upgrades[0].IsNormalUpgrade())
        {
            Add(Database.instance.activeLocation.normalUpgrades, normalContainer, upgrades.Count);
        }
        else if(upgrades[0].IsPrestigeUpgrade())
        {
            Add(Database.instance.activeLocation.prestigeUpgrades, prestigeContainer, upgrades.Count);
        }

        // Check if there are any free collectors to be bought
        foreach (Upgrade upgrade in upgrades)
        {
            if (upgrade.upgradeType == UpgradeType.FREE_PURCHASE)
            {
                menu.game.collectors[upgrade.upgradeTargetId - 1].Buy(upgrade.upgradeValue, false);
            }
        }

        // Update everything
        menu.game.UpdateEverything();

        // Update the upgrade to check for the button's notification
        UpgradeNotificationBehaviour.UpdateNextUpgrade();
    }

    public void RemoveUpgrades(List<Upgrade> upgrades)
    {
        if (upgrades == null || upgrades.Count == 0)
        {
            return;
        }

        // Remove the bought upgrades from the list of shown upgrades
        if (upgrades[0].IsNormalUpgrade())
        {
            Clean(normalContainer);
        }
        else if (upgrades[0].IsPrestigeUpgrade())
        {
            Clean(prestigeContainer);
        }
    }

    private void Create(List<Upgrade> upgrades, RectTransform container)
    {
        int items = 0;

        foreach(Upgrade upgrade in upgrades)
        {
            if(!upgrade.isBought)
            {
                CreateUpgradeGO(upgrade, container);
                items++;

                if (items >= MAX_UPGRADES_SHOWN)
                {
                    return;
                }
            }
        }
    }

    private void Add(List<Upgrade> upgrades, RectTransform container, int amount)
    {
        // This is the amount of unbought upgrades already being shown, can't be lower than 0
        int items = Mathf.Max(0, MAX_UPGRADES_SHOWN - amount);

        // This is the amount of items that will have to be skipped to prevent adding duplicates
        int toSkip = items;

        int skipped = 0;

        foreach(Upgrade upgrade in upgrades)
        {
            if(!upgrade.isBought)
            {
                if(skipped < toSkip)
                {
                    // Skip this one, because it's already being shown
                    skipped++;
                }
                else
                {
                    CreateUpgradeGO(upgrade, container);
                    items++;

                    if (items >= MAX_UPGRADES_SHOWN)
                    {
                        return;
                    }
                }
            }
        }
    }

    private void Clean(RectTransform container)
    {
        foreach(UpgradeBehaviour upgradeBehaviour in container.GetComponentsInChildren<UpgradeBehaviour>())
        {
            upgradeBehaviour.DestroyIfBought();
        }
    }

    private void CreateUpgradeGO(Upgrade upgrade, RectTransform container)
    {
        GameObject upgradeGO = Instantiate(upgradePrefab, container);
        upgradeGO.GetComponent<UpgradeBehaviour>().upgrade = upgrade;
        upgradeGO.GetComponent<UpgradeBehaviour>().boughtCallback = UpgradesBought;
    }
}