using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class MultiplierBehaviour : MonoBehaviour
{
    [SerializeField]
    private TMP_Text titleTxt, descriptionTxt, priceTxt, multiplierTxt;

    [SerializeField]
    private Button buyButton;

    [HideInInspector]
    public double multiplier, price;

    public Action boughtCallback;

    private void Start()
    {
        titleTxt.text = "x" + multiplier + " " + Translator.GetTranslationForId(Translation_Script.SHOP_MULTIPLIER);
        descriptionTxt.text = Translator.GetTranslationForId(Translation_Script.SHOP_MULTIPLIER_INCREASE) + " +" + (multiplier * 100) + "%";
        priceTxt.text = price.ToString();
        multiplierTxt.text = "x" + multiplier;
    }

    private void OnEnable()
    {
        StartCoroutine(KeepUpdatingButton());
    }

    public void OnBuyButtonClicked()
    {
        if(CanAfford())
        {
            Database.instance.SubstractRubies(price, true);
            Database.instance.activeLocation.AddMultiplier(multiplier);
            boughtCallback?.Invoke();
            UpdateButton();
        }
    }

    private bool CanAfford()
    {
        return Database.instance.Rubies >= price;
    }

    private void UpdateButton()
    {
        buyButton.interactable = CanAfford();
    }

    private IEnumerator KeepUpdatingButton()
    {
        yield return null;

        while(true)
        {
            UpdateButton();
            yield return DelayWait.oneFifthSecond;
        }
    }
}