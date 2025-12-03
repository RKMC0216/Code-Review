using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class BoostCollectorBehaviour : MonoBehaviour
{
    public static readonly List<double> boosts = new List<double> { 7, 17, 77, 777, 7777 };
    public static readonly List<double> prices = new List<double> { 10, 0, 10, 30, 50 };

    [SerializeField]
    private GameObject boostPrefab;

    [SerializeField]
    private RectTransform boostsContainer;

    [SerializeField]
    private TMP_Text priceTxt;

    [SerializeField]
    private Image backgroundImg, collectorImg, priceImg;

    [SerializeField]
    private Button buyButton;   

    private List<DiamondBoostBehaviour> boostBehaviours = new List<DiamondBoostBehaviour>();

    [HideInInspector]
    public CollectorBehaviour collector;
    [HideInInspector]
    public bool evenIndex;

    public Action<int> boostBoughtCallback;

    private void Start()
    {
        backgroundImg.color = evenIndex ? new Color(.94f, .94f, .94f) : new Color(1, 1, 1);
        collectorImg.sprite = Database.instance.activeLocation.GetSpriteForCollector(collector != null ? collector.collector.ID : Collector.TARGET_CLICK_ID);

        for (int i = 0; i < boosts.Count; i++)
        {
            GameObject boost = Instantiate(boostPrefab, boostsContainer);
            boostBehaviours.Add(boost.GetComponent<DiamondBoostBehaviour>());
            boostBehaviours[i].stage = i;
        }

        UpdateBoostAvailability();
    }

    public void OnBuyButtonClicked()
    {
        if(!IsMaxed() && HasSufficientDiamonds())
        {
            Database.instance.SubstractDiamonds(GetPriceForAvailableStage(), true);
            boostBoughtCallback?.Invoke(collector.collector.ID);
        }
    }

    public void UpdateBoostAvailability()
    {
        for (int i = 0; i < boosts.Count; i++)
        {
            if(collector == null)
            {
                boostBehaviours[i].UpdateAvailability(
                i <= Database.instance.activeLocation.GetLowestDiamondBoostStage(),
                i <= Database.instance.activeLocation.GetDiamondBoostStageFor(Collector.TARGET_CLICK_ID),
                i == Database.instance.activeLocation.GetDiamondBoostStageFor(Collector.TARGET_CLICK_ID));
            }
            else
            {
                boostBehaviours[i].UpdateAvailability(
                i <= Database.instance.activeLocation.GetLowestDiamondBoostStage() + 1,
                i <= Database.instance.activeLocation.GetDiamondBoostStageFor(collector.collector.ID),
                i == Database.instance.activeLocation.GetDiamondBoostStageFor(collector.collector.ID));
            }
        }

        if(collector == null)
        {
            buyButton.gameObject.SetActive(false);
        }
        else
        {
            if (IsMaxed())
            {
                priceTxt.text = Translator.GetTranslationForId(Translation_Inspector.MAX).ToUpper();
                buyButton.interactable = false;
                priceImg.gameObject.SetActive(false);
            }
            else
            {
                priceTxt.text = GetPriceForAvailableStage().ToString();
                buyButton.interactable = IsAllowedToPurchase() && HasSufficientDiamonds();
            }
        }
    }

    public bool HasSufficientDiamondsForBoost()
    {
        return Database.instance.Diamonds >= GetPriceForAvailableStage();
    }

    public static double GetPriceForAvailableStage()
    {
        return prices[Database.instance.activeLocation.GetLowestDiamondBoostStage() + 1];
    }

    private bool IsMaxed()
    {
        if(collector == null)
        {
            return false;
        }

        return Database.instance.activeLocation.GetDiamondBoostStageFor(collector.collector.ID) >= boosts.Count - 1;
    }

    private bool IsAllowedToPurchase()
    {
        if(collector == null)
        {
            return false;
        }

        return Database.instance.activeLocation.GetDiamondBoostStageFor(collector.collector.ID) <= Database.instance.activeLocation.GetLowestDiamondBoostStage();
    }

    private bool HasSufficientDiamonds()
    {
        return Database.instance.Diamonds >= GetPriceForAvailableStage();
    }
}