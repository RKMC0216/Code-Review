using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class UpgradeBehaviour : MonoBehaviour
{
    [SerializeField]
    private GameObject unsafeUpgradePopUp;

    [SerializeField]
    private Image img;

    [SerializeField]
    private TMP_Text titleTxt, descriptionTxt, priceTxt;

    [SerializeField]
    public Button buyButton;

    public Upgrade upgrade;
    public Action<List<Upgrade>> boughtCallback;

    private void Start()
    {
        img.sprite = upgrade.GetTargetSprite();
        titleTxt.text = upgrade.GetNameForTarget(false);
        descriptionTxt.text = upgrade.GetRewardString();
        priceTxt.text = NumberFormatter.Format(upgrade.price);
    }

    private void OnEnable()
    {
        StartCoroutine(UpdateBuyButton());
    }

    public void OnBuyButtonClicked()
    {
        // Check if the upgrade is safe to buy (for prestige upgrades)
        if(upgrade.IsSafePurchase())
        {
            BuyIt();
        }
        else if(upgrade.HasSufficientFunds())
        {
            // Ask for confirmation
            transform.root.GetComponent<Game>().ShowPopUp(unsafeUpgradePopUp, true,
                (bool confirm) =>
            {
                if(confirm)
                {
                    BuyIt();
                }
            });
        }
    }

    public void DestroyIfBought()
    {
        if(upgrade.isBought)
        {
            Destroy(gameObject);
        }
    }

    private void BuyIt()
    {
        // Returns true if has sufficient funds and purchase was successful
        if (upgrade.Buy(true))
        {
            boughtCallback?.Invoke(new List<Upgrade>() { upgrade });
            Destroy(gameObject);
        }
    }

    private IEnumerator UpdateBuyButton()
    {
        yield return null;

        while(true)
        {
            buyButton.interactable = upgrade.HasSufficientFunds();
            yield return DelayWait.oneFifthSecond;
        }
    }
}