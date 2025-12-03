using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class AskExtraPrestigePointsBehaviour : GenericPopUpBehaviour
{
    [SerializeField]
    private TMP_Text extraDigitsTxt, extraSuffixTxt;

    [SerializeField]
    private Image ppImg;

    [HideInInspector]
    public double extraPoints;

    private void Start()
    {
        ppImg.sprite = Game.GetSpriteForMineral(Mineral.PRESTIGE_POINT);
        NumberFormatter.FormatInto(extraPoints, extraDigitsTxt, extraSuffixTxt, extraSuffixTxt.gameObject);
    }
}