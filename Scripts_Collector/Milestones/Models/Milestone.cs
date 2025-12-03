using System.Collections.Generic;
using UnityEngine;

public enum MilestoneRewardType
{
    SPEED,
    PROFITS,
    FREE_RESOURCES
}

public class Milestone
{
    public const int TARGET_RUBIES_ID = -2;
    public const int TARGET_DIAMONDS_ID = -3;

    public static int GENERATED_ID = 1;

    public int ID { get; private set; }

    // Target ints above 0 are linked to the Collector ID
    public int milestoneTargetId { get; private set; }
    public double milestoneGoal { get; private set; }

    // Target ints above 0 are linked to the Collector ID
    public int rewardTargetId;
    public MilestoneRewardType rewardType;
    public double rewardValue;

    public bool previouslyCompleted { get; private set; }
    public bool isCompleted { get; private set; }

    public Milestone(int milestoneTargetId, double milestoneGoal, int rewardTargetId, MilestoneRewardType rewardType, double rewardValue, List<Collector> collectors, LocationData data)
    {
        ID = GENERATED_ID;
        GENERATED_ID++;

        this.milestoneTargetId = milestoneTargetId;
        this.milestoneGoal = milestoneGoal;

        this.rewardTargetId = rewardTargetId;
        this.rewardType = rewardType;
        this.rewardValue = rewardValue;

        previouslyCompleted = data.milestonesPreviouslyCompleted.Contains(ID);
        // If it has a special reward and has been completed before, mark it as completed to prevent special rewards from being granted twice
        isCompleted = (previouslyCompleted && HasSpecialReward()) || IsGoalAchieved(collectors);
    }

    public Milestone(int milestoneTarget, double milestoneGoal, MilestoneRewardType rewardType, double rewardValue, List<Collector> collectors, LocationData data) :
        this(milestoneTarget, milestoneGoal, milestoneTarget, rewardType, rewardValue, collectors, data)
    {

    }

    public void CompleteAndGrantReward()
    {
        if(HasSpecialReward() && !previouslyCompleted)
        {
            GrantSpecialReward();
        }

        isCompleted = true;
        previouslyCompleted = true;

        if (!Database.instance.activeLocation.data.milestonesPreviouslyCompleted.Contains(ID))
        {
            Database.instance.activeLocation.data.milestonesPreviouslyCompleted.Add(ID);
        }
    }

    public bool IsGoalAchieved()
    {
        return IsGoalAchieved(Database.instance.activeLocation.collectors);
    }

    public bool IsGoalAchieved(List<Collector> collectors)
    {
        return IsGoalAchieved(GetCurrentAmount(collectors));
    }

    public bool IsGoalAchieved(double value)
    {
        return value >= milestoneGoal;
    }

    public bool HasSpecialReward()
    {
        return rewardTargetId == TARGET_RUBIES_ID || rewardTargetId == TARGET_DIAMONDS_ID;
    }

    public Sprite GetMilestoneTargetSprite()
    {
        return Database.instance.activeLocation.GetSpriteForCollector(milestoneTargetId);
    }

    public Sprite GetMilestoneRewardTargetSprite()
    {
        if (rewardTargetId >= -1)
        {
            return Database.instance.activeLocation.GetSpriteForCollector(rewardTargetId);
        }
        else
        {
            switch (rewardTargetId)
            {
                case TARGET_RUBIES_ID:
                    return Game.GetSpriteForMineral(Mineral.RUBY);
                case TARGET_DIAMONDS_ID:
                    return Game.GetSpriteForMineral(Mineral.DIAMOND);
            }
        }

        // Return this by default
        return Database.instance.activeLocation.GetSpriteForCollector(Collector.TARGET_ALL_ID);
    }

    public string GetFormattedRewardString(bool shortName)
    {
        switch (rewardType)
        {
            case MilestoneRewardType.PROFITS:
                return GetNameForRewardTarget(shortName) + " " + Translator.GetTranslationForId(Translation_Script.PROFITS) + " x" + rewardValue;
            case MilestoneRewardType.SPEED:
                return GetNameForRewardTarget(shortName) + " " + Translator.GetTranslationForId(Translation_Script.SPEED) + " x" + rewardValue;
            case MilestoneRewardType.FREE_RESOURCES:
                return "+" + rewardValue + " " + GetNameForRewardTarget(shortName);
            default:
                return "???";
        }
    }

    public string GetNameForRewardTarget(bool shortName)
    {
        if(rewardTargetId > 0)
        {
            return Database.instance.activeLocation.GetCollectorForId(rewardTargetId).GetName(shortName);
        }
        else
        {
            switch (rewardTargetId)
            {
                case Collector.TARGET_ALL_ID:
                    return Translator.GetTranslationForId(Translation_Script.ALL);
                case TARGET_RUBIES_ID:
                    return Translator.GetTranslationForId(Translation_Inspector.RUBIES);
                case TARGET_DIAMONDS_ID:
                    return Translator.GetTranslationForId(Translation_Inspector.DIAMONDS);
            }
        }

        return "???";
    }

    public string GetFormattedProgressString()
    {
        return GetCurrentAmount() + "/" + milestoneGoal;
    }

    public float GetProgressToCompletionValue()
    {
        Milestone previousMilestone = Database.instance.activeLocation.GetPreviousMilestone(this);
        double previousGoal = previousMilestone != null ? previousMilestone.milestoneGoal : 0;

        return (float)((GetCurrentAmount() - previousGoal) / (milestoneGoal - previousGoal));
    }

    public void Reset()
    {
        // Only reset if it doesn't have a special reward, this prevents special rewards from being granted twice
        isCompleted = previouslyCompleted && HasSpecialReward();
    }

    private void GrantSpecialReward()
    {
        switch(rewardTargetId)
        {
            case TARGET_RUBIES_ID:
                Database.instance.AddRubies(rewardValue);
                break;
            case TARGET_DIAMONDS_ID:
                Database.instance.AddDiamonds(rewardValue);
                break;
        }
    }

    private double GetCurrentAmount()
    {
        return GetCurrentAmount(Database.instance.activeLocation.collectors);
    }

    private double GetCurrentAmount(List<Collector> collectors)
    {
        if (milestoneTargetId == Collector.TARGET_ALL_ID)
        {
            // Get the collector amount which is lowest of all
            List<double> amounts = new List<double>();

            foreach (Collector collector in collectors)
            {
                amounts.Add(collector.amountTotal);
            }

            double lowest = amounts.Count > 0 ? amounts[0] : 0;

            foreach (double amount in amounts)
            {
                if (amount < lowest)
                {
                    lowest = amount;
                }
            }

            // Check if that is more than the goal
            return lowest;
        }
        else
        {
            // Then its a collector, collector ID - 1 is index in the collector array
            return collectors[milestoneTargetId - 1].amountTotal;
        }
    }
}