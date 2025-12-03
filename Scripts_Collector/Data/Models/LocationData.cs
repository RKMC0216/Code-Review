using System.Collections.Generic;
using System;

[Serializable]
public class LocationData
{
    // --- Resetable properties ---
    public double rocks;

    public double sessionRocks;
    public double sessionSapphires;
    public double sessionEmeralds;
    public double sessionRubies;
    public double sessionDiamonds;

    public double sessionBoulderClicks;
    public double sessionBoulderClickEarnings;
    public double sessionBouldersCrushed;

    public double sessionPrestigePointsSacrificed;

    public Dictionary<int, float> collectorTimeLeft = new Dictionary<int, float>();
    public Dictionary<int, double> collectorsBought = new Dictionary<int, double>();
    public Dictionary<int, double> collectorsFree = new Dictionary<int, double>();
    public List<int> normalUpgradesBought = new List<int>();
    public List<int> prestigeUpgradesBought = new List<int>();
    // ----------------------------

    // --- Non-resetable properties ---
    public DateTime lastOnlineTime;

    public float adBoostTime;
    public Tuple<DateTime, int> adBoostsViewedToday;
    public Tuple<DateTime, int> spinsViewedToday;

    public double multiplier = 1;
    public double prestigePoints;

    public double sapphiresSpent;
    public double emeraldsSpent;
    public double rubiesSpent;
    public double diamondsSpent;

    public double oldSessionsRocks;
    public double oldSessionsSapphires;
    public double oldSessionsEmeralds;
    public double oldSessionsRubies;
    public double oldSessionsDiamonds;

    public double lifetimeRocks { get { return oldSessionsRocks + sessionRocks; } }
    public double lifetimeSapphires { get { return oldSessionsSapphires + sessionSapphires; } }
    public double lifetimeEmeralds { get { return oldSessionsEmeralds + sessionEmeralds; } }
    public double lifetimeRubies { get { return oldSessionsRubies + sessionRubies; } }
    public double lifetimeDiamonds { get { return oldSessionsDiamonds + sessionDiamonds; } }

    public double boulderHP;
    public double oldSessionsBoulderClicks;
    public double oldSessionsBoulderClickEarnings;
    public double oldSessionsBouldersCrushed;

    public double lifetimeBoulderClicks { get { return oldSessionsBoulderClicks + sessionBoulderClicks; } }
    public double lifetimeBoulderClickEarnings { get { return oldSessionsBoulderClickEarnings + sessionBoulderClickEarnings; } }
    public double lifetimeBouldersCrushed { get { return oldSessionsBouldersCrushed + sessionBouldersCrushed; } }

    public double oldSessionsPrestigePointsSacrificed;
    public double lifetimePrestigePointsSacrificed { get { return oldSessionsPrestigePointsSacrificed + sessionPrestigePointsSacrificed; } }

    public double prestiges;
    public double resets;

    public double totalGoldRocks { get { return goldRockTimeWarps + goldRockSuperTaps; } }
    public double goldRockTimeWarps;
    public double goldRockSuperTaps;
    public double rocksForDiamondsTradesDone;

    public Dictionary<int, int> diamondBoostsStages = new Dictionary<int, int>();
    public List<int> milestonesPreviouslyCompleted = new List<int>();

    public float profitBoosterBoostTime;
    public float profitBoosterRechargeTime;
    // --------------------------------

    public void Prestige(bool reset)
    {
        // Create a clean session
        oldSessionsRocks += sessionRocks;
        oldSessionsSapphires += sessionSapphires;
        oldSessionsEmeralds += sessionEmeralds;
        oldSessionsRubies += sessionRubies;
        oldSessionsDiamonds += sessionDiamonds;
        
        sessionRocks = 0;
        sessionSapphires = 0;
        sessionEmeralds = 0;
        sessionRubies = 0;
        sessionDiamonds = 0;

        oldSessionsBoulderClicks += sessionBoulderClicks;
        oldSessionsBoulderClickEarnings += sessionBoulderClickEarnings;
        oldSessionsBouldersCrushed += sessionBouldersCrushed;

        sessionBoulderClicks = 0;
        sessionBoulderClickEarnings = 0;
        sessionBouldersCrushed = 0;

        oldSessionsPrestigePointsSacrificed += sessionPrestigePointsSacrificed;
        oldSessionsPrestigePointsSacrificed = 0;

        if (reset)
        {
            // Reset everything
            rocks = 0;

            collectorTimeLeft = new Dictionary<int, float>();
            collectorsBought = new Dictionary<int, double>();
            collectorsFree = new Dictionary<int, double>();
            normalUpgradesBought = new List<int>();
            prestigeUpgradesBought = new List<int>();

            resets++;
        }

        prestiges++;
    }
}