using System.Collections.Generic;
using System.Collections;
using UnityEngine;

public class MenuBehaviour : MonoBehaviour
{
    public static bool isSideMenuOpen = false;
    public static int menuItemsOpen = 0;

    [SerializeField]
    private RectTransform menuSideBarRect;

    [SerializeField]
    private GameObject menuObj, menuItemPrefab,
        statsPrefab, milestonesPrefab, upgradesPrefab, prestigePrefab,
        exchangePrefab, shopPrefab, settingsPrefab, boostPrefab, spinPrefab,
        useSapphiresPrefab, useEmeraldsPrefab, useDiamondsPrefab, expandPrefab;

    [SerializeField]
    public GameObject upgradesButton, shopButton, boostButton, spinButton, expandButton;

    [SerializeField]
    public MenuNotificationBehaviour menuNotification;

    [SerializeField]
    public Game game;

    private void Start()
    {
        // If needed due to safe area, add extra space to the side menu bar
        menuSideBarRect.sizeDelta += new Vector2(SafeAreaHelper.PaddingRight(game.GetComponent<RectTransform>()), 0);
        
        StartCoroutine(WaitToShowUpgradesButton());
        StartCoroutine(WaitToShowBoostButton());
        StartCoroutine(WaitToShowShopButton());
        StartCoroutine(WaitToShowSpinButton());
        StartCoroutine(WaitToShowExpandButton());
    }

    private IEnumerator WaitToShowUpgradesButton()
    {
        // If still on earth and hasnt prestiged and bought any upgrades before, wait to show button till he has enough rocks
        if (Database.instance.activeLocation.metaData.ID == Locations.EARTH && Database.instance.activeLocation.data.prestiges == 0 && !Database.instance.activeLocation.normalUpgrades[0].isBought)
        {
            Location location = Database.instance.activeLocation;

            yield return new WaitUntil(() => location.data.rocks >= location.normalUpgrades[0].price && game.IsNoOverlays(true));

            // Double check if upgrades hasnt been secretly bought
            if(!location.normalUpgrades[0].isBought)
            {
                // Show tutorial explaining upgrades
                game.ShowTutorial(new Tutorial(new List<TutorialStep>()
                {
                    new ExplainTutorialStep(Translation_Script.TUT_UPGRADES_INTRO),
                    new ActionTutorialStep(() => upgradesButton.SetActive(true)),
                    new DelayTutorialStep(1),
                    new ClickHintTutorialStep(transform.root.Find("UI").Find("QuickMenu"), () => location.normalUpgrades[0].isBought, () => upgradesButton.transform.Find("Button").Find("Image").position, reversed: true)
                }));
            }
            else
            {
                upgradesButton.SetActive(true);
            }
        }
        else
        {
            upgradesButton.SetActive(true);
        }
    }

    private IEnumerator WaitToShowBoostButton()
    {
        // If still on earth and hasnt prestiged before, wait to show button till rock printer is bought
        if (Database.instance.activeLocation.metaData.ID == Locations.EARTH && Database.instance.activeLocation.data.prestiges == 0 &&
            Database.instance.activeLocation.GetCollectorForId(6) is Collector rockPrinters && rockPrinters.amountTotal == 0)
        {
            // Else wait till first rock printer is bought (ID = 6)
            yield return new WaitUntil(() => rockPrinters.amountTotal > 0);

            // Show tutorial explaining the x3 boost, when tutorial is finished show button
            game.ShowTutorial(new Tutorial(new List<TutorialStep>()
            {
                new DelayTutorialStep(.5f),
                new ExplainTutorialStep(Translation_Script.TUT_BOOST_INTRO),
                new ActionTutorialStep(() => boostButton.SetActive(true)),
                new DelayTutorialStep(1),
                new ClickHintTutorialStep(transform.root.Find("UI").Find("QuickMenu"), () => Database.instance.boostWithAd > 0, () => boostButton.transform.Find("Button").Find("Content").Find("Image").position, reversed: true)
            }));
        }
        else
        {
            boostButton.SetActive(true);
        }
    }

    private IEnumerator WaitToShowShopButton()
    {
        // If still on earth and hasnt prestiged before and unlocked the rock farms for a second time, wait to show button till he has done that
        if (Database.instance.activeLocation.metaData.ID == Locations.EARTH && Database.instance.activeLocation.GetCollectorForId(7) is Collector rockFarms && 
            (Database.instance.activeLocation.data.prestiges == 0 || (Database.instance.activeLocation.data.prestiges == 1 && rockFarms.amountTotal == 0)))
        {
            // Else wait till either prestiged a second time or unlocked the rock farms for the second time
            LocationData data = Database.instance.activeLocation.data;

            // Wait for the overlays to be closed (menu item and special deal pop-up)
            yield return new WaitUntil(() => (data.prestiges > 1 || (data.prestiges == 1 && rockFarms.amountTotal > 0)) && game.IsNoOverlays(true));

            // First show a pop-up with the special deal
            game.ShowSpecialPackOffer();

            // Wait for special deal pop-up and everything else to be closed
            yield return new WaitUntil(() => !game.showSpecialPackOfferQueued && game.IsNoOverlays(true));
        }

        shopButton.SetActive(true);
    }

    private IEnumerator WaitToShowSpinButton()
    {
        // If still on earth and prestiged less than twice, wait to show button till a moon portal is bought
        if (Database.instance.activeLocation.metaData.ID == Locations.EARTH && Database.instance.activeLocation.data.prestiges < 2 && Database.instance.totalSpin == 0 &&
            Database.instance.activeLocation.GetCollectorForId(9) is Collector moonPortals && moonPortals.amountTotal == 0)
        {
            // Else wait till first moon portal is bought (ID = 9)
            yield return new WaitUntil(() => moonPortals.amountTotal > 0);

            // Show tutorial explaining the spin, when tutorial is finished show button
            game.ShowTutorial(new Tutorial(new List<TutorialStep>()
            {
                new DelayTutorialStep(.5f),
                new ExplainTutorialStep(Translation_Script.TUT_SPIN_INTRO_1),
                new ExplainTutorialStep(Translation_Script.TUT_SPIN_INTRO_2),
                new ActionTutorialStep(() => spinButton.SetActive(true)),
                new DelayTutorialStep(1),
                new ClickHintTutorialStep(transform.root.Find("UI").Find("QuickMenu"), () => Database.instance.spinWithAd > 0, () => spinButton.transform.Find("Button").Find("Content").Find("Image").position, reversed: true)
            }));
        }
        else
        {
            spinButton.SetActive(true);
        }
    }

    private IEnumerator WaitToShowExpandButton()
    {
        if(!ExpandBehaviour.IsExpandUnlocked())
        {
            Collector dissolvers = Database.instance.activeLocation.GetCollectorForId(10);
            LocationData data = Database.instance.activeLocation.data;

            // Wait till first planet dissolver bought or prestiged for second time
            yield return new WaitUntil(() => (dissolvers.amountTotal >= ExpandBehaviour.UNLOCK_AT_AMOUNT_OF_DISSOLVERS && data.prestiges == 1) || data.prestiges >= 2);
            yield return null;
            yield return new WaitUntil(() => game.IsNoOverlays(true));

            // Show tutorial explaining expanding their business, when tutorial is finished show button
            game.ShowTutorial(new Tutorial(new List<TutorialStep>()
            {
                new ExplainTutorialStep(Translation_Script.TUT_EXPAND_UNLOCKED_1),
                new ExplainTutorialStep(Translation_Script.TUT_EXPAND_UNLOCKED_2),
                new ExplainTutorialStep(Translation_Script.TUT_EXPAND_UNLOCKED_3),
                new ExplainTutorialStep(Translation_Script.TUT_EXPAND_UNLOCKED_4),
                new ExplainTutorialStep(Translation_Script.TUT_EXPAND_UNLOCKED_5),
                new ActionTutorialStep(() => expandButton.SetActive(true)),
            }));
        }
        else
        {
            expandButton.SetActive(true);
        }
    }

    public void OnOpenMenuButtonClicked()
    {
        StartCoroutine(OpenMenu());
    }

    public void OnCloseMenuButtonClicked()
    {
        StartCoroutine(CloseMenu());
    }

    public void OnResourcesButtonClicked()
    {
        OpenMenuItemWithContent(statsPrefab);
    }

    public void OnStatsButtonClicked()
    {
        OpenMenuItemWithContent(statsPrefab, 1);
    }

    public void OnMilestonesButtonClicked()
    {
        OpenMenuItemWithContent(milestonesPrefab);
    }

    public void OnUpgradesButtonClicked()
    {
        OpenMenuItemWithContent(upgradesPrefab);
    }

    public void OnPrestigeButtonClicked()
    {
        OpenMenuItemWithContent(prestigePrefab);
    }

    public void OnExchangeButtonClicked()
    {
        OpenMenuItemWithContent(exchangePrefab);
    }

    public void OnShopButtonClicked()
    {
        // If user has never spent rubies before, show multipliers, else if he has rubies show time warps, else show rubies IAP
        OpenMenuItemWithContent(shopPrefab, Database.instance.activeLocation.metaData.ID == Locations.EARTH && Database.instance.activeLocation.data.rubiesSpent == 0 ? 1
            : Database.instance.Rubies < 10 ? 2 : 0);
    }

    public void OnExpandButtonClicked()
    {
        OpenMenuItemWithContent(expandPrefab);
    }

    public void OnSettingsButtonClicked()
    {
        OpenMenuItemWithContent(settingsPrefab);
    }

    public void OnBoostButtonClicked()
    {
        OpenMenuItemWithContent(boostPrefab);
    }

    public void OnSpinButtonClicked()
    {
        OpenMenuItemWithContent(spinPrefab);
    }

    public void OnUseSapphiresButtonClicked()
    {
        OpenMenuItemWithContent(useSapphiresPrefab);
    }

    public void OnUseEmeraldsButtonClicked()
    {
        OpenMenuItemWithContent(useEmeraldsPrefab);
    }

    public void OnUseDiamondsButtonClicked()
    {
        OpenMenuItemWithContent(useDiamondsPrefab);
    }

    private void OpenMenuItemWithContent(GameObject contentPrefab, int overrideInitialFragmentIndex = -1)
    {
        menuItemsOpen++;

        OnCloseMenuButtonClicked();
        menuItemPrefab.GetComponent<MenuItemBehaviour>().contentPrefab = contentPrefab;
        menuItemPrefab.GetComponent<MenuItemBehaviour>().overrideInitialFragmentIndex = overrideInitialFragmentIndex;
        menuItemPrefab.GetComponent<MenuItemBehaviour>().menu = this;
        Instantiate(menuItemPrefab, transform);
    }

    private bool isAnimating = false;

    private IEnumerator OpenMenu()
    {
        if(!isAnimating)
        {
            isSideMenuOpen = true;

            isAnimating = true;
            menuObj.SetActive(true);

            Vector2 startPos = new Vector2(menuSideBarRect.anchoredPosition.x, menuSideBarRect.anchoredPosition.y);
            Vector2 endPos = new Vector2(0, menuSideBarRect.anchoredPosition.y);

            yield return StartCoroutine(TransformAnimationHelper.MoveAnchoredPosition(menuSideBarRect, startPos, endPos, 0.2f));
            isAnimating = false;
        }
    }

    private IEnumerator CloseMenu()
    {
        if (!isAnimating)
        {
            isAnimating = true;
            Vector2 startPos = new Vector2(menuSideBarRect.anchoredPosition.x, menuSideBarRect.anchoredPosition.y);
            Vector2 endPos = new Vector2(menuSideBarRect.sizeDelta.x, menuSideBarRect.anchoredPosition.y);

            yield return StartCoroutine(TransformAnimationHelper.MoveAnchoredPosition(menuSideBarRect, startPos, endPos, 0.2f));

            menuObj.SetActive(false);
            isAnimating = false;

            isSideMenuOpen = false;
        }
    }
}