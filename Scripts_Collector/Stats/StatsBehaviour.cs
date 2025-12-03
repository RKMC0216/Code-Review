using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class StatsBehaviour : GenericMenuItem
{
    [SerializeField]
    private GameObject statPrefab;

    [SerializeField]
    private RectTransform statsContainer;

    [SerializeField]
    private Transform emeraldUseButton;

    private void Start()
    {
        // Apply safe area to the bottom padding of the scrollview's content
        statsContainer.GetComponent<VerticalLayoutGroup>().padding.bottom += (int) SafeAreaHelper.PaddingBottom(transform.root.GetComponent<RectTransform>());

        // Load stats
        CreateStat(new Stat(Translator.GetTranslationForId(Translation_Script.STATS_SESSION) + " " + Translator.GetTranslationForId(Translation_Inspector.ROCKS).ToLower(), 
            () => Database.instance.activeLocation.data.sessionRocks));
        CreateStat(new Stat(Translator.GetTranslationForId(Translation_Script.STATS_LIFETIME) + " " + Translator.GetTranslationForId(Translation_Inspector.ROCKS).ToLower(), 
            () => Database.instance.activeLocation.data.lifetimeRocks));

        CreateStat(new Stat(Translator.GetTranslationForId(Translation_Script.STATS_GOALS_COMPLETED), 
            MilestonesBehaviour.CalcAmountOfCompletedMilestones()));

        CreateStat(new Stat(Translator.GetTranslationForId(Translation_Script.STATS_PRESTIGES), 
            Database.instance.activeLocation.data.prestiges));
        CreateStat(new Stat(Translator.GetTranslationForId(Translation_Script.STATS_RESETS), 
            Database.instance.activeLocation.data.resets));

        CreateStat(new Stat(Translator.GetTranslationForId(Translation_Script.STATS_SESSION) + " " + Translator.GetTranslationForId(Translation_Inspector.ROCKS).ToLower() + " " + Translator.GetTranslationForId(Translation_Script.STATS_CLICKED), 
            Database.instance.activeLocation.data.sessionBoulderClicks));
        CreateStat(new Stat(Translator.GetTranslationForId(Translation_Script.STATS_LIFETIME) + " " + Translator.GetTranslationForId(Translation_Inspector.ROCKS).ToLower() + " " + Translator.GetTranslationForId(Translation_Script.STATS_CLICKED), 
            Database.instance.activeLocation.data.lifetimeBoulderClicks));

        CreateStat(new Stat(Translator.GetTranslationForId(Translation_Script.STATS_SESSION) + " " + Translator.GetTranslationForId(Translation_Script.STATS_CLICKED) + " " + Translator.GetTranslationForId(Translation_Script.STATS_EARNINGS), 
            Database.instance.activeLocation.data.sessionBoulderClickEarnings));
        CreateStat(new Stat(Translator.GetTranslationForId(Translation_Script.STATS_LIFETIME) + " " + Translator.GetTranslationForId(Translation_Script.STATS_CLICKED) + " " + Translator.GetTranslationForId(Translation_Script.STATS_EARNINGS), 
            Database.instance.activeLocation.data.lifetimeBoulderClickEarnings));

        CreateStat(new Stat(Translator.GetTranslationForId(Translation_Script.STATS_SESSION) + " " + Translator.GetTranslationForId(Translation_Inspector.ROCKS).ToLower() + " " + Translator.GetTranslationForId(Translation_Script.STATS_CRUSHED), 
            Database.instance.activeLocation.data.sessionBouldersCrushed));
        CreateStat(new Stat(Translator.GetTranslationForId(Translation_Script.STATS_LIFETIME) + " " + Translator.GetTranslationForId(Translation_Inspector.ROCKS).ToLower() + " " + Translator.GetTranslationForId(Translation_Script.STATS_CRUSHED), 
            Database.instance.activeLocation.data.lifetimeBouldersCrushed));

        CreateStat(new Stat(Translator.GetTranslationForId(Translation_Script.STATS_CURRENT) + " " + Translator.GetTranslationForId(Translation_Inspector.PRESTIGE_POINTS), 
            Database.instance.activeLocation.data.prestigePoints));
        CreateStat(new Stat(Translator.GetTranslationForId(Translation_Inspector.PRESTIGE_POINTS) + " " + Translator.GetTranslationForId(Translation_Script.STATS_SACRIFICED), 
            Database.instance.activeLocation.data.lifetimePrestigePointsSacrificed));

        CreateStat(new Stat(Translator.GetTranslationForId(Translation_Script.STATS_SESSION) + " " + Translator.GetTranslationForId(Translation_Inspector.SAPPHIRES), 
            Database.instance.activeLocation.data.sessionSapphires));
        CreateStat(new Stat(Translator.GetTranslationForId(Translation_Script.STATS_LIFETIME) + " " + Translator.GetTranslationForId(Translation_Inspector.SAPPHIRES), 
            Database.instance.activeLocation.data.lifetimeSapphires));

        CreateStat(new Stat(Translator.GetTranslationForId(Translation_Script.STATS_SESSION) + " " + Translator.GetTranslationForId(Translation_Inspector.EMERALDS), 
            Database.instance.activeLocation.data.sessionEmeralds));
        CreateStat(new Stat(Translator.GetTranslationForId(Translation_Script.STATS_LIFETIME) + " " + Translator.GetTranslationForId(Translation_Inspector.EMERALDS), 
            Database.instance.activeLocation.data.lifetimeEmeralds));

        CreateStat(new Stat(Translator.GetTranslationForId(Translation_Script.STATS_SESSION) + " " + Translator.GetTranslationForId(Translation_Inspector.RUBIES), 
            Database.instance.activeLocation.data.sessionRubies));
        CreateStat(new Stat(Translator.GetTranslationForId(Translation_Script.STATS_LIFETIME) + " " + Translator.GetTranslationForId(Translation_Inspector.RUBIES), 
            Database.instance.activeLocation.data.lifetimeRubies));

        CreateStat(new Stat(Translator.GetTranslationForId(Translation_Script.STATS_SESSION) + " " + Translator.GetTranslationForId(Translation_Inspector.DIAMONDS), 
            Database.instance.activeLocation.data.sessionDiamonds));
        CreateStat(new Stat(Translator.GetTranslationForId(Translation_Script.STATS_LIFETIME) + " " + Translator.GetTranslationForId(Translation_Inspector.DIAMONDS), 
            Database.instance.activeLocation.data.lifetimeDiamonds));
    }

    public override void OnOpened(int fragmentIndex)
    {
        // If location = Earth and user has crushed a boulder but has never spent any emeralds yet: tell him to spend his first emerald
        if (fragmentIndex == 0 && Database.instance.activeLocation.metaData.ID == Locations.EARTH &&
            Database.instance.activeLocation.data.emeraldsSpent == 0 &&
            Database.instance.activeLocation.data.sessionBouldersCrushed > 0)
        {
            menu.game.ShowTutorial(new Tutorial(new List<TutorialStep>()
            {
                new ClickHintTutorialStep(transform.Find("Resources"),
                    () => Database.instance.activeLocation.data.emeraldsSpent > 0, 
                    () => emeraldUseButton.position),
            }));
        }
    }

    private void CreateStat(Stat stat)
    {
        GameObject statGO = Instantiate(statPrefab, statsContainer);
        statGO.GetComponent<StatBehaviour>().stat = stat;
    }

    public void OnSapphiresUseButtonClicked()
    {
        menu.OnUseSapphiresButtonClicked();
    }

    public void OnEmeraldsUseButtonClicked()
    {
        menu.OnUseEmeraldsButtonClicked();
    }

    public void OnRubiesUseButtonClicked()
    {
        menu.OnShopButtonClicked();
    }

    public void OnDiamondsUseButtonClicked()
    {
        menu.OnUseDiamondsButtonClicked();
    }
}