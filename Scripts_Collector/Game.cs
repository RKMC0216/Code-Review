using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.IO;
using GoogleMobileAds.Ump.Api;
#if UNITY_IOS
using UnityEngine.iOS;
#endif

public class Game : MonoBehaviour
{
    // Used to refer to the app page to write a review
    private const string APPLE_APP_ID = "1451822510";

    private const string PLAYER_PREFS_IS_RATED = "IsRated";
    private const string PLAYER_PREFS_IS_ENJOYED = "IsEnjoyed";

    private const string MINERALS_SPRITES_PATH = "Minerals";
    private const float SHOW_OFFLINE_INCOME_AFTER_SECONDS = 300;
    private const float GEMS_PER_OFFLINE_SECONDS = 7200;
    private const int MAX_GEMS_PER_OFFLINE_SESSION = 5;

    private static int session = 0;

    public static Sprite GetSpriteForMineral(Mineral mineral)
    {
        return LoadSprite(GetPathForMineral(mineral));
    }

    public static Sprite GetSpriteOutlineForMineral(Mineral mineral)
    {
        return LoadSprite(string.Concat(GetPathForMineral(mineral), "Outline"));
    }

    private static string GetPathForMineral(Mineral mineral)
    {
        switch (mineral)
        {
            case Mineral.ROCK:
                return Path.Combine(MINERALS_SPRITES_PATH, Database.instance.activeLocation.metaData.FolderName(), "Rock");
            case Mineral.SAPPHIRE:
                return Path.Combine(MINERALS_SPRITES_PATH, "Sapphire");
            case Mineral.EMERALD:
                return Path.Combine(MINERALS_SPRITES_PATH, "Emerald");
            case Mineral.RUBY:
                return Path.Combine(MINERALS_SPRITES_PATH, "Ruby");
            case Mineral.DIAMOND:
                return Path.Combine(MINERALS_SPRITES_PATH, "Diamond");
            case Mineral.PRESTIGE_POINT:
                return Path.Combine(MINERALS_SPRITES_PATH, Database.instance.activeLocation.metaData.FolderName(), "Pet rock");
            default:
                return null;
        }
    }

    private static Sprite LoadSprite(string path)
    {
        return Resources.Load<Sprite>(path);
    }

    public const string GO_CLONE = "(Clone)";

    [SerializeField]
    public GameObject boulderIntoPrefab, popUpPrefab, notificationPrefab, specialNotificationPrefab,
        resourcesEarnedPrefab, offlineIncomePrefab, specialPackPrefab, tutorialPrefab, askEnjoyedPopUpPrefab, askToRatePopUpPrefab,
        boosterPrefab, dailyLoginBonusPopUpPrefab;

    [SerializeField]
    private RectTransform collectorsContainer, collectorsBottom, collectorsTop;

    [SerializeField]
    private BoulderBehaviour boulder;
    [SerializeField]
    public BoostMenuButtonBehaviour adBoost;
    [SerializeField]
    public SpinMenuButtonBehaviour spin;

    public List<CollectorBehaviour> collectors { get; private set; } = new List<CollectorBehaviour>();

    public bool showSpecialPackOfferQueued { get; private set; } = false;
    public bool showDailyLoginRewardQueued { get; private set; } = false;

    private void Start()
    {
        // Keep track of how many sessions the user goes through (session = each time an area is visited)
        session++;

        // Store install date if not present already
        if (Database.instance.installDate == null)
        {
            // Register the current date as the install date
            Database.instance.installDate = TimeManager.instance.Time();
        }

        // Setup the screen sizes to account for the safe area and any notches on the top of the screen
        ApplySafeAreas();

        // If a profit booster is available on this location, add the booster to the collectors container before adding the collectors
        if (Database.instance.activeLocation.profitBooster != null)
        {
            Instantiate(boosterPrefab, collectorsContainer);
        }

        // Create the collectors
        CreateCollectors();

        // If new player, show first tutorial
        if (Database.instance.activeLocation.metaData.ID == Locations.EARTH && BoulderBehaviour.CalcLevel() < 2)
        {
            boulder.gameObject.SetActive(false);
            Instantiate(boulderIntoPrefab, transform);
        }

        // Queues to show the daily login reward pop-up, if its available
        ShowClaimDailyReward();

        // On the first session show special pack offer, unless player has never prestiged before and is fairly new
        if (session == 1 && (Database.instance.activeLocation.metaData.ID != Locations.EARTH || Database.instance.activeLocation.data.prestiges > 1 ||
            (Database.instance.activeLocation.data.prestiges == 1 && Database.instance.activeLocation.GetCollectorForId(7).amountTotal > 0)))
        {
            // Queues to show the special pack offer
            ShowSpecialPackOffer();
        }

        // Calculate offline income and start collecting
        StartCoroutine(StartGame());

        if (Database.instance.activeLocation.metaData.ID == Locations.EARTH && Database.instance.activeLocation.data.prestiges == 0)
        {
            StartCoroutine(WaitToShowPrestigeTutorial());
        }
        
        if (session > 1)
        {
            // If this isnt the first session it means the user has travelled to a different location, this is an opportunity to show an interstitial ad
            InterstitialAdOpportunity();
        }
    }

    private void ApplySafeAreas()
    {
        // Set the height of the collectors bottom and top to comply with the safe area offset on the bottom/top of the screen
        RectTransform canvasRect = transform.root.GetComponent<RectTransform>();
        collectorsTop.sizeDelta += new Vector2(0, SafeAreaHelper.PaddingTop(canvasRect));
        collectorsBottom.sizeDelta += new Vector2(0, SafeAreaHelper.PaddingBottom(canvasRect));
    }

    private void OnApplicationPause(bool pause)
    {
        if (pause)
        {
            PauseGame();
        }
        else
        {
            StartCoroutine(ResumeGame());
        }
    }

    private void OnDestroy()
    {
        SaveProgress();
    }

    private IEnumerator StartGame()
    {
        float offlineTime = CalcOfflineTime();

        // To prevent the offline income pop-up from showing for one frame when the game is first installed and opened,
        // check for the year of the last online time, which should be 2020 or higher
        if (Database.instance.activeLocation.data.lastOnlineTime.Year >= 2020)
        {
            if (offlineTime > SHOW_OFFLINE_INCOME_AFTER_SECONDS)
            {
                // Already create the offline income pop-up so the first frame doesnt show the game
                GameObject offlineIncomePopUp = ShowOfflineIncomeScreen(offlineTime, null);

                // Wait for collectors to be instantiated and have updated their income in Start
                yield return null;

                // Calculate the large amount of offline income
                Dictionary<Mineral, double> income = CalcOfflineIncome(offlineTime);

                // Grant it
                GrantOfflineIncome(income);

                // Fill the pop-up with the offline income so it can be doubled, will automatically close if income is empty
                // Check if pop-up was closed to prevent any null pointer exceptions
                if (offlineIncomePopUp != null)
                {
                    offlineIncomePopUp.GetComponent<OfflineIncomeBehaviour>().SetIncome(income);
                }
            }
            else
            {
                // Wait for collectors to be instantiated and have updated their income in Start
                yield return null;

                // Grant the small amount of offline income
                GrantOfflineIncome(CalcOfflineIncome(offlineTime));
            }
        }
        else
        {
            // Else just wait one frame before starting the collectors
            yield return null;
        }

        // Make all collectors start collecting
        StartCollecting();
    }

    private void PauseGame()
    {
        // Make all collectors stop collecting
        StopCollecting();

        // Save game progress, if ad is showing this is already done
        if (!Advertisement.instance.IsShowingAd)
        {
            SaveProgress();
        }
    }

    private IEnumerator ResumeGame()
    {
        // Wait a frame for time to be correct
        yield return null;

        // Calc offline time and income
        float offlineTime = CalcOfflineTime();
        Dictionary<Mineral, double> offlineIncome = CalcOfflineIncome(offlineTime);

        // Grant the offline income
        GrantOfflineIncome(offlineIncome);

        // Check if should show offline income pop-up
        if (offlineIncome[Mineral.ROCK] > 0 && offlineTime > SHOW_OFFLINE_INCOME_AFTER_SECONDS)
        {
            // Check for any previous offline income pop-ups
            Transform otherPopUp = transform.Find(offlineIncomePrefab.name + GO_CLONE);

            if (otherPopUp != null)
            {
                // Only show a new pop-up if this one has a larger offline time, then also destroy the other one
                if (otherPopUp.GetComponent<OfflineIncomeBehaviour>().time < offlineTime)
                {
                    Destroy(otherPopUp.gameObject);
                    ShowOfflineIncomeScreen(offlineTime, offlineIncome);
                }
            }
            else
            {
                ShowOfflineIncomeScreen(offlineTime, offlineIncome);
            }
        }

        // Resume collectors
        StartCollecting();

        // Also show daily login reward if applicable
        ShowClaimDailyReward();
    }

    private float CalcOfflineTime()
    {
        return Mathf.Floor((float)(TimeManager.instance.Time() - Database.instance.activeLocation.data.lastOnlineTime).TotalSeconds);
    }

    public void ShowRewardedVideoAd(Action rewardCallback)
    {
        // Make sure the game is saved first
        SaveProgress();

        // Then show the video
        Advertisement.instance.ShowRewardedAd(rewardCallback);
    }

    public void InterstitialAdOpportunity()
    {
        // There are some prerequisites for interstitial ads to be requested in the first place, these can be found in AdsSettings.cs

        // Check if
        if (// The user hasnt bought more than 50 rubies or a bundle
            Database.instance.IAPScore < 5
            // An interstitial ad is ready to show
            && Advertisement.instance.IsInterstitialAdReady()
            // Has been online for at least 5 minutes
            && Time.realtimeSinceStartup >= 300
            // Has not seen an ad in the last 3 minutes
            && Advertisement.instance.GetSecondsSinceLastAdView() >= 180)
        {
            // Make sure the game is saved first
            SaveProgress();

            // Then show the interstitial
            Advertisement.instance.ShowInterstitialAd();
        }
    }

    public bool IsNoOverlays(bool includeMenuItems)
    {
        return transform.childCount == 3 && !MenuBehaviour.isSideMenuOpen && (!includeMenuItems || MenuBehaviour.menuItemsOpen == 0);
    }

    private bool IsNewDailyRewardReady()
    {
        return TimeManager.instance.Time().Date > Database.instance.lastLoginRewardClaimed.Date;
    }

    private void ShowClaimDailyReward()
    {
        if(!showDailyLoginRewardQueued)
        {
            StartCoroutine(ShowClaimDailyRewardWhenReady());
        }
    }

    private IEnumerator ShowClaimDailyRewardWhenReady()
    {
        showDailyLoginRewardQueued = true;

        yield return WaitForFreeFrames(5);

        if(IsNewDailyRewardReady())
        {
            ShowPopUp(dailyLoginBonusPopUpPrefab, false, closeCallback: () => { showDailyLoginRewardQueued = false; });
        }
        else
        {
            showDailyLoginRewardQueued = false;
        }
    }

    public void ShowSpecialPackOffer()
    {
        if (!showSpecialPackOfferQueued)
        {
            StartCoroutine(ShowSpecialPackOfferWhenReady());
        }
    }

    private IEnumerator ShowSpecialPackOfferWhenReady()
    {
        showSpecialPackOfferQueued = true;

        yield return WaitForFreeFrames(8);

        // Check if there is a new special pack available or if the starter pack is still available
        if (SpecialPack.IsNoSpecialPackActive() || SpecialPack.GetActiveSpecialPack().ID.Equals(SpecialPack.STARTER_PACK))
        {
            // If there is no pack active (unless its the starter pack), a new pack is automaticaly generated when the pop-up opens
            ShowPopUp(specialPackPrefab, true, closeCallback: () => { showSpecialPackOfferQueued = false; });
        }
        else
        {
            // If there is already a special pack active that has been shown before, dont show it
            showSpecialPackOfferQueued = false;
        }
    }

    private IEnumerator WaitForFreeFrames(int frames)
    {
        // Wait untill there are no pop-ups etc. showing for atleast x consecutive frames
        int freeFrames = 0;

        while (freeFrames < frames)
        {
            if (IsNoOverlays(true) && !Advertisement.instance.IsShowingAd)
            {
                freeFrames++;
            }
            else
            {
                freeFrames = 0;
            }

            yield return null;
        }
    }

    private void CreateCollectors()
    {
        GameObject collectorPrefab = Database.instance.activeLocation.GetCollectorPrefab();

        foreach (Collector collector in Database.instance.activeLocation.collectors)
        {
            GameObject col = Instantiate(collectorPrefab, collectorsContainer);
            col.GetComponent<CollectorBehaviour>().collector = collector;

            collectors.Add(col.GetComponent<CollectorBehaviour>());
        }

        collectorsBottom.SetAsLastSibling();
    }

    private void DestroyCollectors()
    {
        foreach (CollectorBehaviour col in collectors)
        {
            col.StopCollecting();
            Destroy(col.gameObject);
        }

        collectors.Clear();
    }

    public void GrantPowerUp(PowerUp powerUp)
    {
        switch (powerUp.powerUpType)
        {
            case PowerUpType.SUPER_TAP:
                if (BoulderBehaviour.goldRockTime > 0)
                {
                    BoulderBehaviour.goldRockTime += (int)powerUp.ActiveValue();
                }
                else
                {
                    StartCoroutine(CountdownGoldRock((int)powerUp.ActiveValue()));
                }

                Database.instance.activeLocation.data.goldRockSuperTaps++;
                break;
            case PowerUpType.TIME_WARP:
                Database.instance.activeLocation.AddRocks(powerUp.ActiveValue());
                Database.instance.activeLocation.data.goldRockTimeWarps++;
                break;
        }
    }

    private IEnumerator CountdownGoldRock(int initial)
    {
        BoulderBehaviour.goldRockTime = initial;
        boulder.UpdateBoulderStage();
        boulder.UpdateIncome();

        while (BoulderBehaviour.goldRockTime > 0)
        {
            yield return DelayWait.oneSecond;
            BoulderBehaviour.goldRockTime--;
        }

        BoulderBehaviour.goldRockTime = 0;
        boulder.UpdateIncome();
        boulder.UpdateBoulderStage();
    }

    public void ShowPopUp(GameObject content, bool backgroundClickCloses, Action<bool> callback = null, Action<GameObject> initContent = null, float closeAfterDelay = 0, Action closeCallback = null)
    {
        GameObject popUp = Instantiate(popUpPrefab, transform);
        PopUpBehaviour popUpBehaviour = popUp.GetComponent<PopUpBehaviour>();

        popUpBehaviour.contentPrefab = content;
        popUpBehaviour.backgroundClickCloses = backgroundClickCloses;
        popUpBehaviour.closeAfterDelay = closeAfterDelay;
        popUpBehaviour.callback = callback;
        popUpBehaviour.closeCallback = closeCallback;
        popUpBehaviour.initContent = initContent;
    }

    public void ShowResourcesEarned(List<GrantedResource> resources, Sound sound = Sound.GEMS_GAIN, string overrideTitle = null)
    {
        if (resources == null || resources.Count == 0)
        {
            return;
        }

        ShowPopUp(resourcesEarnedPrefab, true,
            initContent: (GameObject GO) =>
            {
                GO.GetComponent<ResourcesGrantedBehaviour>().title = string.IsNullOrEmpty(overrideTitle) ? Translator.GetTranslationForId(Translation_Script.RESOURCES_EARNED) : overrideTitle;
                GO.GetComponent<ResourcesGrantedBehaviour>().resources = resources;
            },
            closeAfterDelay: 2.5f);

        // Play a sound when the pop-up is opened
        AudioManager.instance.Play(sound, .2f);
    }

    public void Prestige(double points, bool reset)
    {
        if (points <= 0)
        {
            return;
        }

        if (reset)
        {
            DestroyCollectors();
            Database.instance.ResetBuyAmount();
        }

        Database.instance.activeLocation.Prestige(reset);
        Database.instance.activeLocation.AddPrestigePoints(points);

        if (reset)
        {
            UpgradeNotificationBehaviour.UpdateNextUpgrade();
            CreateCollectors();
        }
        else
        {
            UpdateIncomes();
        }

        // Reset scroll position to top
        collectorsContainer.anchoredPosition = new Vector2(0, 0);

        // Wait a frame for the new collectors to be instantiated and then start collecting
        StartCoroutine(StartCollectingAfterFrame());
    }

    private IEnumerator StartCollectingAfterFrame()
    {
        yield return null;
        StartCollecting();
    }

    public double CalcIncomePerSecond(bool boostable)
    {
        return Math.Floor(CalcRawIncomePerSecond(boostable));
    }

    public double CalcRawIncomePerSecond(bool boostable)
    {
        // boostable = whether the returned value should include the ad boost when its active
        double income = 0;

        foreach (CollectorBehaviour collector in collectors)
        {
            income += collector.CalcIncomePerSecond();
        }

        if (!boostable)
        {
            income /= Database.instance.activeLocation.GetActiveAdBonusMultiplier();
            income /= Database.instance.activeLocation.GetProfitBoosterBonusMultiplier();
        }

        return income;
    }

    public void UpdateEverything()
    {
        foreach (CollectorBehaviour col in collectors)
        {
            col.UpdateEverything();
        }

        // Also update boulder click income
        UpdateBoulderClickIncome();
    }

    public void UpdateIncomes()
    {
        foreach (CollectorBehaviour col in collectors)
        {
            col.UpdateTotalIncome();
        }

        // Also update boulder click income
        UpdateBoulderClickIncome();
    }

    public void UpdateBoulderClickIncome()
    {
        // By checking if the max HP is larger than 0, we can be certain that the boulder has finished its Start function,
        // This is done solely to prevent this call from causing an null pointer exception when the language is changed during the first tutorial,
        // In which the boulder is still disabled, and thus does not have a reference to the game script which it needs to calc the income
        if(boulder.maxHP > 0)
        {
            boulder.UpdateIncome();
        }
    }

    public void BoulderIntroFinished()
    {
        boulder.gameObject.SetActive(true);

        ShowTutorial(new Tutorial(new List<TutorialStep>
        {
            new DelayTutorialStep(2),
            new ExplainTutorialStep(Translation_Script.TUT_BUY_COLS_1),
            new ExplainTutorialStep(Translation_Script.TUT_BUY_COLS_2),
            new ExplainTutorialStep(Translation_Script.TUT_BUY_COLS_3),
            new ClickHintTutorialStep(collectorsContainer, () => Database.instance.activeLocation.collectors[0].amountTotal > 0, () => collectors[0].buyButton.transform.position),
            // Wait till user has enough rocks to buy Geologists, then tell him to invest in better collectors
            new WaitUntilTutorialStep(() => Database.instance.activeLocation.data.rocks >= Database.instance.activeLocation.collectors[1].basePrice),
            new ExplainTutorialStep(Translation_Script.TUT_BUY_COLS_4),
            new ClickHintTutorialStep(collectorsContainer, () => Database.instance.activeLocation.collectors[1].amountTotal > 0, () => collectors[1].buyButton.transform.position),
            // Wait till user has enough rocks to buy Heavy Machines, then show click hint
            new WaitUntilTutorialStep(() => Database.instance.activeLocation.data.rocks >= Database.instance.activeLocation.collectors[2].basePrice),
            new ClickHintTutorialStep(collectorsContainer, () => Database.instance.activeLocation.collectors[2].amountTotal > 0, () => collectors[2].buyButton.transform.position),
        }));
    }

    public void NotifyMilestonesCompleted(List<Milestone> milestones)
    {
        if (milestones == null || milestones.Count == 0)
        {
            return;
        }

        // Pick the most important milestone you want to notify for and notify it
        for (int i = milestones.Count - 1; i >= 0; i--)
        {
            if (milestones[i].HasSpecialReward())
            {
                ShowNotification(specialNotificationPrefab, new NotificationInfo(milestones[i]));
                return;
            }
        }

        // Else just notify the last milestones, which will probably be the highest lvl milestone
        ShowNotification(notificationPrefab, new NotificationInfo(milestones[milestones.Count - 1]));
    }

    public void NotifyLevelUp()
    {
        ShowNotification(notificationPrefab, new NotificationInfo("Level up!", Database.instance.activeLocation.GetSpriteForCollector(Collector.TARGET_CLICK_ID), Sound.LEVEL_UP, 38));
    }

    private void ShowNotification(GameObject prefab, NotificationInfo info)
    {
        // Destroy any normal notifications already showing, Destroy(null) does NOT cause any crashes
        Destroy(transform.Find(notificationPrefab.name + GO_CLONE)?.gameObject);

        GameObject notification = Instantiate(prefab, transform);
        notification.GetComponent<NotificationBehaviour>().info = info;
    }

    private GameObject ShowOfflineIncomeScreen(float offlineTime, Dictionary<Mineral, double> income)
    {
        // Show an offline income pop-up with the opportunity to double offline income (ONLY ROCKS)
        GameObject offlineIncomeGO = Instantiate(offlineIncomePrefab, transform);
        OfflineIncomeBehaviour behaviour = offlineIncomeGO.GetComponent<OfflineIncomeBehaviour>();

        behaviour.time = offlineTime;
        behaviour.income = income;
        behaviour.confirmCallback =
            (bool confirm) =>
            {
                // If user wants to watch a video and income != null
                if (confirm && behaviour.income != null)
                {
                    // Show video to double rocks
                    ShowRewardedVideoAd(() =>
                    {
                        // Grant the rocks again
                        Database.instance.activeLocation.AddRocks(behaviour.income[Mineral.ROCK]);
                        Database.instance.doubleOfflineWithAd++;

                        // Double the rocks so the pop-up shows the doubled amount
                        behaviour.income[Mineral.ROCK] *= 2;

                        // Show the earned resources, whether video was success or not
                        ShowResourcesEarned(ConvertListOfIncome(behaviour.income));
                    });
                }

                // Else just do nothing cuz the income is already granted
            };

        return offlineIncomeGO;
    }

    private Dictionary<Mineral, double> CalcOfflineIncome(float offlineTime)
    {
        Dictionary<Mineral, double> resources = new Dictionary<Mineral, double>();
        float unpaidTime = offlineTime;
        double rocks = 0;

        // Check if ad boost is active
        if (unpaidTime > 0 && Database.instance.activeLocation.data.adBoostTime > 0)
        {
            if (Database.instance.activeLocation.data.adBoostTime > unpaidTime)
            {
                // If ad boost boost is longer than the offline time, then you can just calculate the offline income,
                // because the income already applies the ad boost
                rocks += CalcOfflineIncomeAndUpdateTimeLefts(unpaidTime);

                Database.instance.activeLocation.data.adBoostTime -= unpaidTime;
                unpaidTime = 0;
            }
            else
            {
                // If the ad boost did not last for the entire offline time, then first calculate the income for the time while
                // the ad boost was active
                rocks += CalcOfflineIncomeAndUpdateTimeLefts(Database.instance.activeLocation.data.adBoostTime);

                unpaidTime -= Database.instance.activeLocation.data.adBoostTime;
                Database.instance.activeLocation.data.adBoostTime = 0;

                // Then update the collectors so that the income does no longer include the ad boost
                UpdateIncomes();
            }
        }

        if (unpaidTime > 0)
        {
            // If there is still some time left outside of the ad boost, calc it now
            // the collectors do not have the ad boost applied, even if the first part of the calculations did
            rocks += CalcOfflineIncomeAndUpdateTimeLefts(unpaidTime);
        }

        // Always add rocks
        resources.Add(Mineral.ROCK, rocks);

        /*
         * Currently, earning gems from offline income is turned off and replaced with daily login rewards
         * 
        // If no rocks are earned you also cant earn gems
        if (rocks > 0)
        {
            int gems = 0;

            // Grant a gem for each x amount of hours of offline time
            if (offlineTime >= GEMS_PER_OFFLINE_SECONDS)
            {
                int gemsToGrant = Math.Min(MAX_GEMS_PER_OFFLINE_SESSION, (int)Math.Floor(offlineTime / GEMS_PER_OFFLINE_SECONDS));

                for (int i = 0; i < gemsToGrant; i++)
                {
                    AddRandomMineral(resources);
                }

                gems += gemsToGrant;
            }

            // For the remaining time, grant a chance of an extra gem
            if (gems < MAX_GEMS_PER_OFFLINE_SESSION && offlineTime > SHOW_OFFLINE_INCOME_AFTER_SECONDS)
            {
                if (UnityEngine.Random.Range(0, GEMS_PER_OFFLINE_SECONDS) <= offlineTime % GEMS_PER_OFFLINE_SECONDS)
                {
                    AddRandomMineral(resources);
                }
            }
        }
        */

        return resources;
    }

    private double CalcOfflineIncomeAndUpdateTimeLefts(float time)
    {
        double income = 0;

        foreach (CollectorBehaviour collector in collectors)
        {
            income += collector.CalcOfflineIncome(time);
        }

        return income / Database.instance.activeLocation.GetProfitBoosterBonusMultiplier();
    }

    private void AddRandomMineral(Dictionary<Mineral, double> dict)
    {
        Mineral random = RandomMineral(.7f, .25f, .03f, .02f);

        if (dict.ContainsKey(random))
        {
            dict[random]++;
        }
        else
        {
            dict.Add(random, 1);
        }
    }

    public static List<GrantedResource> ConvertListOfIncome(Dictionary<Mineral, double> resources)
    {
        List<GrantedResource> granted = new List<GrantedResource>();

        foreach (KeyValuePair<Mineral, double> pair in resources)
        {
            switch (pair.Key)
            {
                case Mineral.ROCK:
                    granted.Add(new GrantedResource(Grant.ROCKS, pair.Value));
                    break;
                case Mineral.SAPPHIRE:
                    granted.Add(new GrantedResource(Grant.SAPPHIRES, pair.Value));
                    break;
                case Mineral.EMERALD:
                    granted.Add(new GrantedResource(Grant.EMERALDS, pair.Value));
                    break;
                case Mineral.RUBY:
                    granted.Add(new GrantedResource(Grant.RUBIES, pair.Value));
                    break;
                case Mineral.DIAMOND:
                    granted.Add(new GrantedResource(Grant.DIAMONDS, pair.Value));
                    break;
                case Mineral.PRESTIGE_POINT:
                    granted.Add(new GrantedResource(Grant.PRESTIGE_POINTS, pair.Value));
                    break;
            }
        }

        return granted;
    }

    private void GrantOfflineIncome(Dictionary<Mineral, double> resources)
    {
        foreach (KeyValuePair<Mineral, double> pair in resources)
        {
            Database.instance.AddMineral(pair.Key, pair.Value);
        }
    }

    private void StartCollecting()
    {
        foreach (CollectorBehaviour collector in collectors)
        {
            collector.StartCollecting();
        }
    }

    private void StopCollecting()
    {
        foreach (CollectorBehaviour collector in collectors)
        {
            collector.StopCollecting();
        }
    }

    private void SaveProgress()
    {
        // Store the time left for each collector
        foreach (CollectorBehaviour col in collectors)
        {
            col.StoreTimeLeft();
        }

        // Store the time since last online (now)
        Database.instance.activeLocation.data.lastOnlineTime = TimeManager.instance.Time();

        // Save the game by writing the stored data to the game data file
        Database.instance.SaveGame();
    }

    public static Mineral RandomMineral(float sapphireChance, float emeraldChance, float rubyChance, float diamondChance)
    {
        // Roll for a random mineral
        float roll = UnityEngine.Random.Range(0f, sapphireChance + emeraldChance + rubyChance + diamondChance);

        if (roll < sapphireChance)
        {
            return Mineral.SAPPHIRE;
        }
        else if (roll < sapphireChance + emeraldChance)
        {
            return Mineral.EMERALD;
        }
        else if (roll < sapphireChance + emeraldChance + rubyChance)
        {
            return Mineral.RUBY;
        }
        else
        {
            return Mineral.DIAMOND;
        }
    }

    public GameObject ShowTutorial(Tutorial tutorial)
    {
        GameObject tutorialGO = Instantiate(tutorialPrefab, transform);
        tutorialGO.GetComponent<TutorialBehaviour>().tutorial = tutorial;

        return tutorialGO;
    }

    public bool IsTutorialShowing()
    {
        return transform.Find(tutorialPrefab.name + GO_CLONE) != null;
    }

    public void RequestFeedback()
    {
        // If user has already rated the game, dont bother him with anything
        if (PlayerPrefs.GetInt(PLAYER_PREFS_IS_RATED) == 1)
        {
            return;
        }

        // Check if user told us he has enjoyed the game before
        if (PlayerPrefs.GetInt(PLAYER_PREFS_IS_ENJOYED) == 1)
        {
            // User has already told us he enjoys the game, immediately ask him to rate
            AskToRate();
        }
        else
        {
            // Else, ask if he enjoys the game before asking him to rate it
            ShowPopUp(askEnjoyedPopUpPrefab, false, (bool liked) =>
            {
                if (liked)
                {
                    // Store that user enjoys the game
                    PlayerPrefs.SetInt(PLAYER_PREFS_IS_ENJOYED, 1);

                    // Ask him to rate it
                    AskToRate();
                }
            });
        }
    }

    private void AskToRate()
    {
        // Ask to rate the game
#if UNITY_IOS
        // If native iOS rate pop-up is available, show it
        if (Device.RequestStoreReview())
        {
            // There is no callback to determine if user actually rated the game,
            // to prevent any problems just never ask for a rate again
            PlayerPrefs.SetInt(PLAYER_PREFS_IS_RATED, 1);
        }
        else
        {
            // If iOS version doesn't support native rate pop-up, refer manualy to App Store review deeplink
            ReferToStore("https://itunes.apple.com/us/app/appname/id" + APPLE_APP_ID + "?action=write-review");
        }
#elif UNITY_ANDROID
        // Refer to Play Store
        ReferToStore("https://play.google.com/store/apps/details?id=" + Application.identifier);
#endif
    }

    private void ReferToStore(string link)
    {
        // Ask user to rate/review the app, if they agree, refer them to the App/Play Store
        ShowPopUp(askToRatePopUpPrefab, false, (bool confirm) =>
        {
            if (confirm)
            {
                // Store that user has rated the game
                PlayerPrefs.SetInt(PLAYER_PREFS_IS_RATED, 1);

                // Refer to App/Play Store
                Application.OpenURL(link);
            }
        });
    }

    private IEnumerator WaitToShowPrestigeTutorial()
    {
        // Wait untill first prestige is ready
        yield return new WaitUntil(PrestigeNotificationBehaviour.Notify);

        Transform quickMenu = transform.Find("UI").Find("QuickMenu");
        Transform sideMenu = transform.Find("Menu").Find("SlideOutMenu").Find("SideBar").Find("Content").Find("Buttons");

        // Tell user to prestige!
        ShowTutorial(new Tutorial(new List<TutorialStep>()
        {
            new ExplainTutorialStep(Translation_Script.TUT_PRESTIGE_INTRO_1),
            new ExplainTutorialStep(Translation_Script.TUT_PRESTIGE_INTRO_2),
            new ClickHintTutorialStep(quickMenu, () => MenuBehaviour.isSideMenuOpen, () => quickMenu.Find("Menu").Find("Button").Find("Image").position, reversed: true),
            new ClickHintTutorialStep(sideMenu, () => Database.instance.activeLocation.data.prestiges > 0, () => sideMenu.Find("Prestige").position, reversed: true),
        }));
    }

    public void Test()
    {
        Logger.LogDebug("test");
    }

    // Used to take game screenshots in editor
    /*
    private void Update()
    {
        if(Input.GetKeyDown(KeyCode.Space))
        {
            string name = "Screenshot_" + string.Format("{0:00}-{1:00}-{2:00}_{3:00}.{4:00}.{5:00}.{6:000}", DateTime.Now.Month, DateTime.Now.Day, DateTime.Now.Year, DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second, DateTime.Now.Millisecond) + ".png";
            ScreenCapture.CaptureScreenshot(Application.persistentDataPath + "/" + name);
            Debug.Log("Took a screenshot! Saved at: " + Application.persistentDataPath + "/" + name);
        }

        if(Input.GetKeyDown(KeyCode.LeftControl))
        {
            Time.timeScale = Time.timeScale == 1 ? 0 : 1;
        }
    }
    */
}