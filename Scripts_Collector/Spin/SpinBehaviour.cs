using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public enum SpinPrice
{
    DIAMONDS = 1,
    EMERALDS = 2,
    TIME_WARP_4H = 3,
    SAPPHIRES = 4,
    RUBIES = 5,
    TIME_WARP_24H = 6,
}

public class SpinBehaviour : GenericMenuItem
{
    private const double RUBY_PRICE = 2;
    public const int MAX_FREE = 3;
    public const float RESET_FREE_PER_HOURS = 8;

    [SerializeField]
    private GameObject adPriceObj, adLoadingObj, freePriceObj;

    [SerializeField]
    private Button adButton, rubyButton;

    [SerializeField]
    private TMP_Text adPriceTxt, rubyPriceTxt;

    [SerializeField]
    private RectTransform spinRect;

    private bool isSpinning = false;

    private void Start()
    {
        rubyPriceTxt.text = RUBY_PRICE.ToString();
        UpdateRubyButton();

        if (Database.instance.spinWithAd == 0 || Advertisement.instance.IsRewardedAdReady())
        {
            UpdateAdButton(true);
        }
        else
        {
            StartCoroutine(WaitForAdReady());
        }

        if(!IsFreeSpinAvailable())
        {
            StartCoroutine(FreeSpinCountdown());
        }
    }

    public override void OnOpened(int fragmentIndex)
    {
        if(Database.instance.spinWithAd == 0)
        {
            menu.game.ShowTutorial(new Tutorial(new List<TutorialStep>() 
            {
                new ExplainTutorialStep(Translation_Script.TUT_SPIN_1),
                new ExplainTutorialStep(Translation_Script.TUT_SPIN_2),
                new ExplainTutorialStep(Translation_Script.TUT_SPIN_3),
                new ClickHintTutorialStep(adButton.transform, () => Database.instance.spinWithAd > 0),
            }));
        }
    }

    private IEnumerator FreeSpinCountdown()
    {
        adPriceTxt.fontSize = 42;

        while ((TimeManager.instance.Time() - Database.instance.activeLocation.data.spinsViewedToday.Item1).TotalHours < RESET_FREE_PER_HOURS)
        {
            adPriceTxt.text = TimeManager.FormatTimeSpan(Database.instance.activeLocation.data.spinsViewedToday.Item1.AddHours(RESET_FREE_PER_HOURS) - TimeManager.instance.Time());
            yield return DelayWait.oneSecond;
        }

        adPriceTxt.fontSize = 48;
        UpdateAdButton();
    }

    public void OnWatchVideoButtonClicked()
    {
        if (!isSpinning && IsFreeSpinAvailable())
        {
            // First spin is free!
            if(Database.instance.spinWithAd == 0)
            {
                Spin();
                Database.instance.spinWithAd++;

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
            else if(Advertisement.instance.IsRewardedAdReady())
            {
                menu.game.ShowRewardedVideoAd(() =>
                {
                    Spin();
                    Database.instance.spinWithAd++;

                    // Store free spin used
                    FreeSpinUsed();

                    UpdateRubyButton();
                    UpdateAdButton();
                });

                StartCoroutine(WaitForAdReady());
            }
        }
    }

    public void OnRubiesButtonClicked()
    {
        if (!isSpinning && Database.instance.Rubies >= RUBY_PRICE)
        {
            Database.instance.SubstractRubies(RUBY_PRICE, true);

            Spin();
            Database.instance.spinWithRuby++;

            UpdateRubyButton();
            UpdateAdButton();
        }
    }

    private void Spin()
    {
        // Spin and grant reward
        int roll = UnityEngine.Random.Range(1, 101);

        if (roll <= 30)
        {
            // 4 Sapphires      30%
            StartCoroutine(SpinTheWheel(SpinPrice.SAPPHIRES));
        }
        else if (roll <= 60)
        {
            // 2 Emeralds       30%
            StartCoroutine(SpinTheWheel(SpinPrice.EMERALDS));
        }
        else if (roll <= 78)
        {
            // 2 Rubies         18%
            StartCoroutine(SpinTheWheel(SpinPrice.RUBIES));
        }
        else if (roll <= 96)
        {
            // 1 Diamond        18%
            StartCoroutine(SpinTheWheel(SpinPrice.DIAMONDS));
        }
        else if (roll <= 99)
        {
            // 4H time warp     3%
            StartCoroutine(SpinTheWheel(SpinPrice.TIME_WARP_4H));
        }
        else
        {
            // 24H time warp    1%
            StartCoroutine(SpinTheWheel(SpinPrice.TIME_WARP_24H));
        }
    }

    private IEnumerator SpinTheWheel(SpinPrice price)
    {
        isSpinning = true;

        // Rotate the wheel and wait till its done
        float start = spinRect.eulerAngles.z;
        float end = (3 * 360) + ((int)price * 60) + UnityEngine.Random.Range(-26, 27);

        float time = 0;
        while(time < 1)
        {
            time += Time.deltaTime / 1.5f * ((1.015f - time) / .8f);
            spinRect.eulerAngles = new Vector3(0, 0, Mathf.Lerp(start, end, time));
            yield return null;
        }

        yield return DelayWait.halfSecond;

        List<GrantedResource> winnings = new List<GrantedResource>();

        switch(price)
        {
            case SpinPrice.SAPPHIRES:
                Database.instance.AddSapphires(4);
                winnings.Add(new GrantedResource(Grant.SAPPHIRES, 4));
                break;
            case SpinPrice.EMERALDS:
                Database.instance.AddEmeralds(2);
                winnings.Add(new GrantedResource(Grant.EMERALDS, 2));
                break;
            case SpinPrice.RUBIES:
                Database.instance.AddRubies(2);
                winnings.Add(new GrantedResource(Grant.RUBIES, 2));
                // Make sure the ruby button is updated after granting rubies
                UpdateRubyButton();
                break;
            case SpinPrice.DIAMONDS:
                Database.instance.AddDiamonds(1);
                winnings.Add(new GrantedResource(Grant.DIAMONDS, 1));
                break;
            case SpinPrice.TIME_WARP_4H:
                // Min 1 million rocks
                double rocks4H = Math.Max(1E+6, TimeWarpBehaviour.GetIncomeForTimeWarp(Item.TIME_WARP_4H, menu.game));
                Database.instance.activeLocation.AddRocks(rocks4H);
                winnings.Add(new GrantedResource(Grant.ROCKS, rocks4H));
                break;
            case SpinPrice.TIME_WARP_24H:
                // Min 1 billion rocks
                double rocks24H = Math.Max(1E+9, TimeWarpBehaviour.GetIncomeForTimeWarp(Item.TIME_WARP_24H, menu.game));
                Database.instance.activeLocation.AddRocks(rocks24H);
                winnings.Add(new GrantedResource(Grant.ROCKS, rocks24H));
                break;
        }

        menu.game.ShowResourcesEarned(winnings);

        isSpinning = false;
    }

    private void UpdateRubyButton()
    {
        rubyButton.interactable = Database.instance.Rubies >= RUBY_PRICE;
    }

    private void UpdateAdButton()
    {
        UpdateAdButton(Advertisement.instance.IsRewardedAdReady());
    }

    private void UpdateAdButton(bool adReady)
    {
        // First spin is free and always available
        if(Database.instance.spinWithAd == 0)
        {
            freePriceObj.SetActive(true);
            adPriceObj.SetActive(false);
            adLoadingObj.SetActive(false);
            adButton.interactable = true;
        }
        else if (IsFreeSpinAvailable())
        {
            adPriceTxt.text = FreeSpinsAvailable() + "/" + MAX_FREE;
            adPriceObj.SetActive(adReady);
            adLoadingObj.SetActive(!adReady);
            adButton.interactable = adReady;
        }
        else
        {
            adPriceObj.SetActive(true);
            adLoadingObj.SetActive(false);
            adButton.interactable = false;
        }
    }

    public static bool IsFreeSpinAvailable()
    {
        return FreeSpinsAvailable() > 0;
    }

    public static int FreeSpinsAvailable()
    {
        return 
            Database.instance.activeLocation.data.spinsViewedToday == null || 
            (TimeManager.instance.Time() - Database.instance.activeLocation.data.spinsViewedToday.Item1).TotalHours >= RESET_FREE_PER_HOURS 
            ? 
            MAX_FREE 
            : 
            MAX_FREE - Database.instance.activeLocation.data.spinsViewedToday.Item2;
    }

    private void FreeSpinUsed()
    {
        if(Database.instance.activeLocation.data.spinsViewedToday == null ||
            (TimeManager.instance.Time() - Database.instance.activeLocation.data.spinsViewedToday.Item1).TotalHours >= RESET_FREE_PER_HOURS)
        {
            Database.instance.activeLocation.data.spinsViewedToday = Tuple.Create(TimeManager.instance.Time(), 1);
        }
        else
        {
            Database.instance.activeLocation.data.spinsViewedToday = Tuple.Create(Database.instance.activeLocation.data.spinsViewedToday.Item1, Database.instance.activeLocation.data.spinsViewedToday.Item2 + 1);
        }

        if (!IsFreeSpinAvailable())
        {
            StartCoroutine(FreeSpinCountdown());
            menu.game.spin.StartCountdown();
        }

        // This will tell all the observer button notifications to update
        ObserverButtonNotificationBehaviour.ValueChanged();
    }

    private IEnumerator WaitForAdReady()
    {
        UpdateAdButton(false);

        yield return new WaitUntil(() => !Advertisement.instance.IsShowingAd && Advertisement.instance.IsRewardedAdReady());

        UpdateAdButton(true);
    }

    public override bool IsAllowedToClose()
    {
        return !isSpinning;
    }
}