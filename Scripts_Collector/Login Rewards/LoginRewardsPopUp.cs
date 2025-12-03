using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using System;

public class LoginRewardsPopUp : GenericPopUpBehaviour
{
    [SerializeField]
    private GameObject rewardPrefab;

    [SerializeField]
    private RectTransform contentRect;

    private List<SpecialPackContent> rewards = new List<SpecialPackContent>
    {
        // Rocks' values are the amount of seconds worth of income!
        // 1-10
        new SpecialPackContent(Item.SAPPHIRES, 1),
        new SpecialPackContent(Item.EMERALDS, 1),
        new SpecialPackContent(Item.SAPPHIRES, 2),
        new SpecialPackContent(Item.EMERALDS, 2),
        new SpecialPackContent(Item.RUBIES, 1),
        new SpecialPackContent(Item.SAPPHIRES, 2),
        new SpecialPackContent(Item.TIME_WARP_24H, 1),
        new SpecialPackContent(Item.SAPPHIRES, 3),
        new SpecialPackContent(Item.EMERALDS, 2),
        new SpecialPackContent(Item.DIAMONDS, 1),
        // 11-20
        new SpecialPackContent(Item.SAPPHIRES, 3),
        new SpecialPackContent(Item.EMERALDS, 2),
        new SpecialPackContent(Item.RUBIES, 1),
        new SpecialPackContent(Item.EMERALDS, 2),
        new SpecialPackContent(Item.RUBIES, 1),
        new SpecialPackContent(Item.SAPPHIRES, 4),
        new SpecialPackContent(Item.EMERALDS, 2),
        new SpecialPackContent(Item.DIAMONDS, 1),
        new SpecialPackContent(Item.EMERALDS, 3),
        new SpecialPackContent(Item.DIAMONDS, 1),
        // 21-30
        new SpecialPackContent(Item.RUBIES, 1),
        new SpecialPackContent(Item.SAPPHIRES, 4),
        new SpecialPackContent(Item.RUBIES, 1),
        new SpecialPackContent(Item.EMERALDS, 3),
        new SpecialPackContent(Item.RUBIES, 1),
        new SpecialPackContent(Item.DIAMONDS, 1),
        new SpecialPackContent(Item.SAPPHIRES, 5),
        new SpecialPackContent(Item.DIAMONDS, 1),
        new SpecialPackContent(Item.EMERALDS, 2),
        new SpecialPackContent(Item.TIME_WARP_7D, 1),
        // 31-40
        new SpecialPackContent(Item.SAPPHIRES, 5),
        new SpecialPackContent(Item.RUBIES, 1),
        new SpecialPackContent(Item.RUBIES, 1),
        new SpecialPackContent(Item.EMERALDS, 3),
        new SpecialPackContent(Item.RUBIES, 2),
        new SpecialPackContent(Item.SAPPHIRES, 6),
        new SpecialPackContent(Item.DIAMONDS, 1),
        new SpecialPackContent(Item.DIAMONDS, 1),
        new SpecialPackContent(Item.EMERALDS, 3),
        new SpecialPackContent(Item.DIAMONDS, 2),
        // 41-50
        new SpecialPackContent(Item.SAPPHIRES, 6),
        new SpecialPackContent(Item.RUBIES, 1),
        new SpecialPackContent(Item.RUBIES, 2),
        new SpecialPackContent(Item.EMERALDS, 4),
        new SpecialPackContent(Item.RUBIES, 2),
        new SpecialPackContent(Item.SAPPHIRES, 7),
        new SpecialPackContent(Item.DIAMONDS, 1),
        new SpecialPackContent(Item.DIAMONDS, 2),
        new SpecialPackContent(Item.EMERALDS, 4),
        new SpecialPackContent(Item.DIAMONDS, 2),
        // 51-60
        new SpecialPackContent(Item.SAPPHIRES, 7),
        new SpecialPackContent(Item.RUBIES, 2),
        new SpecialPackContent(Item.RUBIES, 2),
        new SpecialPackContent(Item.EMERALDS, 4),
        new SpecialPackContent(Item.RUBIES, 2),
        new SpecialPackContent(Item.SAPPHIRES, 8),
        new SpecialPackContent(Item.DIAMONDS, 2),
        new SpecialPackContent(Item.DIAMONDS, 2),
        new SpecialPackContent(Item.EMERALDS, 4),
        new SpecialPackContent(Item.TIME_WARP_14D, 1),
        // 61-70
        new SpecialPackContent(Item.SAPPHIRES, 8),
        new SpecialPackContent(Item.RUBIES, 2),
        new SpecialPackContent(Item.RUBIES, 2),
        new SpecialPackContent(Item.EMERALDS, 4),
        new SpecialPackContent(Item.RUBIES, 2),
        new SpecialPackContent(Item.SAPPHIRES, 9),
        new SpecialPackContent(Item.DIAMONDS, 2),
        new SpecialPackContent(Item.DIAMONDS, 2),
        new SpecialPackContent(Item.EMERALDS, 4),
        new SpecialPackContent(Item.DIAMONDS, 2),
        // 71-80
        new SpecialPackContent(Item.SAPPHIRES, 9),
        new SpecialPackContent(Item.RUBIES, 2),
        new SpecialPackContent(Item.RUBIES, 2),
        new SpecialPackContent(Item.EMERALDS, 4),
        new SpecialPackContent(Item.RUBIES, 2),
        new SpecialPackContent(Item.SAPPHIRES, 10),
        new SpecialPackContent(Item.DIAMONDS, 2),
        new SpecialPackContent(Item.DIAMONDS, 2),
        new SpecialPackContent(Item.EMERALDS, 4),
        new SpecialPackContent(Item.DIAMONDS, 2),
        // 81-90
        new SpecialPackContent(Item.SAPPHIRES, 12),
        new SpecialPackContent(Item.RUBIES, 2),
        new SpecialPackContent(Item.EMERALDS, 5),
        new SpecialPackContent(Item.RUBIES, 2),
        new SpecialPackContent(Item.SAPPHIRES, 15),
        new SpecialPackContent(Item.EMERALDS, 5),
        new SpecialPackContent(Item.DIAMONDS, 5),
        new SpecialPackContent(Item.RUBIES, 5),
        new SpecialPackContent(Item.DIAMONDS, 5),
        new SpecialPackContent(Item.TIME_WARP_30D, 1),
    };

    private void Start()
    {
        // If the user has not claimed his login reward yesterday, restart his streak
        if((TimeManager.instance.Time().Date - Database.instance.lastLoginRewardClaimed.Date).TotalDays > 1)
        {
            Database.instance.currentLoginStreak = 0;
        }

        for (int i = 0; i < rewards.Count; i ++)
        {
            GameObject obj = Instantiate(rewardPrefab, contentRect);
            obj.GetComponent<LoginRewardBehaviour>().Initialize(((Database.instance.currentLoginStreak / rewards.Count) * rewards.Count) + i + 1, rewards[i], Database.instance.currentLoginStreak + 1);
        }

        StartCoroutine(PositionAfterAllRewardsInitialized());
    }

    private IEnumerator PositionAfterAllRewardsInitialized()
    {
        // Need to wait a frame for the rewards to be initialized and the content rect to have the correct size
        yield return null;

        // Set the content rect's position so the current daily reward is centered
        // Min/Max making sure that scroll position isnt set outside of its size and makes a jerky move upon start
        // 900 = width of scroll view
        // 450 = 1/2 width of scroll view
        contentRect.anchoredPosition = new Vector2(Math.Min(0, Math.Max(-contentRect.sizeDelta.x + 900, -contentRect.GetChild(Database.instance.currentLoginStreak % rewards.Count).localPosition.x + 450)),
            contentRect.anchoredPosition.y);
    }

    public void OnClaimButtonClicked()
    {
        SpecialPackContent reward = rewards[Database.instance.currentLoginStreak % rewards.Count];

        // Rocks has a custom reward granting system
        if (reward.item == Item.ROCKS)
        {
            // Grant the biggest of: 100 million or the income per second times the amount of seconds (value)
            double rocks = Math.Floor(Math.Max(100E+6, transform.root.GetComponent<Game>().CalcRawIncomePerSecond(false) * reward.value));
            Database.instance.activeLocation.AddRocks(rocks);

            // Save streak and close pop-up
            SaveAndClose();

            // Show the rocks granted
            transform.root.GetComponent<Game>().ShowResourcesEarned(new List<GrantedResource> { new GrantedResource(Grant.ROCKS, rocks) });
        }
        else
        {
            // Grant item with default method
            reward.GrantItem();

            // Save streak and close pop-up
            SaveAndClose();

            // Show the resources granted
            transform.root.GetComponent<Game>().ShowResourcesEarned(new List<GrantedResource> { reward.ConvertToGrantedResource() });
        }
    }

    private void SaveAndClose()
    {
        // Register date of last claim and increase streak
        Database.instance.lastLoginRewardClaimed = TimeManager.instance.Time();
        Database.instance.currentLoginStreak++;

        // This will close the pop-up
        OnConfirmButtonClicked();
    }
}