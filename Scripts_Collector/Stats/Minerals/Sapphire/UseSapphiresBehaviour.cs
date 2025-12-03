using System.Collections.Generic;
using UnityEngine;

public class UseSapphiresBehaviour : GenericMenuItem
{
    [SerializeField]
    private GameObject buyCollectorPrefab;

    [SerializeField]
    private RectTransform collectorsContainer;

    private void Start()
    {
        foreach(CollectorBehaviour collector in menu.game.collectors)
        {
            GameObject col = Instantiate(buyCollectorPrefab, collectorsContainer);
            col.GetComponent<BuyCollectorWithSapphireBehaviour>().collector = collector;
        }
    }

    public override void OnOpened(int fragmentIndex)
    {
        // If still on earth and never used a sapphire before, explain what they do
        if (Database.instance.activeLocation.metaData.ID == Locations.EARTH &&
            Database.instance.activeLocation.data.sapphiresSpent == 0)
        {
            menu.game.ShowTutorial(new Tutorial(new List<TutorialStep>()
            {
                new ExplainTutorialStep(Translation_Script.TUT_SAPPHIRES_1),
                new ExplainTutorialStep(Translation_Script.TUT_SAPPHIRES_2),
            }));
        }
    }
}