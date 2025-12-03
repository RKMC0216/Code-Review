using System.Collections.Generic;
using UnityEngine;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;
using System;

public class Database : MonoBehaviour
{
    private const string LEGACY_RESOURCES = "GameDatalIllIlIIIlIllIIlIIllIlIIIllIlllI.dat";
    private const string LEGACY_COLLECTORS = "GameDatalIllIlIIIlIllIIlIIllIlIIlIlIIllI.dat";
    private const string LEGACY_MILESTONES = "GameDatalIllIlIIIlIllIIlIIllIlIIIllIIllI.dat";
    private const string LEGACY_UPGRADES = "GameDatalIllIlIIIlIlIIIlIIllIllIIlIIlllI.dat";
    private const string LEGACY_STATS = "GameDatalIllIlIIIlIlIIIlIIllIllIIllIlllI.dat";
    private const string LEGACY_BOOSTS = "GameDatalIllIlIIIlIllIlIIIllIlIIlIlIIllI.dat";
    private const string LEGACY_SETTINGS = "GameDatalIllIlIIIlIllIIlIIllIllIIllIlllI.dat";

    private const string SAVE_FILE = "GameData.dat";
    private const string BACK_UP_FILE = "BackUp.dat";

    private const int ID_INSTALL_DATE = 0;
    private const int ID_ACTIVE_LOCATION_ID = 1;
    private const int ID_LOCATION_DATAS = 2;

    private const int ID_SAPPHIRES = 101;
    private const int ID_EMERALDS = 102;
    private const int ID_RUBIES = 103;
    private const int ID_DIAMONDS = 104;

    private const int ID_TIME_WARPS = 201;

    private const int ID_CURRENT_SPECIAL_PACK = 301;
    private const int ID_CURRENT_LOGIN_STREAK = 302;
    private const int ID_LAST_LOGIN_REWARD_CLAIMED = 303;

    private const int ID_BOOST_WITH_AD = 1001;
    private const int ID_SPIN_WITH_AD = 1002;
    private const int ID_MULTI_POWER_WITH_AD = 1003;
    private const int ID_EXTRA_PRESTIGE_WITH_AD = 1004;
    private const int ID_DOUBLE_OFFLINE_WITH_AD = 1005;

    private const int ID_BOOST_WITH_RUBY = 1011;
    private const int ID_SPIN_WITH_RUBY = 1012;

    private const int ID_IAP_SCORE = 2001;
    private const int ID_ADS_SCORE = 2002;

    private const int ID_MUSIC_ON = 9001;
    private const int ID_SFX_ON = 9002;
    private const int ID_BATTERY_SAVER = 9999;

    public static Database instance;
    public Location activeLocation;
    public double buyAmount { get; private set; } = 1;

    // =========================================================
    // ===================== STORABLE DATA =====================
    // =========================================================
    public DateTime? installDate;
    public Dictionary<int, LocationData> locationDatas = new Dictionary<int, LocationData>();

    private double sapphires = 0;
    public double Sapphires { get => sapphires; private set => sapphires = value; }

    private double emeralds = 0;
    public double Emeralds { get => emeralds; private set => emeralds = value; }

    private double rubies = 0;
    public double Rubies { get => rubies; private set => rubies = value; }

    private double diamonds = 0;
    public double Diamonds { get => diamonds; private set => diamonds = value; }

    private Dictionary<int, int> timeWarps = new Dictionary<int, int>();
    public Dictionary<int, int> TimeWarps { get => timeWarps; private set => timeWarps = value; }

    public Tuple<string, DateTime> currentSpecialPack = null;
    [HideInInspector] public int currentLoginStreak = 0;
    public DateTime lastLoginRewardClaimed;

    [HideInInspector] public double boostWithAd = 0;
    [HideInInspector] public double spinWithAd = 0;
    [HideInInspector] public double multiPowerWithAd = 0;
    [HideInInspector] public double extraPrestigeWithAd = 0;
    [HideInInspector] public double doubleOfflineWithAd = 0;

    [HideInInspector] public double boostWithRuby = 0;
    [HideInInspector] public double spinWithRuby = 0;

    [HideInInspector] public double IAPScore = 0;
    [HideInInspector] public double adsScore = 0;

    [HideInInspector] public bool music = true;
    [HideInInspector] public bool sfx = true;
    [HideInInspector] public bool batterySaver = false;
    
    public double totalBoost { get { return boostWithAd + boostWithRuby; } }
    public double totalSpin { get { return spinWithAd + spinWithRuby; } }

    // =========================================================
    // =========================================================
    // =========================================================

    public List<IObserver> buyAmountObservers = new List<IObserver>();

    public List<IObserver> sapphiresObservers = new List<IObserver>();
    public List<IObserver> emeraldsObservers = new List<IObserver>();
    public List<IObserver> rubiesObservers = new List<IObserver>();
    public List<IObserver> diamondsObservers = new List<IObserver>();

    public bool isInitialized { get; private set; } = false;

    private void Awake()
    {
        if (instance != null)
        {
            Destroy(gameObject);
        }
        else
        {
            instance = this;
            DontDestroyOnLoad(gameObject);

            LoadGame();
        }
    }

    public void ResetBuyAmount()
    {
        buyAmount = 1;
        ObservableHelper.Notify(buyAmountObservers);
    }

    public double GetMineralAmount(Mineral mineral)
    {
        switch (mineral)
        {
            case Mineral.ROCK:
                return activeLocation.data.rocks;
            case Mineral.SAPPHIRE:
                return sapphires;
            case Mineral.EMERALD:
                return emeralds;
            case Mineral.RUBY:
                return rubies;
            case Mineral.DIAMOND:
                return diamonds;
            case Mineral.PRESTIGE_POINT:
                return activeLocation.data.prestigePoints;
            default:
                return 0;
        }
    }

    public void AddMineral(Mineral mineral, double value)
    {
        switch (mineral)
        {
            case Mineral.ROCK:
                activeLocation.AddRocks(value);
                break;
            case Mineral.SAPPHIRE:
                AddSapphires(value);
                break;
            case Mineral.EMERALD:
                AddEmeralds(value);
                break;
            case Mineral.RUBY:
                AddRubies(value);
                break;
            case Mineral.DIAMOND:
                AddDiamonds(value);
                break;
            case Mineral.PRESTIGE_POINT:
                activeLocation.AddPrestigePoints(value);
                break;
        }
    }

    public bool SubstractMineral(Mineral mineral, double value, bool playSound)
    {
        switch (mineral)
        {
            case Mineral.ROCK:
                return activeLocation.SubstractRocks(value, playSound);
            case Mineral.SAPPHIRE:
                SubstractSapphires(value, playSound);
                return true;
            case Mineral.EMERALD:
                SubstractEmeralds(value, playSound);
                return true;
            case Mineral.RUBY:
                SubstractRubies(value, playSound);
                return true;
            case Mineral.DIAMOND:
                SubstractDiamonds(value, playSound);
                return true;
            case Mineral.PRESTIGE_POINT:
                activeLocation.SubstractPrestigePoints(value, playSound);
                return true;
            default:
                return false;
        }
    }

    public void AddSapphires(double amount)
    {
        Sapphires += amount;
        activeLocation.data.sessionSapphires += amount;

        ObservableHelper.Notify(sapphiresObservers);
    }

    public void AddEmeralds(double amount)
    {
        Emeralds += amount;
        activeLocation.data.sessionEmeralds += amount;

        ObservableHelper.Notify(emeraldsObservers);
    }

    public void AddRubies(double amount)
    {
        Rubies += amount;
        activeLocation.data.sessionRubies += amount;

        ObservableHelper.Notify(rubiesObservers);
    }

    public void AddDiamonds(double amount)
    {
        Diamonds += amount;
        activeLocation.data.sessionDiamonds += amount;

        ObservableHelper.Notify(diamondsObservers);
    }

    public void SubstractSapphires(double amount, bool playSound)
    {
        Sapphires -= amount;
        activeLocation.data.sapphiresSpent += amount;

        ObservableHelper.Notify(sapphiresObservers);
        if(playSound)
            AudioManager.instance.Play(Sound.KA_CHING);
    }

    public void SubstractEmeralds(double amount, bool playSound)
    {
        Emeralds -= amount;
        activeLocation.data.emeraldsSpent += amount;

        ObservableHelper.Notify(emeraldsObservers);
        if (playSound)
            AudioManager.instance.Play(Sound.KA_CHING);
    }

    public void SubstractRubies(double amount, bool playSound)
    {
        Rubies -= amount;
        activeLocation.data.rubiesSpent += amount;

        ObservableHelper.Notify(rubiesObservers);
        if (playSound)
            AudioManager.instance.Play(Sound.KA_CHING);
    }

    public void SubstractDiamonds(double amount, bool playSound)
    {
        Diamonds -= amount;
        activeLocation.data.diamondsSpent += amount;

        ObservableHelper.Notify(diamondsObservers);
        if (playSound)
            AudioManager.instance.Play(Sound.KA_CHING);
    }

    public void AddTimeWarp(int id, int value)
    {
        if(timeWarps.ContainsKey(id))
        {
            timeWarps[id] += value;
        }
        else
        {
            timeWarps.Add(id, value);
        }
    }

    public void SubstractTimeWarp(int id)
    {
        if(timeWarps.ContainsKey(id))
        {
            timeWarps[id] = Math.Max(0, timeWarps[id] - 1);
        }
    }

    public void RotateBuyAmount()
    {
        switch (buyAmount)
        {
            case 1:
                buyAmount = 10;
                break;
            case 10:
                buyAmount = 100;
                break;
            case 100:
                buyAmount = -1;
                break;
            case -1:
                buyAmount = 1;
                break;
        }

        ObservableHelper.Notify(buyAmountObservers);
    }

    public void SaveGame()
    {
        if(!isInitialized)
        {
            return;
        }

        Dictionary<int, object> data = new Dictionary<int, object>();

        data.Add(ID_INSTALL_DATE, installDate);
        data.Add(ID_ACTIVE_LOCATION_ID, activeLocation.metaData.ID);
        data.Add(ID_LOCATION_DATAS, locationDatas);

        data.Add(ID_SAPPHIRES, sapphires);
        data.Add(ID_EMERALDS, emeralds);
        data.Add(ID_RUBIES, rubies);
        data.Add(ID_DIAMONDS, diamonds);

        data.Add(ID_TIME_WARPS, timeWarps);

        data.Add(ID_CURRENT_SPECIAL_PACK, currentSpecialPack);
        data.Add(ID_CURRENT_LOGIN_STREAK, currentLoginStreak);
        data.Add(ID_LAST_LOGIN_REWARD_CLAIMED, lastLoginRewardClaimed);

        data.Add(ID_BOOST_WITH_AD, boostWithAd);
        data.Add(ID_SPIN_WITH_AD, spinWithAd);
        data.Add(ID_MULTI_POWER_WITH_AD, multiPowerWithAd);
        data.Add(ID_EXTRA_PRESTIGE_WITH_AD, extraPrestigeWithAd);
        data.Add(ID_DOUBLE_OFFLINE_WITH_AD, doubleOfflineWithAd);

        data.Add(ID_BOOST_WITH_RUBY, boostWithRuby);
        data.Add(ID_SPIN_WITH_RUBY, spinWithRuby);

        data.Add(ID_IAP_SCORE, IAPScore);
        data.Add(ID_ADS_SCORE, adsScore);

        data.Add(ID_MUSIC_ON, music);
        data.Add(ID_SFX_ON, sfx);
        data.Add(ID_BATTERY_SAVER, batterySaver);

        try
        {
            using (FileStream fileStream = File.Open(Path.Combine(Application.persistentDataPath, SAVE_FILE), FileMode.OpenOrCreate))
            {
                new BinaryFormatter().Serialize(fileStream, data);
            }
        }
        catch(Exception e)
        {
            Debug.LogError("Failed to save game data. " + e.Message);
        }
    }

    // This function only works with the new save file format
    private void ExtractDataIntoDatabase(Dictionary<int, object> data, ref int activeLocationID)
    {
        // Fill DB with the stored data
        ExtractData(ref installDate, data, ID_INSTALL_DATE);
        ExtractData(ref activeLocationID, data, ID_ACTIVE_LOCATION_ID);
        ExtractData(ref locationDatas, data, ID_LOCATION_DATAS);

        ExtractData(ref sapphires, data, ID_SAPPHIRES);
        ExtractData(ref emeralds, data, ID_EMERALDS);
        ExtractData(ref rubies, data, ID_RUBIES);
        ExtractData(ref diamonds, data, ID_DIAMONDS);

        ExtractData(ref timeWarps, data, ID_TIME_WARPS);

        ExtractData(ref currentSpecialPack, data, ID_CURRENT_SPECIAL_PACK);
        ExtractData(ref currentLoginStreak, data, ID_CURRENT_LOGIN_STREAK);
        ExtractData(ref lastLoginRewardClaimed, data, ID_LAST_LOGIN_REWARD_CLAIMED);

        ExtractData(ref boostWithAd, data, ID_BOOST_WITH_AD);
        ExtractData(ref spinWithAd, data, ID_SPIN_WITH_AD);
        ExtractData(ref multiPowerWithAd, data, ID_MULTI_POWER_WITH_AD);
        ExtractData(ref extraPrestigeWithAd, data, ID_EXTRA_PRESTIGE_WITH_AD);
        ExtractData(ref doubleOfflineWithAd, data, ID_DOUBLE_OFFLINE_WITH_AD);

        ExtractData(ref boostWithRuby, data, ID_BOOST_WITH_RUBY);
        ExtractData(ref spinWithRuby, data, ID_SPIN_WITH_RUBY);

        ExtractData(ref IAPScore, data, ID_IAP_SCORE);
        ExtractData(ref adsScore, data, ID_ADS_SCORE);

        ExtractData(ref music, data, ID_MUSIC_ON);
        ExtractData(ref sfx, data, ID_SFX_ON);
        ExtractData(ref batterySaver, data, ID_BATTERY_SAVER);
    }

    private void LoadGame()
    {
        if(isInitialized)
        {
            // If database is already initialized, don't do it again
            return;
        }

        // Default location is earth
        int activeLocationID = Locations.EARTH;

        // Check if new saved game format is available
        if (DoesFileExist(SAVE_FILE) && DeserializeFile(SAVE_FILE) is Dictionary<int, object> data && data != null)
        {
            // Extract the save file into the database
            ExtractDataIntoDatabase(data, ref activeLocationID);

            // [Backlog] TODO: Create back-up file by copying the save file

        }
        // Check for back-up file
        else if(DoesFileExist(BACK_UP_FILE) && DeserializeFile(BACK_UP_FILE) is Dictionary<int, object> backUpData && backUpData != null)
        {
            // Extract the back-up file into the database
            ExtractDataIntoDatabase(backUpData, ref activeLocationID);
        }
        else
        {
            // Create empty data object for a new location
            LocationData locationData = new LocationData();

            // This will attempt to retrieve legacy data, if none is found, data object will stay empty
            AttemptToRetrieveLegacyData(locationData);

            // Add the new data to the datas dictionary under the default ID (EARTH)
            locationDatas.Add(activeLocationID, locationData);
        }

        // In case the user clicked a notification that was location specific to open the app, set that location as active
        if (NotificationsManager.instance?.notificationHandler.RetrieveRespondedNotificationData() is string respondedNotificationData && !string.IsNullOrEmpty(respondedNotificationData))
        {
            // Try to parse the notification intent data to a location ID, results 0 if failed
            int.TryParse(respondedNotificationData, out int respondedNotificationLocationID);

            // Make sure parsed integer is an actual owned location ID
            if (respondedNotificationLocationID > 0 && locationDatas.ContainsKey(respondedNotificationLocationID))
            {
                // Set active location to the responded location ID
                activeLocationID = respondedNotificationLocationID;
            }
        }

        // Create a location with the matching location data
        activeLocation = Locations.CreateLocationForId(activeLocationID, locationDatas[activeLocationID]);

        // Mark database initialization has completed
        isInitialized = true;
    }

    private void AttemptToRetrieveLegacyData(LocationData data)
    {
        // Check for any old save files and insert that into the database or data object
        // 1. Convert resources data
        if (DoesFileExist(LEGACY_RESOURCES) && DeserializeFile(LEGACY_RESOURCES) is Dictionary<int, object> resourcesData && resourcesData != null)
        {
            // Extract data into database/data object
            ExtractData(ref data.rocks, resourcesData, 1);
            ExtractData(ref sapphires, resourcesData, 2);
            ExtractData(ref emeralds, resourcesData, 3);
            ExtractData(ref rubies, resourcesData, 4);
            ExtractData(ref diamonds, resourcesData, 5);
            ExtractData(ref data.prestigePoints, resourcesData, 6);

            // Originally stored as int, cast to int to prevent invalid cast exceptions
            int tradeStage = 0;
            ExtractData(ref tradeStage, resourcesData, 101);
            data.rocksForDiamondsTradesDone = tradeStage;

            // Data extracted, delete old save file
            DeleteFile(LEGACY_RESOURCES);
        }

        // 2. Convert collectors data
        if (DoesFileExist(LEGACY_COLLECTORS) && DeserializeFile(LEGACY_COLLECTORS) is Dictionary<int, object> collectorsData && collectorsData != null)
        {
            // Extract data into database/data object
            for(int i = 1; i <= 10; i++)
            {
                // Originally stored as int, cast to int to prevent invalid cast exceptions
                // Index used: 1 - 10
                int amountBought = 0;
                ExtractData(ref amountBought, collectorsData, i);

                // Originally stored as int, cast to int to prevent invalid cast exceptions
                // Index used: 21 - 30
                int amountFree = 0;
                ExtractData(ref amountFree, collectorsData, i + 20);
                 
                // Originally stored as -1 if the collector wasnt collecting yet
                // Index used: 101 - 110
                float timeLeft = 0;
                ExtractData(ref timeLeft, collectorsData, i + 100);

                if(amountBought > 0)
                {
                    data.collectorsBought.Add(i, amountBought);
                }
                
                if(amountFree > 0)
                {
                    data.collectorsFree.Add(i, amountFree);
                }
                
                if(timeLeft > 0)
                {
                    data.collectorTimeLeft.Add(i, timeLeft);
                }
            }

            // Data extracted, delete old save file
            DeleteFile(LEGACY_COLLECTORS);
        }

        // 3. Convert milestones data
        if (DoesFileExist(LEGACY_MILESTONES) && DeserializeFile(LEGACY_MILESTONES) is Dictionary<int, object> milestonesData && milestonesData != null)
        {
            // Extract data into database/data object
            List<int> milestoneIDs = new List<int>();
            ExtractData(ref milestoneIDs, milestonesData, 1);

            if(milestoneIDs != null && milestoneIDs.Count > 0)
            {
                // In the old format the first 40 and the last 27 milestones are click milestones
                // The new save file format does not include click milestones, so they have to be stripped
                // The total amount of milestones is 652
                int milestoneIDOffset = 40;

                foreach(int i in milestoneIDs)
                {
                    // Ignore milestone IDs of 40 and below and above the 652 IDs after that (652 + 40)
                    if (i > milestoneIDOffset && i <= 652 + milestoneIDOffset)
                    {
                        if (i == 468 + milestoneIDOffset)
                        {
                            // The milestone at (new) ID 468 (1x Moon Teleporter) was moved to (new) ID 417 (1x Rock Cloner)
                            data.milestonesPreviouslyCompleted.Add(417);
                        }
                        else if (i >= 417 + milestoneIDOffset && i <= 467 + milestoneIDOffset)
                        {
                            // To account for the changed milestone ID above, all (new) milestone IDs between 417-467 should be incremented by 1
                            // Substract 40 to convert the IDs to the new format and add +1 to account for the change
                            data.milestonesPreviouslyCompleted.Add(i - milestoneIDOffset + 1);
                        }
                        else
                        {
                            // Substract 40 to convert the IDs to the new format
                            data.milestonesPreviouslyCompleted.Add(i - milestoneIDOffset);
                        }
                    }
                }
            }

            // Data extracted, delete old save file
            DeleteFile(LEGACY_MILESTONES);
        }

        // 4. Convert upgrades data
        if (DoesFileExist(LEGACY_UPGRADES) && DeserializeFile(LEGACY_UPGRADES) is Dictionary<int, object> upgradesData && upgradesData != null)
        {
            // Extract data into database/data object
            ExtractData(ref data.normalUpgradesBought, upgradesData, 1);
            ExtractData(ref data.prestigeUpgradesBought, upgradesData, 2);

            // Data extracted, delete old save file
            DeleteFile(LEGACY_UPGRADES);
        }

        // 5. Convert boosts data
        if (DoesFileExist(LEGACY_BOOSTS) && DeserializeFile(LEGACY_BOOSTS) is Dictionary<int, object> boostsData && boostsData != null)
        {
            // Extract data into database/data object
            // In old format multiplier starts at 0, in new format it is always at least 1
            double multiplier = 0;
            ExtractData(ref multiplier, boostsData, 3);

            // Only assign the multiplier if it is larger than 1, because else it wont affect anything
            if(multiplier > 1)
            {
                data.multiplier = multiplier;
            }

            // Convert the dictionary to the new format
            // In the old format all collectors have a key-value pair with value = -1 when no boost is bought
            Dictionary<int, int> diamondBoosts = new Dictionary<int, int>();
            ExtractData(ref diamondBoosts, boostsData, 1);

            // Old format (index/multiplier --> new index):
            // 0        =   x7      --> 0
            // ALL 0    =   x17     --> 1        
            // 1        =   x77     --> 2
            // 2        =   x777    --> 3
            // 3        =   x7777   --> 4

            // First find out what the lowest bought stage is
            int lowestStage = BoostCollectorBehaviour.boosts.Count - 1;
            foreach(KeyValuePair<int, int> pair in diamondBoosts)
            {
                if(pair.Value < lowestStage)
                {
                    lowestStage = pair.Value;
                }
            }

            foreach(KeyValuePair<int, int> pair in diamondBoosts)
            {
                // If no boost bought (value = -1), then don't add it to the new format
                if(pair.Value > -1)
                {
                    // If the lowest stage is equal to or higher than 0, it means that the x17 boost should be accounted for
                    // The x17 boost does not have an index in the old format, so we'll have to add +1 to the index for these boosts
                    data.diamondBoostsStages.Add(pair.Key, lowestStage >= 0 ? pair.Value + 1 : pair.Value);
                }
            }

            // Convert time warps to new format
            // Time warps are stored as legacy Item enums, which is basically an integer
            // So use the integer to determine what time warp it is in the new format
            // 3H & 8H never used in old version and not supported in new version, so just ignore this
            // 4 = 24H  --> 1
            // 5 = 7D   --> 2
            // 6 = 14D  --> 3
            // 7 = 30D  --> 4
            // Difference in ID = -3

            // Old format stores the time warps as Item enums, which are stored as integers
            // But, it cant be cast directly to an integer, it must be cast to a enum named Item
            // But the Item enum is already used in the new format but with different integers
            // So we can cast it to the new Item enum and then convert that to the new format
            Dictionary<Item, int> tws = new Dictionary<Item, int>();
            ExtractData(ref tws, boostsData, 2);

            // Convert the old time warps format to the new format
            if(tws != null)
            {
                foreach (KeyValuePair<Item, int> pair in tws)
                {
                    // Only convert time warps that are supported in new version of the game
                    if((int) pair.Key >= 4 && (int) pair.Key <= 7)
                    {
                        // Convert old Item enum integer to new Item enum integer by substracting 3
                        timeWarps.Add((int) pair.Key - 3, pair.Value);
                    }
                }
            }

            // Originally stored as an int, first extract it as an int to prevent invalid cast exceptions
            int adBoost = 0;
            ExtractData(ref adBoost, boostsData, 4);
            data.adBoostTime = adBoost;

            // Data extracted, delete old save file
            DeleteFile(LEGACY_BOOSTS);
        }

        // 6. Convert stats data
        if (DoesFileExist(LEGACY_STATS) && DeserializeFile(LEGACY_STATS) is Dictionary<int, object> statsData && statsData != null)
        {
            // Extract data into database/data object
            ExtractAndReformatStat(ref data.sessionRocks, ref data.oldSessionsRocks, statsData, 11, 1);
            ExtractAndReformatStat(ref data.sessionSapphires, ref data.oldSessionsSapphires, statsData, 12, 2);
            ExtractAndReformatStat(ref data.sessionEmeralds, ref data.oldSessionsEmeralds, statsData, 13, 3);
            ExtractAndReformatStat(ref data.sessionRubies, ref data.oldSessionsRubies, statsData, 14, 4);
            ExtractAndReformatStat(ref data.sessionDiamonds, ref data.oldSessionsDiamonds, statsData, 15, 5);

            ExtractAndReformatStat(ref data.sessionBoulderClicks, ref data.oldSessionsBoulderClicks, statsData, 61, 51);
            ExtractAndReformatStat(ref data.sessionBouldersCrushed, ref data.oldSessionsBouldersCrushed, statsData, 62, 52);
            ExtractAndReformatStat(ref data.sessionBoulderClickEarnings, ref data.oldSessionsBoulderClickEarnings, statsData, 63, 53);

            // This stat does not need reformatting
            ExtractData(ref data.oldSessionsPrestigePointsSacrificed, statsData, 103);

            // Originally stored as an int, first extract it as an int to prevent invalid cast exceptions
            int prestiges = 0;
            ExtractData(ref prestiges, statsData, 100);
            data.prestiges = prestiges;

            // Originally stored as an int, first extract it as an int to prevent invalid cast exceptions
            int resets = 0;
            ExtractData(ref resets, statsData, 104);
            data.resets = resets;

            // Calculate how much of each gem the user has spent
            data.sapphiresSpent = data.lifetimeSapphires - sapphires;
            data.emeraldsSpent = data.lifetimeEmeralds - emeralds;
            data.rubiesSpent = data.lifetimeRubies - rubies;
            data.diamondsSpent = data.lifetimeDiamonds - diamonds;

            // A rough conversion of the estimated amount of dollars the user has spent
            double rubiesBought = 0;
            ExtractData(ref rubiesBought, statsData, 200);
            IAPScore += rubiesBought / 10;

            // Data extracted, delete old save file
            DeleteFile(LEGACY_STATS);
        }

        // 7. Convert settings data
        if (DoesFileExist(LEGACY_SETTINGS) && DeserializeFile(LEGACY_SETTINGS) is Dictionary<int, object> settingsData && settingsData != null)
        {
            // Extract data into database/data object
            // Originally stored as an DateTime?, first extract it as an DateTime? to prevent invalid cast exceptions
            DateTime? lastOnline = null;
            ExtractData(ref lastOnline, settingsData, 1);

            if(lastOnline != null)
            {
                data.lastOnlineTime = (DateTime) lastOnline;
            }

            // In the old format the power-ups are stored in one integer, in the new format they are stored in two
            int powerUps = 0;
            ExtractData(ref powerUps, settingsData, 35);

            if(powerUps > 0)
            {
                // Based on the chances of getting each power-up (88% and 12%) divide the total over the two new variables
                data.goldRockTimeWarps = Math.Round(powerUps * .88);
                data.goldRockSuperTaps = Math.Round(powerUps * .12);
            }

            ExtractData(ref data.adBoostsViewedToday, settingsData, 21);

            // Data extracted, delete old save file
            DeleteFile(LEGACY_SETTINGS);
        }
    }

    private void ExtractAndReformatStat(ref double currentSession, ref double oldSessions, Dictionary<int, object> dataDict, int keyCurrent, int keyLifetime)
    {
        // Extract the current session's data
        ExtractData(ref currentSession, dataDict, keyCurrent);

        // Extract the lifetime data, which includes current session
        double lifetime = 0;
        ExtractData(ref lifetime, dataDict, keyLifetime);

        // To calc the previous sessions' data, simply substract the current session from the lifetime data
        oldSessions = lifetime - currentSession;
    }

    private void ExtractData<T>(ref T valueContainer, Dictionary<int, object> data, int key)
    {
        if (data.ContainsKey(key))
        {
            try
            {
                valueContainer = (T)data[key];
            }
            catch (Exception e)
            {
                // If it fails it will just keep the default value
                Debug.LogError("Error extracting data from dictionary. " + e.Message);
            }
        }
    }

    private bool DoesFileExist(string fileName)
    {
        return File.Exists(ConstructFilePath(fileName));
    }

    private Dictionary<int, object> DeserializeFile(string fileName)
    {
        using (FileStream fileStream = File.Open(ConstructFilePath(fileName), FileMode.Open))
        {
            try
            {
                return new BinaryFormatter().Deserialize(fileStream) as Dictionary<int, object>;
            }
            catch (Exception e)
            {
                // If it fails, return null
                Debug.LogError("Error deserializing data file. " + e.Message);
                return null;
            }
        }
    }

    private void DeleteFile(string fileName)
    {
        File.Delete(ConstructFilePath(fileName));
    }

    private string ConstructFilePath(string fileName)
    {
        return Path.Combine(Application.persistentDataPath, fileName);
    }
}