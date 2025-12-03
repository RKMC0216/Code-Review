using System.Collections.Generic;
using UnityEngine;
using System;

public class UseDiamondsBehaviour : GenericMenuItem
{
    [SerializeField]
    private GameObject boostCollectorPrefab;

    [SerializeField]
    private RectTransform collectorsContainer;

    private List<BoostCollectorBehaviour> boostBehaviours = new List<BoostCollectorBehaviour>();
    private int index = 0;

    private void Start()
    {
        // This will show the active click boost
        CreateCollector(null, null);

        foreach(CollectorBehaviour collector in menu.game.collectors)
        {
            CreateCollector(collector, BoostBoughtCallback);
        }
    }

    public override void OnOpened(int fragmentIndex)
    {
        // If still on earth and never used a diamond before, explain what they do
        if (Database.instance.activeLocation.metaData.ID == Locations.EARTH &&
            Database.instance.activeLocation.data.diamondsSpent == 0)
        {
            menu.game.ShowTutorial(new Tutorial(new List<TutorialStep>()
            {
                new ExplainTutorialStep(Translation_Script.TUT_DIAMONDS_1),
                new ExplainTutorialStep(Translation_Script.TUT_DIAMONDS_2),
            }));
        }
    }

    private void CreateCollector(CollectorBehaviour collector, Action<int> callback)
    {
        GameObject colBoost = Instantiate(boostCollectorPrefab, collectorsContainer);
        boostBehaviours.Add(colBoost.GetComponent<BoostCollectorBehaviour>());
        boostBehaviours[index].collector = collector;
        boostBehaviours[index].evenIndex = index % 2 == 0;
        boostBehaviours[index].boostBoughtCallback = BoostBoughtCallback;
        index++;
    }

    private void BoostBoughtCallback(int collectorID)
    {
        int oldLow = Database.instance.activeLocation.GetLowestDiamondBoostStage();

        // Increase stage for that collector and update its income
        IncreaseStageForCollector(collectorID);

        menu.game.collectors[collectorID - 1].UpdateTotalIncome();
        menu.game.collectors[collectorID - 1].UpdateActiveDiamondBoostVisual();

        // Check if all boosts for this stage have been bought
        if(Database.instance.activeLocation.GetLowestDiamondBoostStage() > oldLow)
        {
            // Check if there's a next stage and if it is free
            if(!Database.instance.activeLocation.IsDiamondBoostsMaxed() && BoostCollectorBehaviour.GetPriceForAvailableStage() == 0)
            {
                // Grant this stage because its free
                foreach(CollectorBehaviour collector in menu.game.collectors)
                {
                    IncreaseStageForCollector(collector.collector.ID);
                    collector.UpdateActiveDiamondBoostVisual();
                }

                // Update the income for all collectors
                menu.game.UpdateIncomes();

                if(Database.instance.activeLocation.metaData.ID == Locations.EARTH)
                {
                    // Show tutorial telling that he got free x17 and also boosts boulder clicks
                    menu.game.ShowTutorial(new Tutorial(new List<TutorialStep>()
                    {
                        new ExplainTutorialStep(Translation_Script.TUT_FREE_DIAMOND_STAGE_1),
                        new ExplainTutorialStep(Translation_Script.TUT_FREE_DIAMOND_STAGE_2),
                        new ExplainTutorialStep(Translation_Script.TUT_FREE_DIAMOND_STAGE_3),
                    }));
                }
            }
        }

        // Update the available boost and button price/interactable
        foreach(BoostCollectorBehaviour boostBehaviour in boostBehaviours)
        {
            boostBehaviour.UpdateBoostAvailability();
        }
    }

    private void IncreaseStageForCollector(int ID)
    {
        // Increase the stage of the collector
        if (Database.instance.activeLocation.data.diamondBoostsStages.ContainsKey(ID))
        {
            Database.instance.activeLocation.data.diamondBoostsStages[ID]++;
        }
        else
        {
            Database.instance.activeLocation.data.diamondBoostsStages.Add(ID, 0);
        }
    }
}