using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class AskPrestigeMethodBehaviour : GenericPopUpBehaviour
{
    [SerializeField]
    private TMP_Text claimableDigitsTxt, claimableSuffixTxt, premiumPriceTxt;

    [SerializeField]
    private Button premiumButton;

    [SerializeField]
    private Image ppImg;

    [SerializeField]
    private GameObject claimableSuffixObj;

    private void Start()
    {
        ppImg.sprite = Game.GetSpriteForMineral(Mineral.PRESTIGE_POINT);
        premiumPriceTxt.text = PrestigeBehaviour.PRESTIGE_WITHOUT_RESET_PRICE.ToString();
        premiumButton.interactable = Database.instance.Rubies >= PrestigeBehaviour.PRESTIGE_WITHOUT_RESET_PRICE;

        StartCoroutine(UpdateValue());
    }

    private IEnumerator UpdateValue()
    {
        while(true)
        {
            NumberFormatter.FormatInto(Database.instance.activeLocation.CalcPrestigePointsForReset(), claimableDigitsTxt, claimableSuffixTxt, claimableSuffixObj);
            yield return DelayWait.oneFifthSecond;
        }
    }
}