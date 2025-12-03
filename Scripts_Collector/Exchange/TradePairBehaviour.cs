using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class TradePairBehaviour : MonoBehaviour, IObserver
{
    [SerializeField]
    private GameObject selectAmountContainer;

    [SerializeField]
    private Image fromImg, toImg;

    [SerializeField]
    private TMP_Text fromDigitsTxt, fromSuffixTxt,
        toDigitsTxt, toSuffixText, selectedAmountTxt;

    [SerializeField]
    private Button tradeButton;

    public TradePair tradePair;

    private void Start()
    {
        CheckInteractivePriceValue();

        selectAmountContainer.SetActive(!tradePair.IsInteractivePrice());
        UpdateShownAmounts();

        fromImg.sprite = Game.GetSpriteForMineral(tradePair.fromMineral);
        toImg.sprite = Game.GetSpriteForMineral(tradePair.toMineral);

        if (tradePair.IsInteractivePrice())
        {
            StartCoroutine(KeepUpdatingTradeButton());
        }
        else
        {
            GetObserverRegistry()?.Add(this);
            UpdateTradeButton();
        }
    }

    private void OnDestroy()
    {
        GetObserverRegistry()?.Remove(this);
    }

    private List<IObserver> GetObserverRegistry()
    {
        switch (tradePair.fromMineral)
        {
            case Mineral.SAPPHIRE:
                return Database.instance.sapphiresObservers;
            case Mineral.EMERALD:
                return Database.instance.emeraldsObservers;
            case Mineral.RUBY:
                return Database.instance.rubiesObservers;
            case Mineral.DIAMOND:
                return Database.instance.diamondsObservers;
            default:
                return null;
        }
    }

    public void OnTradeButtonClicked()
    {
        // Check if can trade, if so, trade
        if(tradePair.CanTrade())
        {
            tradePair.Trade();
            CheckInteractivePriceValue();
            UpdateTradeButton();
            UpdateShownAmounts();
        }
    }

    private IEnumerator KeepUpdatingTradeButton()
    {
        while(true)
        {
            UpdateTradeButton();
            yield return DelayWait.oneFifthSecond;
        }
    }

    private void UpdateTradeButton()
    {
        tradeButton.interactable = tradePair.CanTrade();
    }

    private void UpdateShownAmounts()
    {
        NumberFormatter.FormatInto(tradePair.GetFromAmount(), fromDigitsTxt, fromSuffixTxt, fromSuffixTxt.gameObject);
        NumberFormatter.FormatInto(tradePair.GetToAmount(), toDigitsTxt, toSuffixText, toSuffixText.gameObject);
        selectedAmountTxt.text = tradePair.selectedAmount + "x";
    }

    private void CheckInteractivePriceValue()
    {
        // Make sure the price does not exceed a certain point
        if (tradePair.IsInteractivePrice() && tradePair.GetFromAmount() > ExchangeBehaviour.MAX_DIAMOND_PRICE)
        {
            Destroy(gameObject);
        }
    }

    public void OnValueChanged()
    {
        UpdateTradeButton();
    }

    public void OnPlusButtonClicked()
    {
        tradePair.selectedAmount = Math.Min(tradePair.selectedAmount + 1, 999);
        UpdateShownAmounts();
        UpdateTradeButton();
    }

    public void OnMinusButtonClicked()
    {
        tradePair.selectedAmount = Math.Max(tradePair.selectedAmount - 1, 1);
        UpdateShownAmounts();
        UpdateTradeButton();
    }
}