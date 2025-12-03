using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ResourceBehaviour : MonoBehaviour
{
    [SerializeField]
    private Sprite multiplierSprite, timeWarpSprite;

    [SerializeField]
    private GameObject timeWarpInfo;

    [SerializeField]
    private TMP_Text digitsTxt, suffixTxt, multiplierTxt, timeWarpTxt;

    [SerializeField]
    private Image backgroundImg, resourceImg;

    public GrantedResource resource;
    [HideInInspector]
    public bool invertColors = false;

    private void Start()
    {
        if(invertColors)
        {
            backgroundImg.color = new Color(1, 1, 1, .19f);
            digitsTxt.color = new Color(1, 1, 1);
            suffixTxt.color = new Color(1, 1, 1);
        }

        switch(resource.resource)
        {
            case Grant.ROCKS:
                MineralSetup(Mineral.ROCK);
                break;
            case Grant.SAPPHIRES:
                MineralSetup(Mineral.SAPPHIRE);
                break;
            case Grant.EMERALDS:
                MineralSetup(Mineral.EMERALD);
                break;
            case Grant.RUBIES:
                MineralSetup(Mineral.RUBY);
                break;
            case Grant.DIAMONDS:
                MineralSetup(Mineral.DIAMOND);
                break;
            case Grant.PRESTIGE_POINTS:
                MineralSetup(Mineral.PRESTIGE_POINT);
                break;
            case Grant.MULTIPLIER:
                MultiplierSetup();
                break;
            case Grant.TIME_WARP_24H:
                TimeWarpSetup(Item.TIME_WARP_24H);
                break;
            case Grant.TIME_WARP_7D:
                TimeWarpSetup(Item.TIME_WARP_7D);
                break;
            case Grant.TIME_WARP_14D:
                TimeWarpSetup(Item.TIME_WARP_14D);
                break;
            case Grant.TIME_WARP_30D:
                TimeWarpSetup(Item.TIME_WARP_30D);
                break;
            default:
                // Unknown resource
                Destroy(gameObject);
                break;
        }
    }

    private void MineralSetup(Mineral mineral)
    {
        resourceImg.sprite = Game.GetSpriteForMineral(mineral);
        NumberFormatter.FormatInto(resource.value, digitsTxt, suffixTxt, suffixTxt.gameObject);
    }

    private void MultiplierSetup()
    {
        resourceImg.sprite = multiplierSprite;
        multiplierTxt.gameObject.SetActive(true);
        multiplierTxt.text = "x" + resource.value;
        suffixTxt.gameObject.SetActive(false);
        digitsTxt.text = "+" + (resource.value * 100) + "%";
    }

    private void TimeWarpSetup(Item timeWarp)
    {
        resourceImg.sprite = timeWarpSprite;
        timeWarpInfo.SetActive(true);
        timeWarpTxt.text = TimeWarpBehaviour.GetAbbreviationForTimeWarp(timeWarp);
        NumberFormatter.FormatInto(resource.value, digitsTxt, suffixTxt, suffixTxt.gameObject);
    }
}