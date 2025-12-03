using System.Collections.Generic;
using System.Data.Common;
using UnityEngine;
using UnityEngine.UI;

public class ExpandBehaviour : GenericMenuItem
{
    public const int UNLOCK_AT_AMOUNT_OF_DISSOLVERS = 1;

    [SerializeField]
    private GameObject expansionPrefab, lockedInfo;

    [SerializeField]
    private RectTransform content;

    private Dictionary<int, ExpansionBehaviour> expansions = new Dictionary<int, ExpansionBehaviour>();

    private void Start()
    {
        // Apply safe area to the bottom padding of the scrollview's content
        content.GetComponent<VerticalLayoutGroup>().padding.bottom += (int) SafeAreaHelper.PaddingBottom(transform.root.GetComponent<RectTransform>());

        if(IsExpandUnlocked())
        {
            // Create a list of all locations, excluding the currently opened location
            foreach (LocationMetaData location in Locations.metaDatas)
            {
                GameObject expansion = Instantiate(expansionPrefab, content);
                expansion.GetComponent<ExpansionBehaviour>().location = location;
                expansions.Add(location.ID, expansion.GetComponent<ExpansionBehaviour>());
            }
        }
        else
        {
            // Show locked
            lockedInfo.SetActive(true);
        }
    }

    public override void OnOpened(int fragmentIndex)
    { 
        if(IsExpandUnlocked())
        {
            if(Database.instance.activeLocation.metaData.ID == Locations.EARTH && Database.instance.locationDatas.Count == 1)
            {
                if(Database.instance.GetMineralAmount(Locations.GetMetaDataForLocation(Locations.MOON).priceType) >= Locations.GetMetaDataForLocation(Locations.MOON).priceValue)
                {
                    // Tell him to expand to the moon
                    menu.game.ShowTutorial(new Tutorial(new List<TutorialStep>()
                    {
                        new ExplainTutorialStep(Translation_Script.TUT_EXPAND_MOON_1),
                        new ExplainTutorialStep(Translation_Script.TUT_EXPAND_MOON_2),
                        new ExplainTutorialStep(Translation_Script.TUT_EXPAND_MOON_3),
                        new ExplainTutorialStep(Translation_Script.TUT_EXPAND_MOON_4),
                        new ClickHintTutorialStep(content, () => this == null || Database.instance.locationDatas.ContainsKey(Locations.MOON), () => expansions[Locations.MOON].lockedBtn.transform.position),
                    }));
                }
                else
                {
                    // Tell him to earn more rocks to expand to the moon
                    menu.game.ShowTutorial(new Tutorial(new List<TutorialStep>()
                    {
                        new ExplainTutorialStep(Translation_Script.TUT_EXPAND_UNSUFFICIENT_1),
                        new ExplainTutorialStep(Translation_Script.TUT_EXPAND_UNSUFFICIENT_2),
                    }));
                }
            }
        }
        else
        {
            // Tell him to earn more rocks to unlock this feature
            menu.game.ShowTutorial(new Tutorial(new List<TutorialStep>()
            {
                new ExplainTutorialStep(Translation_Script.TUT_EXPAND_LOCKED),
            }));
        }
    }

    public static bool IsExpandUnlocked()
    {
        // Unlocked when either not on earth, already owns more than 1 location, has prestiged 2 or more times, or has prestiged once and owns at least 1 planet dissolver
        return Database.instance.activeLocation.metaData.ID != Locations.EARTH || Database.instance.locationDatas.Count > 1 ||
            Database.instance.activeLocation.data.prestiges >= 2 || (Database.instance.activeLocation.data.prestiges == 1 && Database.instance.activeLocation.GetCollectorForId(10).amountTotal >= UNLOCK_AT_AMOUNT_OF_DISSOLVERS);
    }
}