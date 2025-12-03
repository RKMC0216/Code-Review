using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class TimeWarpBehaviour : MonoBehaviour
{
    [SerializeField]
    private GameObject priceGO, useGO, ownedGO;

    [SerializeField]
    private TMP_Text titleTxt, descriptionTxt, abbreviationTxt, priceTxt, ownedTxt;

    [SerializeField]
    private Button buyButton;

    [HideInInspector]
    public Item timeWarp;
    [HideInInspector]
    public double price;

    private void Start()
    {
        titleTxt.text = GetNameForTimeWarp(timeWarp);
        abbreviationTxt.text = GetAbbreviationForTimeWarp(timeWarp);
        priceTxt.text = price.ToString();
        UpdateInfo();
    }

    private void OnEnable()
    {
        StartCoroutine(KeepUpdatingButton());
    }

    public void UpdateInfo()
    {
        int amount = GetAmountOfTimeWarps(timeWarp);

        ownedGO.SetActive(amount > 0);
        useGO.SetActive(amount > 0);
        priceGO.SetActive(amount <= 0);

        descriptionTxt.text = Translator.GetTranslationForId(Translation_Script.SHOP_TIME_WARP_CLAIM) + " " + 
            NumberFormatter.Format(GetIncomeForTimeWarp(timeWarp, transform.root.GetComponent<Game>())) + 
            " " + Translator.GetTranslationForId(Translation_Inspector.ROCKS).ToLower() + "!";
        ownedTxt.text = Translator.GetTranslationForId(Translation_Script.SHOP_TIME_WARP_OWNED) + ": " + amount;
    }

    public void OnBuyButtonClicked()
    {
        if (GetAmountOfTimeWarps(timeWarp) > 0)
        {
            // Use
            Database.instance.SubstractTimeWarp((int)timeWarp);
            UseTimeWarp();
            UpdateInfo();
            
        }
        else if(HasSufficientFunds())
        {
            // Buy & use
            Database.instance.SubstractRubies(price, false);
            UseTimeWarp();
        }

        UpdateButton();
    }

    private void UseTimeWarp()
    {
        double income = GetIncomeForTimeWarp(timeWarp, transform.root.GetComponent<Game>());

        Database.instance.activeLocation.AddRocks(income);
        transform.root.GetComponent<Game>().ShowResourcesEarned(new List<GrantedResource> { new GrantedResource(Grant.ROCKS, income) });
    }

    private void UpdateButton()
    {
        buyButton.interactable = GetAmountOfTimeWarps(timeWarp) > 0 || HasSufficientFunds();
    }

    private IEnumerator KeepUpdatingButton()
    {
        yield return null;

        while (true)
        {
            UpdateButton();
            yield return DelayWait.oneFifthSecond;
        }
    }

    private bool HasSufficientFunds()
    {
        return Database.instance.Rubies >= price;
    }

    public static int GetAmountOfTimeWarps(Item timeWarp)
    {
        return Database.instance.TimeWarps.ContainsKey((int)timeWarp) ? Database.instance.TimeWarps[(int)timeWarp] : 0;
    }

    public static string GetNameForTimeWarp(Item timeWarp)
    {
        string name;

        switch(timeWarp)
        {
            case Item.TIME_WARP_4H:
                name = "4-" + Translator.GetTranslationForId(Translation_Script.HOUR);
                break;
            case Item.TIME_WARP_24H:
                name = "24-" + Translator.GetTranslationForId(Translation_Script.HOUR);
                break;
            case Item.TIME_WARP_7D:
                name = "7-" + Translator.GetTranslationForId(Translation_Script.DAY);
                break;
            case Item.TIME_WARP_14D:
                name = "14-" + Translator.GetTranslationForId(Translation_Script.DAY);
                break;
            case Item.TIME_WARP_30D:
                name = "30-" + Translator.GetTranslationForId(Translation_Script.DAY);
                break;
            default:
                name = "0-" + Translator.GetTranslationForId(Translation_Script.HOUR);
                break;
        }

        return name + " " + Translator.GetTranslationForId(Translation_Script.SHOP_TIME_WARP).ToLower();
    }

    public static string GetAbbreviationForTimeWarp(Item timeWarp)
    {
        switch (timeWarp)
        {
            case Item.TIME_WARP_4H:
                return "4" + Translator.GetTranslationForId(Translation_Script.HOURS_ABBREVIATION);
            case Item.TIME_WARP_24H:
                return "24" + Translator.GetTranslationForId(Translation_Script.HOURS_ABBREVIATION);
            case Item.TIME_WARP_7D:
                return "7" + Translator.GetTranslationForId(Translation_Script.DAYS_ABBREVIATION);
            case Item.TIME_WARP_14D:
                return "14" + Translator.GetTranslationForId(Translation_Script.DAYS_ABBREVIATION);
            case Item.TIME_WARP_30D:
                return "30" + Translator.GetTranslationForId(Translation_Script.DAYS_ABBREVIATION);
            default:
                return "0" + Translator.GetTranslationForId(Translation_Script.HOURS_ABBREVIATION);
        }
    }

    public static double GetIncomeForTimeWarp(Item timeWarp, Game game)
    {
        return Math.Floor(GetSecondsForTimeWarp(timeWarp) * game.CalcRawIncomePerSecond(false));
    }

    public static double GetSecondsForTimeWarp(Item timeWarp)
    {
        switch (timeWarp)
        {
            case Item.TIME_WARP_4H:
                // 4 * 60 * 60 = 14.400
                return 14400;
            case Item.TIME_WARP_24H:
                // 24 * 60 * 60 = 86.400
                return 86400;
            case Item.TIME_WARP_7D:
                // 7 * 24 * 60 * 60 = 604.800
                return 604800;
            case Item.TIME_WARP_14D:
                // 14 * 24 * 60 * 60 = 1.209.600
                return 1209600;
            case Item.TIME_WARP_30D:
                // 30 * 24 * 60 * 60 = 2.592.000
                return 2592000;
            default:
                return 0;
        }
    }
}