using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System;
using TMPro;

public class CollectorBehaviour : Observer
{
    private const float MIN_DURATION = 0.5f;

    [SerializeField]
    private RectTransform artsContainer;

    [SerializeField]
    private GameObject lockedOverlay, incomeSuffixObj, priceSuffixObj, activeDiamondBoostObj, quickDurationOverlayObj;

    [SerializeField]
    private Image progressBorderImg, activeDiamondBoostImg, rockImg;

    [SerializeField]
    private TextMultiLang nameTxt;

    [SerializeField]
    private TMP_Text amountTxt,
        countdownTxt, incomeDigitsTxt, incomeSuffixTxt,
        buyAmountTxt, priceDigitsTxt, priceSuffixTxt,
        activeDiamondBoostMultiplier;

    [SerializeField]
    private Slider milestoneSlider, incomeSlider;

    [SerializeField]
    public Button buyButton;

    public Collector collector;

    private double incomePerCollector;
    private double income;

    private float duration;
    private float realUncappedDuration;
    private float timeLeft;

    private double buyAmount;
    private double price;
    private double discount;

    private Milestone nextMilestone;
    private static Milestone nextAllMilestone;

    private Location location;

    private void Start()
    {
        location = Database.instance.activeLocation;
        Instantiate(location.GetCollectorArtPrefab(collector.shortName[Language.EN]), artsContainer);

        nextMilestone = location.GetNextMilestoneForCollector(collector.ID);
        nextAllMilestone = location.GetNextMilestoneForCollector(Collector.TARGET_ALL_ID);

        // Show collector name
        nameTxt.translations = collector.name;
        rockImg.sprite = Game.GetSpriteForMineral(Mineral.ROCK);

        // Show amount and progress towards next milestone
        SetAmountText();
        UpdateMilestoneProgress();

        // Show current collectors advancements
        UpdateLockedVisibility();
        UpdateVisibleCollectors();

        // Initialize and show the values of this collector
        SetDuration();
        UpdateTotalIncome();
        UpdateDiscount();

        // Show active diamond boost multiplier
        UpdateActiveDiamondBoostVisual();   

        // Set and show time left
        timeLeft = location.data.collectorTimeLeft.ContainsKey(collector.ID) ? location.data.collectorTimeLeft[collector.ID] : duration;
        SetCountdownText();
    }

    public void StartCollecting()
    {
        // Make sure no duplicate coroutines will run
        StopAllCoroutines();

        // Start updating the buy button
        StartCoroutine(BuyButton());

        // Only start collecting if there are any of these collector
        if (collector.amountTotal > 0)
        {
            if (duration > MIN_DURATION)
            {
                StartCoroutine(IncomeWithTimer());
                StartCoroutine(Countdown());
            }
            else
            {
                incomeSlider.value = 0;
                countdownTxt.text = Translator.GetTranslationForId(Translation_Script.COLLECTOR_PER_SEC);
                quickDurationOverlayObj.SetActive(true);

                StartCoroutine(IncomeWithoutTimer());
            }
        }
    }

    public void StopCollecting()
    {
        StopAllCoroutines();
    }

    public void StoreTimeLeft()
    {
        if (collector.amountTotal > 0)
        {
            // Store the timeleft in the Location's data
            if (Database.instance.activeLocation.data.collectorTimeLeft.ContainsKey(collector.ID))
            {
                Database.instance.activeLocation.data.collectorTimeLeft[collector.ID] = timeLeft;
            }
            else
            {
                Database.instance.activeLocation.data.collectorTimeLeft.Add(collector.ID, timeLeft);
            }
        }
    }

    private void SetDuration()
    {
        Location location = Database.instance.activeLocation;

        realUncappedDuration = (float)(collector.baseDuration
            / location.GetMilestonesBonusMultiplierFor(collector.ID, MilestoneRewardType.SPEED)
            / location.GetUpgradesBonusMultiplierFor(collector.ID, UpgradeType.SPEED));

        duration = Math.Max(MIN_DURATION, realUncappedDuration);
    }

    public void UpdateDuration()
    {
        // Store old duration and update the duration
        float oldDuration = duration;
        SetDuration();

        // Calc and grant missed income = missed loops * income
        Database.instance.activeLocation.AddRocks(Math.Floor(Math.Floor((oldDuration - timeLeft) / duration) * income));

        // Calc and set new time left
        timeLeft = duration - ((oldDuration - timeLeft) % duration);

        // Check if time left text can be simply updated or if we should start different coroutines
        if(duration > MIN_DURATION)
        {
            // Update countdown text for new time left
            SetCountdownText();
        }
        else if(oldDuration > MIN_DURATION)
        {
            // If the new duration is under the min threshold but the old duration was above it
            // Update the coroutines which should be used
            StartCollecting();
        }
    }

    public void UpdateIncomePerCollector()
    {
        Location location = Database.instance.activeLocation;

        incomePerCollector = collector.baseIncome
            * location.GetMilestonesBonusMultiplierFor(collector.ID, MilestoneRewardType.PROFITS)
            * location.GetUpgradesBonusMultiplierFor(collector.ID, UpgradeType.PROFITS)
            * location.GetPrestigePointsBonusMultiplier()
            * location.GetDiamondBoostMultiplierFor(collector.ID)
            * location.data.multiplier
            * location.GetProfitBoosterBonusMultiplier()
            * location.GetActiveAdBonusMultiplier();

        if (realUncappedDuration < MIN_DURATION)
        {
            incomePerCollector *= (MIN_DURATION / realUncappedDuration);
        }
    }

    public void UpdateIncome()
    {
        income =  Math.Floor(collector.amountTotal * incomePerCollector);
        SetIncomeText();
    }

    public void UpdateTotalIncome()
    {
        UpdateIncomePerCollector();
        UpdateIncome();
    }

    public void UpdateDiscount()
    {
        discount = 1 - Database.instance.activeLocation.GetPriceDiscountFor(collector.ID);
        UpdateBuyAmountAndPrice();
    }

    public void UpdateEverything()
    {
        UpdateDuration();
        UpdateTotalIncome();
        UpdateDiscount();
    }

    public void UpdateActiveDiamondBoostVisual()
    {
        int stage = Database.instance.activeLocation.GetDiamondBoostStageFor(collector.ID);
        activeDiamondBoostObj.SetActive(stage >= 0);

        // -1 = none
        if (stage >= 0)
        {
            // Change between light blue/gold color every other stage
            Color color = stage % 2 == 0 ?
                // Light blue = #B9F2FF
                new Color(185f / 255f, 242f / 255f, 255f / 255f) :
                // Gold = #FFDF00
                new Color(255f / 255f, 233f / 255f, 0f / 255f);

            activeDiamondBoostImg.color = color;
            progressBorderImg.color = color;
            activeDiamondBoostMultiplier.text = "x" + BoostCollectorBehaviour.boosts[stage];
        }
    }

    public double CalcOfflineIncome(float time)
    {
        // Check if there are any collectors
        if(collector.amountTotal > 0)
        {
            // Check if the current loop can be finished
            if(time > timeLeft)
            {
                // Substract the time it took to finish that loop from the total time
                time -= timeLeft;

                // Set the time left to the remainder of the time after the max amount of loops
                timeLeft = duration - (time % duration);

                // Calculate how many complete loops the rest of the time can do and multiply that by the income per loop
                // +1 for the first loop that was finished
                return (1 + Math.Floor(time / duration)) * income;
            }
            else
            {
                // No complete loop finished, substract time from time left and return 0
                timeLeft -= time;
                return 0;
            }
        }
        else
        {
            // If you dont have collectors they cant earn income
            return 0;
        }
    }

    public double CalcIncomePerSecond()
    {
        return income / duration;
    }

    public void OnBuyButtonClicked()
    {
        Buy(buyAmount, true);
    }

    public void Buy(double amount, bool pay)
    {
        double oldAmount = collector.amountTotal;

        // Only substract rocks if the collectors aren't free
        if (pay)
        {
            // To make sure the correct price is used, calc the price in a new variable
            double priceCheck = collector.CalcPrice(amount, discount);

            if (Database.instance.activeLocation.data.rocks < priceCheck)
            {
                // If not sufficient funds available, cancel the purchase
                return;
            }

            if (!Database.instance.activeLocation.SubstractRocks(priceCheck, true))
                return;
        }
        
        collector.Bought(amount, pay);

        List<Milestone> completedMilestones = new List<Milestone>();

        while (nextMilestone != null && nextMilestone.IsGoalAchieved(collector.amountTotal))
        {
            nextMilestone.CompleteAndGrantReward();
            completedMilestones.Add(nextMilestone);

            nextMilestone = Database.instance.activeLocation.GetNextMilestoneForMilestoneId(nextMilestone.ID);
        }

        while (nextAllMilestone != null && nextAllMilestone.IsGoalAchieved())
        {
            nextAllMilestone.CompleteAndGrantReward();
            completedMilestones.Add(nextAllMilestone);

            nextAllMilestone = Database.instance.activeLocation.GetNextMilestoneForMilestoneId(nextAllMilestone.ID);
        }

        if (completedMilestones.Count > 0)
        {
            Game game = transform.root.GetComponent<Game>();

            // Normal milestone could also affect other collector, so only updating this collector is not always the case
            // Update all collectors, including their duration, income and price
            game.UpdateEverything();

            // Show a notifcation for the completed milestone(s)
            game.NotifyMilestonesCompleted(completedMilestones);
        }
        else
        {
            // Only update the income by recalcing the amount of colls * income per coll
            UpdateIncome();

            // Also update the price of this collector
            UpdateBuyAmountAndPrice();
        }

        UpdateMilestoneProgress();        
        SetAmountText();

        if (oldAmount == 0 && collector.amountTotal > 0)
        {
            UpdateLockedVisibility();
            StartCollecting();
        }

        if(oldAmount < 30)
        {
            UpdateVisibleCollectors();
        }
    }

    private IEnumerator IncomeWithTimer()
    {
        incomeSlider.value = timeLeft / duration;

        while (true)
        {
            yield return null;
            timeLeft -= Time.unscaledDeltaTime;

            if (timeLeft <= 0)
            {
                Database.instance.activeLocation.AddRocks(income);
                timeLeft = duration;
                SetCountdownText();
            }

            incomeSlider.value = timeLeft / duration;
        }
    }

    private IEnumerator IncomeWithoutTimer()
    {
        WaitForSecondsRealtime wait = new WaitForSecondsRealtime(duration);

        while (true)
        {
            // Duration is so small that the accurate time left doesn't matter
            yield return wait;
            Database.instance.activeLocation.AddRocks(income);
        }
    }

    private IEnumerator Countdown()
    {
        WaitForSecondsMutable inconsistentWait = new WaitForSecondsMutable();
 
        while (true)
        {
            SetCountdownText();
            yield return inconsistentWait.Wait(timeLeft % 1);
        }
    }

    private void SetCountdownText()
    {
        countdownTxt.text = TimeManager.FormatSeconds(Math.Ceiling(timeLeft));
    }

    private IEnumerator BuyButton()
    {
        while (true)
        {
            if (Database.instance.buyAmount == -1)
            {
                buyAmount = collector.CalcMaxBuyAmount(Database.instance.activeLocation.data.rocks, discount);
                price = collector.CalcPrice(buyAmount, discount);

                SetBuyAmountAndPrice();
            }

            buyButton.interactable = Database.instance.activeLocation.data.rocks >= price;
            yield return DelayWait.oneFifthSecond;
        }
    }

    private void SetAmountText()
    {
        if(nextMilestone != null)
        {
            amountTxt.text = collector.amountTotal + "/" + nextMilestone.milestoneGoal;
        }
        else
        {
            amountTxt.text = collector.amountTotal.ToString();
        }
    }

    private void SetIncomeText()
    {
        // If 0 collectors bought, show income per collector
        // If duration is below min threshold show income per second
        NumberFormatter.FormatInto(
            collector.amountTotal > 0 ?
                duration > MIN_DURATION ? income :
                Math.Floor(income * (1 / MIN_DURATION))
            : 
            incomePerCollector, incomeDigitsTxt, incomeSuffixTxt, incomeSuffixObj);
    }

    private void SetBuyAmountAndPrice()
    {
        buyAmountTxt.text = "x" + buyAmount;
        NumberFormatter.FormatInto(price, priceDigitsTxt, priceSuffixTxt, priceSuffixObj);
    }

    private void UpdateMilestoneProgress()
    {
        milestoneSlider.value = GetMilestoneProgress();
    }

    public float GetMilestoneProgress()
    {
        // If there's no next milestone, always show full progress bar
        return nextMilestone != null ? 1 - nextMilestone.GetProgressToCompletionValue() : 0;
    }

    private void UpdateLockedVisibility()
    {
        lockedOverlay.SetActive(collector.amountTotal == 0);
    }

    private void UpdateVisibleCollectors()
    {
        for (int i = 1; i <= collector.amountTotal; i++)
        {
            artsContainer.GetChild(0).Find(i.ToString())?.gameObject.SetActive(true);
        }
    }

    protected override List<IObserver> Registry()
    {
        return Database.instance.buyAmountObservers;
    }

    public override void OnValueChanged()
    {
        UpdateBuyAmountAndPrice();
    }

    private void UpdateBuyAmountAndPrice()
    {
        if (Database.instance.buyAmount == -1)
        {
            buyAmount = collector.CalcMaxBuyAmount(Database.instance.activeLocation.data.rocks, discount);
            price = collector.CalcPrice(buyAmount, discount);
        }
        else
        {
            buyAmount = Database.instance.buyAmount;
            price = collector.CalcPrice(buyAmount, discount);
        }

        SetBuyAmountAndPrice();
        buyButton.interactable = Database.instance.activeLocation.data.rocks >= price;
    }
}