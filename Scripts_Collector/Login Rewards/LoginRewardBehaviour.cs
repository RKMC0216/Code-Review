using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LoginRewardBehaviour : MonoBehaviour
{
    [SerializeField]
    private Sprite multiplierSprite, timeWarpSprite;

    [SerializeField]
    private Image contentContainerImg, rewardImg;

    [SerializeField]
    private TMP_Text rewardTxt, dayTxt, timeWarpTxt, multiplierTxt;

    [HideInInspector]
    public int day, currentStreakDay;

    public SpecialPackContent reward;

    public void Initialize(int day, SpecialPackContent reward, int currentStreakDay)
    {
        this.day = day;
        this.currentStreakDay = currentStreakDay;
        this.reward = reward;
    }

    private void Start()
    {
        dayTxt.text = day.ToString();

        if(day == currentStreakDay)
        {
            contentContainerImg.color = new Color(251f / 255f, 149f / 255f, 48f / 255f);
        }
        else if(day < currentStreakDay)
        {
            contentContainerImg.color = new Color(150f / 255f, 150f / 255f, 150f / 255f);
            rewardImg.color = new Color(150f / 255f, 150f / 255f, 150f / 255f);
        }

        rewardImg.sprite = GetSpriteForReward();

        // Show custom text is reward is multiplier
        if(reward.item == Item.MULTIPLIER)
        {
            rewardTxt.text = "+" + (reward.value * 100) + "%";
        }
        else
        {
            rewardTxt.text = reward.value >= 100 ? Translator.GetTranslationForId(Translation_Inspector.DAILY_REWARD_LOTS) : reward.value.ToString();
        }
    }

    private Sprite GetSpriteForReward()
    {
        switch(reward.item)
        {
            case Item.ROCKS:
                return Game.GetSpriteForMineral(Mineral.ROCK);
            case Item.SAPPHIRES:
                return Game.GetSpriteForMineral(Mineral.SAPPHIRE);
            case Item.EMERALDS:
                return Game.GetSpriteForMineral(Mineral.EMERALD);
            case Item.RUBIES:
                return Game.GetSpriteForMineral(Mineral.RUBY);
            case Item.DIAMONDS:
                return Game.GetSpriteForMineral(Mineral.DIAMOND);
            case Item.TIME_WARP_4H:
            case Item.TIME_WARP_24H:
            case Item.TIME_WARP_7D:
            case Item.TIME_WARP_14D:
            case Item.TIME_WARP_30D:
                // Show time warp time
                timeWarpTxt.transform.parent.gameObject.SetActive(true);
                timeWarpTxt.text = TimeWarpBehaviour.GetAbbreviationForTimeWarp(reward.item);
                return timeWarpSprite;
            case Item.MULTIPLIER:
                // Show multiplier amount
                multiplierTxt.gameObject.SetActive(true);
                multiplierTxt.text = "x" + reward.value;
                return multiplierSprite;
        }

        return null;
    }
}