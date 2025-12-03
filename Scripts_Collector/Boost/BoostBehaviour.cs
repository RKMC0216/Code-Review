using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class BoostBehaviour : GenericMenuItem
{
    public const double AD_BOOST_MULTIPLIER = 3;

    private const double RUBY_PRICE = 2;
    private const float BOOST_TIME = 7200;
    public const int MAX_BOOSTS_PER_DAY = 12;
    private const int SAPPHIRE_REWARD_AT = 3;
    private const int EMERALD_REWARD_AT = 6;
    private const int RUBY_REWARD_AT = 9;
    private const int DIAMOND_REWARD_AT = 12;

    [SerializeField]
    private GameObject adPriceObj, adLoadingObj, freePriceObj;

    [SerializeField]
    private Slider progressSlider;

    [SerializeField]
    private Button adButton, rubyButton;

    [SerializeField]
    private TMP_Text rubyPriceTxt, countdownTxt;

    private void Start()
    {
        // If no previous tracked views or its a new day, create a new tracked view count of 0
        if (Database.instance.activeLocation.data.adBoostsViewedToday == null || Database.instance.activeLocation.data.adBoostsViewedToday.Item1.Date != TimeManager.instance.Time().ToLocalTime().Date)
        {
            // Use local time so the ad boosts resets at midnight local time
            Database.instance.activeLocation.data.adBoostsViewedToday = Tuple.Create(TimeManager.instance.Time().ToLocalTime(), 0);
        }

        rubyPriceTxt.text = RUBY_PRICE.ToString();
        UpdateRubyButton();

        if(Database.instance.boostWithAd == 0 || Advertisement.instance.IsRewardedAdReady())
        {
            UpdateAdButton(true);
        }
        else
        {
            StartCoroutine(WaitForAdReady());
        }

        UpdateSlider();
        StartCoroutine(KeepUpdatingCountdown());
    }

    public override void OnOpened(int fragmentIndex)
    {
        if (Database.instance.boostWithAd == 0)
        {
            menu.game.ShowTutorial(new Tutorial(new List<TutorialStep>()
            {
                new ExplainTutorialStep(Translation_Script.TUT_BOOST_1),
                new ExplainTutorialStep(Translation_Script.TUT_BOOST_2),
                new ExplainTutorialStep(Translation_Script.TUT_BOOST_3),
                new ClickHintTutorialStep(adButton.transform, () => Database.instance.boostWithAd > 0),
            }));
        }
    }

    public void OnWatchVideoButtonClicked()
    {
        if(Database.instance.boostWithAd == 0)
        {
            GrantBoost();
            Database.instance.boostWithAd++;

            freePriceObj.SetActive(false);
            UpdateRubyButton();

            if (Advertisement.instance.IsRewardedAdReady())
            {
                UpdateAdButton(true);
            }
            else
            {
                StartCoroutine(WaitForAdReady());
            }
        }
        else if (Advertisement.instance.IsRewardedAdReady())
        {
            menu.game.ShowRewardedVideoAd(() =>
            {
                GrantBoost();
                Database.instance.boostWithAd++;

                UpdateRubyButton();
                UpdateAdButton();
            });

            StartCoroutine(WaitForAdReady());
        }
    }

    public void OnRubiesButtonClicked()
    {
        if(Database.instance.Rubies >= RUBY_PRICE)
        {
            Database.instance.SubstractRubies(RUBY_PRICE, true);

            GrantBoost();
            Database.instance.boostWithRuby++;

            UpdateRubyButton();
            UpdateAdButton();
        }
    }

    private void GrantBoost()
    {
        // Track this boost granted
        Database.instance.activeLocation.data.adBoostsViewedToday = Tuple.Create(Database.instance.activeLocation.data.adBoostsViewedToday.Item1, Database.instance.activeLocation.data.adBoostsViewedToday.Item2 + 1);

        bool shouldStartCountdown = Database.instance.activeLocation.data.adBoostTime == 0;
        Database.instance.activeLocation.data.adBoostTime += BOOST_TIME;

        // Some views grant a special reward
        switch (Database.instance.activeLocation.data.adBoostsViewedToday.Item2)
        {
            case SAPPHIRE_REWARD_AT:
                Database.instance.AddSapphires(2);
                menu.game.ShowResourcesEarned(new List<GrantedResource> { new GrantedResource(Grant.SAPPHIRES, 2) });
                break;
            case EMERALD_REWARD_AT:
                Database.instance.AddEmeralds(2);
                menu.game.ShowResourcesEarned(new List<GrantedResource> { new GrantedResource(Grant.EMERALDS, 2) });
                break;
            case RUBY_REWARD_AT:
                Database.instance.AddRubies(2);
                menu.game.ShowResourcesEarned(new List<GrantedResource> { new GrantedResource(Grant.RUBIES, 2) });
                break;
            case DIAMOND_REWARD_AT:
                Database.instance.AddDiamonds(2);
                menu.game.ShowResourcesEarned(new List<GrantedResource> { new GrantedResource(Grant.DIAMONDS, 2) });
                break;
        }

        UpdateCountdown();
        UpdateSlider();

        if(shouldStartCountdown)
        {
            // Update all collectors and start the countdown
            menu.game.UpdateIncomes();
            menu.game.adBoost.StartCountdown();
        }

        // This will tell all the observer button notifications to update
        ObserverButtonNotificationBehaviour.ValueChanged();
    }

    private void UpdateSlider()
    {
        // Casting is definitely not redundant!
        progressSlider.value = 1 - ((float)Database.instance.activeLocation.data.adBoostsViewedToday.Item2 / (float)DIAMOND_REWARD_AT);
    }

    private void UpdateRubyButton()
    {
        rubyButton.interactable = Database.instance.Rubies >= RUBY_PRICE && !IsMaxedForToday();
    }

    private void UpdateAdButton()
    {
        UpdateAdButton(Advertisement.instance.IsRewardedAdReady());
    }

    private void UpdateAdButton(bool adReady)
    {
        // First boost is free and always available if not maxed out for today
        if (Database.instance.boostWithAd == 0)
        {
            freePriceObj.SetActive(true);
            adPriceObj.SetActive(false);
            adLoadingObj.SetActive(false);
            adButton.interactable = !IsMaxedForToday();
        }
        else
        {
            adPriceObj.SetActive(adReady);
            adLoadingObj.SetActive(!adReady);
            adButton.interactable = adReady && !IsMaxedForToday();
        }
    }

    private void UpdateCountdown()
    {
        countdownTxt.text = TimeManager.FormatSeconds(Database.instance.activeLocation.data.adBoostTime);
    }

    private bool IsMaxedForToday()
    {
        return Database.instance.activeLocation.data.adBoostsViewedToday.Item2 >= MAX_BOOSTS_PER_DAY;
    }

    private IEnumerator WaitForAdReady()
    {
        UpdateAdButton(false);

        yield return new WaitUntil(() => !Advertisement.instance.IsShowingAd && Advertisement.instance.IsRewardedAdReady());

        UpdateAdButton(true);
    }

    private IEnumerator KeepUpdatingCountdown()
    {
        while(true)
        {
            UpdateCountdown();
            yield return DelayWait.oneSecond;
        }
    }
}