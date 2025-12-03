using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class AskMultiplyPowerUpBehaviour : GenericPopUpBehaviour
{
    [SerializeField]
    private Sprite goldRockSprite;

    [SerializeField]
    private TMP_Text
        normalValueDigitsTxt, normalValueSuffixTxt, 
        multipliedValueDigitsTxt, multipliedValueSuffixTxt, 
        explainTxt, confirmButtonTxt;

    [SerializeField]
    private Image normalValueImg, multipliedValueImg;

    public PowerUp powerUp;

    private void Start()
    {
        switch(powerUp.powerUpType)
        {
            case PowerUpType.TIME_WARP:
                normalValueImg.sprite = Game.GetSpriteForMineral(Mineral.ROCK);
                multipliedValueImg.sprite = Game.GetSpriteForMineral(Mineral.ROCK);

                NumberFormatter.FormatInto(powerUp.value, normalValueDigitsTxt, normalValueSuffixTxt, normalValueSuffixTxt.gameObject);
                NumberFormatter.FormatInto(powerUp.MultipliedValue(), multipliedValueDigitsTxt, multipliedValueSuffixTxt, multipliedValueSuffixTxt.gameObject);
                break;
            case PowerUpType.SUPER_TAP:
                normalValueImg.sprite = goldRockSprite;
                multipliedValueImg.sprite = goldRockSprite;

                normalValueDigitsTxt.text = powerUp.value.ToString();
                normalValueSuffixTxt.text = Translator.GetTranslationForId(Translation_Script.SECONDS);
                multipliedValueDigitsTxt.text = powerUp.MultipliedValue().ToString();
                multipliedValueSuffixTxt.text = Translator.GetTranslationForId(Translation_Script.SECONDS);
                break;
            default:
                OnCancelButtonClicked();
                break;
        }

        explainTxt.text = Translator.GetTranslationForId(Translation_Script.GOLD_ROCK_MULTIPLY) + " x" + powerUp.multiplier + "!</color>";
        confirmButtonTxt.text = "+" + (powerUp.multiplier * 100) + "%";
    }
}