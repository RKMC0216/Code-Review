using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System;

public class Location
{
    public LocationMetaData metaData { get; private set; }

    public List<Collector> collectors { get; private set; }
    public List<Upgrade> normalUpgrades { get; private set; }
    public List<Upgrade> prestigeUpgrades { get; private set; }
    public List<Milestone> milestones { get; private set; }

    public LocationData data { get; private set; }
    private string artsPath;

    public Func<double> CalcPrestigePointsForReset;
    public ProfitBooster profitBooster { get; private set; }

    public Location(LocationMetaData metaData, List<Collector> collectors, List<Upgrade> normalUpgrades, List<Upgrade> prestigeUpgrades, List<Milestone> milestones, LocationData data, string artsPath, Func<double> CalcPrestigePointsForReset, ProfitBooster profitBooster = null)
    {
        this.metaData = metaData;

        this.collectors = collectors;
        this.normalUpgrades = normalUpgrades;
        this.prestigeUpgrades = prestigeUpgrades;
        this.milestones = milestones;

        this.data = data;
        this.artsPath = artsPath;

        this.CalcPrestigePointsForReset = CalcPrestigePointsForReset;
        this.profitBooster = profitBooster;
    }

    public bool PrestigeAdviced()
    {
        return CalcPrestigePointsForReset() >= GetAdvicedClaimAmount();
    }

    public double GetAdvicedClaimAmount()
    {
        return data.prestigePoints >= 100 ? data.prestigePoints : 100;
    }

    public Collector GetCollectorForId(int id)
    {
        if(id > 0 && id <= collectors.Count)
        {
            return collectors[id - 1];
        }
        else
        {
            return null;
        }
    }

    public Milestone GetNextMilestoneForMilestoneId(int milestoneId)
    {
        if(milestoneId > 0 && milestoneId < milestones.Count && milestones[milestoneId - 1].milestoneTargetId == milestones[milestoneId].milestoneTargetId)
        {
            if(!milestones[milestoneId].isCompleted)
            {
                return milestones[milestoneId];
            }
            else
            {
                return GetNextMilestoneForMilestoneId(milestoneId + 1);
            }
        }
        else
        {
            return null;
        }
    }

    public void AddMultiplier(double multiplier)
    {
        // If this is the first multiplier bought, make sure that default multiplier of 1 is substracted
        // For example:
        // Default = 1 (always)
        // Buy a multiplier of 3
        // 1 + 3 = 4
        // But the total multiplier should be 3, so substract 1
        // After the first multiplier is bought the whole value can be added
        // For example:
        // Current = 3 (after buying a x3 multiplier)
        // Buy a multiplier of 7
        // 3 + 7 = 10
        data.multiplier += data.multiplier == 1 ? multiplier - 1 : multiplier;
    }

    public void AddRockFromBoulderClick(double value, bool damage)
    {
        data.sessionBoulderClicks++;
        data.sessionBoulderClickEarnings += value;
        AddRocks(value);

        if(damage)
        {
            data.boulderHP--;
        }
    }

    public void AddRocks(double value)
    {
        data.rocks += value;
        data.sessionRocks += value;
    }

    public bool SubstractRocks(double value, bool playSound)
    {
        if (value > data.rocks)
            return false;

        data.rocks -= value;

        // Fix for random bug reports about negative rocks
        if (data.rocks < 0)
            data.rocks = 0;

        if (playSound)
            AudioManager.instance.Play(Sound.KA_CHING);

        return true;
    }

    public void AddPrestigePoints(double value)
    {
        data.prestigePoints += value;
    }

    public void SubstractPrestigePoints(double value, bool playSound)
    {
        data.prestigePoints -= value;
        data.sessionPrestigePointsSacrificed += value;
        if (playSound)
            AudioManager.instance.Play(Sound.KA_CHING);
    }

    public double GetDiamondBoostMultiplierFor(int target)
    {
        return GetDiamondBoostStageFor(target) < 0 ? 1 : BoostCollectorBehaviour.boosts[GetDiamondBoostStageFor(target)];
    }

    public int GetDiamondBoostStageFor(int target)
    {
        if (target == Collector.TARGET_CLICK_ID)
        {
            return GetLowestDiamondBoostStage();
        }
        else
        {
            return data.diamondBoostsStages.ContainsKey(target) ? data.diamondBoostsStages[target] : -1;
        }
    }

    public int GetLowestDiamondBoostStage()
    {
        int lowest = BoostCollectorBehaviour.boosts.Count - 1;

        foreach (Collector collector in collectors)
        {
            if (GetDiamondBoostStageFor(collector.ID) < lowest)
            {
                lowest = GetDiamondBoostStageFor(collector.ID);
            }
        }

        return lowest;
    }

    public bool IsDiamondBoostsMaxed()
    {
        return GetLowestDiamondBoostStage() >= BoostCollectorBehaviour.boosts.Count - 1;
    }

    public double GetUpgradesBonusMultiplierFor(int target, UpgradeType upgradeType)
    {
        double multiplier = 1;

        foreach (Upgrade up in normalUpgrades)
        {
            if (up.isBought && up.upgradeType == upgradeType && (up.upgradeTargetId == target || up.upgradeTargetId == Collector.TARGET_ALL_ID))
            {
                multiplier = multiplier * up.upgradeValue;
            }
        }

        foreach (Upgrade up in prestigeUpgrades)
        {
            if (up.isBought && up.upgradeType == upgradeType && (up.upgradeTargetId == target || up.upgradeTargetId == Collector.TARGET_ALL_ID))
            {
                multiplier = multiplier * up.upgradeValue;
            }
        }

        return multiplier;
    }

    public double GetMilestonesBonusMultiplierFor(int target, MilestoneRewardType milestoneRewardType)
    {
        double multiplier = 1;

        foreach (Milestone milestone in milestones)
        {
            if (milestone.isCompleted && milestone.rewardType == milestoneRewardType && (milestone.rewardTargetId == target || milestone.rewardTargetId == Collector.TARGET_ALL_ID))
            {
                multiplier = multiplier * milestone.rewardValue;
            }
        }

        return multiplier;
    }

    public double GetPriceDiscountFor(int target)
    {
        double discount = 0;

        foreach (Upgrade up in normalUpgrades)
        {
            if (up.isBought && up.upgradeType == UpgradeType.DISCOUNT && (up.upgradeTargetId == target || up.upgradeTargetId == Collector.TARGET_ALL_ID))
            {
                if (up.upgradeValue > discount)
                {
                    discount = up.upgradeValue;
                }
            }
        }

        foreach (Upgrade up in prestigeUpgrades)
        {
            if (up.isBought && up.upgradeType == UpgradeType.DISCOUNT && (up.upgradeTargetId == target || up.upgradeTargetId == Collector.TARGET_ALL_ID))
            {
                if (up.upgradeValue > discount)
                {
                    discount = up.upgradeValue;
                }
            }
        }

        return discount;
    }

    public double GetPrestigePointEffectiveness()
    {
        double effectiveness = 0.02;

        foreach (Upgrade up in normalUpgrades)
        {
            if (up.isBought && up.upgradeType == UpgradeType.EFFECTIVENESS && up.upgradeTargetId == Upgrade.TARGET_PRESTIGE_POINTS_ID)
            {
                effectiveness += up.upgradeValue;
            }
        }

        foreach (Upgrade up in prestigeUpgrades)
        {
            if (up.isBought && up.upgradeType == UpgradeType.EFFECTIVENESS && up.upgradeTargetId == Upgrade.TARGET_PRESTIGE_POINTS_ID)
            {
                effectiveness += up.upgradeValue;
            }
        }

        return effectiveness;
    }

    public double GetPrestigePointsBonusMultiplier()
    {
        return 1 + (data.prestigePoints * GetPrestigePointEffectiveness());
    }

    public double GetProfitBoosterBonusMultiplier()
    {
        if(profitBooster != null && data.profitBoosterBoostTime > 0)
        {
            return profitBooster.profitMultiplier;
        }
        else
        {
            return 1;
        }
    }

    public double GetActiveAdBonusMultiplier()
    {
        if(data.adBoostTime > 0 )
        {
            return BoostBehaviour.AD_BOOST_MULTIPLIER;
        }
        else
        {
            return 1;
        }
    }

    public Milestone GetNextMilestoneForCollector(int id)
    {
        foreach(Milestone milestone in milestones)
        {
            if(milestone.milestoneTargetId == id && !milestone.isCompleted)
            {
                return milestone;
            }
        }

        return null;
    }

    public Milestone GetPreviousMilestone(Milestone milestone)
    {
        if(milestone != null)
        {
            // Previous milestone = current ID - 1 and -1 for the right index
            int previousMilestoneIndex = milestone.ID - 2;

            // Check if index is in range and both milestones have the same target
            if (previousMilestoneIndex >= 0 && milestones[previousMilestoneIndex].milestoneTargetId == milestone.milestoneTargetId)
            {
                return milestones[previousMilestoneIndex];
            }
        }

        return null;
    }

    public Sprite GetSpriteForCollector(int id)
    {
        // -1 = click, 0 = all, everything above 0 relates to the collector id
        return Resources.Load<Sprite>(Path.Combine("Collectors", metaData.FolderName(), id.ToString()));
    }

    public GameObject GetCollectorPrefab()
    {
        // Returns the collector's UI prefab
        return Resources.Load<GameObject>(Path.Combine("Prefabs", "Collector UI", artsPath, "Collector"));
    }

    public GameObject GetCollectorArtPrefab(string colNameShort)
    {
        // Returns the collector's art prefab
        return Resources.Load<GameObject>(Path.Combine("Prefabs", "Arts", metaData.FolderName(), colNameShort));
    }

    public void Prestige(bool reset)
    {
        if(reset)
        {
            foreach (Collector collector in collectors)
            {
                collector.Reset();
            }

            foreach (Upgrade upgrade in normalUpgrades)
            {
                upgrade.Reset();
            }

            foreach (Upgrade upgrade in prestigeUpgrades)
            {
                upgrade.Reset();
            }

            foreach (Milestone milestone in milestones)
            {
                milestone.Reset();
            }
        }

        data.Prestige(reset);
    }
}