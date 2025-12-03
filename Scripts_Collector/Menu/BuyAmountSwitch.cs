using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class BuyAmountSwitch : Observer
{
    [SerializeField]
    private TMP_Text buyAmountTxt;

    void Start()
    {
        UpdateBuyAmountText();
    }

    public void OnSwitchClicked()
    {
        Database.instance.RotateBuyAmount();
    }

    protected override List<IObserver> Registry()
    {
        return Database.instance.buyAmountObservers;
    }

    public override void OnValueChanged()
    {
        UpdateBuyAmountText();
    }

    private void UpdateBuyAmountText()
    {
        buyAmountTxt.text = (Database.instance.buyAmount == -1 ? Translator.GetTranslationForId(Translation_Inspector.MAX).ToUpper() : "x" + Database.instance.buyAmount);
    }
}