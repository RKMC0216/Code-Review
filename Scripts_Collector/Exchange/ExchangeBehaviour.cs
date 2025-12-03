using System.Collections.Generic;
using UnityEngine;
using System;

public class ExchangeBehaviour : GenericMenuItem
{
    public const double MAX_DIAMOND_PRICE = 1E+300;

    [SerializeField]
    private GameObject tradePairPrefab;

    private void Start()
    {
        // Load trade pairs
        CreateTradePair(new TradePair(Mineral.ROCK, GetNextDiamondPrice,
                        Mineral.DIAMOND, 1, () => Database.instance.activeLocation.data.rocksForDiamondsTradesDone++));
        CreateTradePair(new TradePair(Mineral.RUBY, 2, Mineral.DIAMOND, 1));
        CreateTradePair(new TradePair(Mineral.RUBY, 1, Mineral.EMERALD, 2));
        CreateTradePair(new TradePair(Mineral.RUBY, 1, Mineral.SAPPHIRE, 3));

        // Check if location = Earth, never traded rocks for a diamond before and if first diamond can be traded for rocks
        if(Database.instance.activeLocation.metaData.ID == Locations.EARTH && Database.instance.activeLocation.data.rocksForDiamondsTradesDone == 0 && ExchangeNotificationBehaviour.Notify())
        {
            menu.game.ShowTutorial(new Tutorial(new List<TutorialStep>()
            {
                new ExplainTutorialStep(Translation_Script.TUT_EXCHANGE_1),
                new ExplainTutorialStep(Translation_Script.TUT_EXCHANGE_2),
            }));
        }
    }

    private void CreateTradePair(TradePair tradePair)
    {
        GameObject pair = Instantiate(tradePairPrefab, transform);
        pair.GetComponent<TradePairBehaviour>().tradePair = tradePair;
    }

    public static double GetNextDiamondPrice()
    {
        // First trade costs 1E+33 and increases in prices with a factor of 1000 every trade
        return Math.Pow(10, 33 + (3 * Database.instance.activeLocation.data.rocksForDiamondsTradesDone));
    }
}