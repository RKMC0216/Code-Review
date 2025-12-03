using System.Collections;
using UnityEngine;
using TMPro;
using System;
using UnityEngine.Purchasing;

public class SpecialPackBehaviour : MonoBehaviour
{
    [SerializeField]
    private GameObject rubyContentPrefab, multiplierContentPrefab, timeWarpContentPrefab;

    [SerializeField]
    private RectTransform contentContainer;

    [SerializeField]
    private CodelessIAPButton iapButton;

    [SerializeField]
    private GameObject removeAdsObj;

    [SerializeField]
    private TMP_Text titleTxt, discountTxt, countdownTxt;

    [HideInInspector]
    public bool destroyWhenBought;

    public Action boughtCallback;
    public Action offerExpiredCallback;

    private SpecialPack specialPack;

    private void Start()
    {
        specialPack = SpecialPack.GetActiveSpecialPack();

        titleTxt.text = Translator.GetTranslationForId(specialPack.name);
        discountTxt.text = "-" + specialPack.discount + "%";
        iapButton.productId = specialPack.ID;
        iapButton.enabled = true;

        if(Database.instance.IAPScore >= 5)
        {
            // If the user already has an IAP score of 5 or higher he already has this unlocked so hide it
            removeAdsObj.SetActive(false);
        }

        // Set the visuals for the pack
        foreach(SpecialPackContent item in specialPack.contents)
        {
            CreateItem(item);
        }

        StartCoroutine(CountdownOfferExpires());
    }

    public void OnSpecialPackBought()
    {
        StartCoroutine(DelayedCallback());
    }

    private IEnumerator DelayedCallback()
    {
        // Wait a frame, because editor crashes if IAP button is destroyed in the same frame as the purchase was completed
        // Wouldn't crash on real devices, but it doesn't harm to still wait a frame tho
        yield return null;
        
        if(destroyWhenBought)
        {
            Destroy(gameObject);
        }

        // Generate a new special pack
        SpecialPack.GenerateNewSpecialPack();

        // Update the menu's shop button's countdown
        GameObject shopButton = transform.root.Find("Menu").GetComponent<MenuBehaviour>().shopButton;
        if(shopButton.activeInHierarchy)
        {
            shopButton.GetComponent<ShopMenuButtonBehaviour>().ResetCountdown();
        }

        // Invoke bought callback
        boughtCallback?.Invoke();
    }

    private IEnumerator CountdownOfferExpires()
    {
        while(specialPack.OfferExpiresInSeconds() > 0)
        {
            countdownTxt.text = TimeManager.FormatSeconds(specialPack.OfferExpiresInSeconds());
            yield return DelayWait.oneSecond;
        }

        Destroy(gameObject);
        offerExpiredCallback?.Invoke();
    }

    private void CreateItem(SpecialPackContent content)
    {
        if(content.item == Item.RUBIES)
        {
            // Ruby
            GameObject ruby = Instantiate(rubyContentPrefab, contentContainer);
            ruby.GetComponent<PackRubyBehaviour>().content = content;
        }
        else if(content.item == Item.MULTIPLIER)
        {
            // Multiplier
            GameObject multiplier = Instantiate(multiplierContentPrefab, contentContainer);
            multiplier.GetComponent<PackMultiplierBehaviour>().content = content;
        }
        else
        {
            // Time warp
            GameObject timeWarp = Instantiate(timeWarpContentPrefab, contentContainer);
            timeWarp.GetComponent<PackTimeWarpBehaviour>().content = content;
        }
    }
}