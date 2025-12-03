using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class ShopBehaviour : GenericMenuItem
{
    [SerializeField]
    private GameObject specialPackPrefab, multiplierPrefab, timeWarpPrefab;

    [SerializeField]
    private RectTransform specialPackContainer, multipliersContainer, rubiesContainer, timeWarpsContainer;

    [SerializeField]
    private TMP_Text multiplierTxt, removeAdsTxt;

    [SerializeField]
    private GameObject adsRemovedCheckObj;

    [SerializeField]
    private Animator multiplierAnimator;

    private List<TimeWarpBehaviour> timeWarpBehaviours = new List<TimeWarpBehaviour>();

    private void Start()
    {
        SetMultiplierText(false);

        LoadSpecialPack();
        LoadTimeWarps();
        LoadMultipliers();

        UpdateRemoveAdsText();
    }

    public void UpdateRemoveAdsText()
    {
        // Check if user has a low enough IAP score to show interstitials
        if (Database.instance.IAPScore < 5)
        {
            removeAdsTxt.text = (Database.instance.IAPScore == 0 ? Translator.GetTranslationForId(Translation_Script.REMOVE_ADS_PREFIX_NO_PURCHASES) : Translator.GetTranslationForId(Translation_Script.REMOVE_ADS_PREFIX_SOME_PURCHASES))
                + " " + (50 - (Database.instance.IAPScore * 10)) + " " + Translator.GetTranslationForId(Translation_Script.REMOVE_ADS_SUFFIX);
        }
        else
        {
            removeAdsTxt.gameObject.SetActive(false);
            adsRemovedCheckObj.SetActive(true);
        }
    }

    public void OnRubiesBoughtCallback()
    {
        StartCoroutine(DelayedCallback());
    }

    private IEnumerator DelayedCallback()
    {
        // Wait a frame to make sure the IAP score is increased
        yield return null;

        UpdateRemoveAdsText();
    }

    private void LoadSpecialPack()
    {
        // Load special pack IAP item
        GameObject specialPack = Instantiate(specialPackPrefab, specialPackContainer);
        specialPack.GetComponent<SpecialPackBehaviour>().destroyWhenBought = true;
        specialPack.GetComponent<SpecialPackBehaviour>().boughtCallback = () =>
        {
            // The old special pack is destroyed and new one is generated, load it
            LoadSpecialPack();

            // Oh, and update the multiplier count in case the pack contained a multiplier
            SetMultiplierText(false);

            // And update the time warps in case the pack contained a time warp
            UpdateTimeWarps();

            // And update the remove ads info
            UpdateRemoveAdsText();
        };
        specialPack.GetComponent<SpecialPackBehaviour>().offerExpiredCallback = () => 
        {
            // A new special pack is automatically generated, just need to load it
            LoadSpecialPack();
        };
    }

    private void SetMultiplierText(bool animate)
    {
        multiplierTxt.text = "+" + NumberFormatter.Format((Database.instance.activeLocation.data.multiplier > 1 ? Database.instance.activeLocation.data.multiplier : 0) * 100) + "%";

        if(animate)
        {
            multiplierAnimator.Play("MultiplierBought");
        }
    }

    private void LoadTimeWarps()
    {
        // Load time warps
        CreateTimeWarp(Item.TIME_WARP_24H, 10);
        CreateTimeWarp(Item.TIME_WARP_7D, 25);
        CreateTimeWarp(Item.TIME_WARP_14D, 40);
        CreateTimeWarp(Item.TIME_WARP_30D, 65);
    }

    private void CreateTimeWarp(Item timeWarp, double price)
    {
        GameObject twGO = Instantiate(timeWarpPrefab, timeWarpsContainer);
        twGO.GetComponent<TimeWarpBehaviour>().timeWarp = timeWarp;
        twGO.GetComponent<TimeWarpBehaviour>().price = price;
        timeWarpBehaviours.Add(twGO.GetComponent<TimeWarpBehaviour>());
    }

    private void UpdateTimeWarps()
    {
        foreach(TimeWarpBehaviour tw in timeWarpBehaviours)
        {
            tw.UpdateInfo();
        }
    }

    private void LoadMultipliers()
    {
        // Load multipliers
        CreateMultiplier(3, 20);
        CreateMultiplier(7, 35);
        CreateMultiplier(12, 50);
        CreateMultiplier(27, 100);
    }

    private void CreateMultiplier(double multiplier, double price)
    {
        GameObject multiGO = Instantiate(multiplierPrefab, multipliersContainer);
        multiGO.GetComponent<MultiplierBehaviour>().multiplier = multiplier;
        multiGO.GetComponent<MultiplierBehaviour>().price = price;
        multiGO.GetComponent<MultiplierBehaviour>().boughtCallback = () =>
        {
            menu.game.UpdateIncomes();
            SetMultiplierText(true);
            UpdateTimeWarps();
        };
    }
}