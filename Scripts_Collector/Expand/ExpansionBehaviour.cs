using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System;

public class ExpansionBehaviour : MonoBehaviour
{
    [SerializeField]
    private TextMultiLang titleTxt, descriptionTxt;

    [SerializeField]
    private TMP_Text priceTxt, offlineTimeTxt, adBoostTxt, spinsTxt;

    [SerializeField]
    public Button lockedBtn, unlockedBtn;

    [SerializeField]
    private Image locationImg, priceImg, adBoostImg, spinsImg;

    [SerializeField]
    private GameObject lockedContainer, unlockedContainer;

    public LocationMetaData location;
    public Action<LocationMetaData> travelCallback;
    private bool isUnlocked;

    private void Start()
    {
        titleTxt.translationId = location.name;
        locationImg.sprite = location.GetSpriteForLocation();

        UpdateLocationAvailability();
    }

    private void UpdateLocationAvailability()
    {
        // To prevent any coroutines running multiple times
        StopAllCoroutines();

        isUnlocked = Database.instance.locationDatas.ContainsKey(location.ID);

        lockedContainer.SetActive(!isUnlocked);
        unlockedContainer.SetActive(isUnlocked);

        if (isUnlocked)
        {
            bool isCurrent = Database.instance.activeLocation.metaData.ID == location.ID;
            unlockedBtn.interactable = !isCurrent;

            if (isCurrent)
            {
                offlineTimeTxt.text = Translator.GetTranslationForId(Translation_Script.EXPAND_CURRENT_LOCATION);
            }
            else
            {
                if (Database.instance.locationDatas[location.ID].lastOnlineTime.Year < 2020)
                {
                    // Location has not yet been played yet
                    offlineTimeTxt.text = Translator.GetTranslationForId(Translation_Script.EXPAND_TRAVEL_TO_START);
                }
                else
                {
                    // Show offline time
                    StartCoroutine(UpdateOfflineTime());
                }
            }

            StartCoroutine(UpdateAdBoostTime());
            StartCoroutine(UpdateSpinsTime());
        }
        else
        {
            descriptionTxt.translationId = location.description;

            priceImg.sprite = Game.GetSpriteForMineral(location.priceType);
            priceTxt.text = NumberFormatter.Format(location.priceValue);

            StartCoroutine(UpdateBuyButton());
        }
    }

    public void OnBuyButtonClicked()
    {
        // If has sufficient resources and not already unlocked, buy the location and update UI
        if(Database.instance.GetMineralAmount(location.priceType) >= location.priceValue && !Database.instance.locationDatas.ContainsKey(location.ID))
        {
            if (!Database.instance.SubstractMineral(location.priceType, location.priceValue, true))
                return;
            
            Database.instance.locationDatas.Add(location.ID, new LocationData());

            UpdateLocationAvailability();

            // Show a short message from dude
            Translation_Script message;

            switch(location.ID)
            {
                case Locations.MOON:
                    // Tell him to travel to the moon
                    message = Translation_Script.TUT_EXPANSION_BOUGHT_MOON;
                break;
                case Locations.MARS:
                    // Tell him to travel to mars
                    message = Translation_Script.TUT_EXPANSION_BOUGHT_MARS;
                    break;
                default:
                    // Dont show any message
                    return;
            }

            transform.root.GetComponent<Game>().ShowTutorial(new Tutorial(new List<TutorialStep>()
            {
                new DelayTutorialStep(.5f),
                new ExplainTutorialStep(message),
                new ClickHintTutorialStep(transform.parent, () => this == null, () => unlockedBtn.transform.position)
            }));
        }
    }

    public void OnTravelButtonClicked()
    {
        if (Database.instance.locationDatas.ContainsKey(location.ID))
        {
            // Quickfix for gold rock being stuck after switching locations while gold rock is active
            BoulderBehaviour.goldRockTime = 0;

            // Travel to this location
            Travel.location = location;
            SceneManager.LoadScene(Loading.SCENE_ID_TRAVEL);
        }
    }

    private IEnumerator UpdateBuyButton()
    {
        while(!isUnlocked)
        {
            lockedBtn.interactable = Database.instance.GetMineralAmount(location.priceType) >= location.priceValue;
            yield return null;
        }
    }

    private IEnumerator UpdateOfflineTime()
    {
        DateTime lastOnline = Database.instance.locationDatas[location.ID].lastOnlineTime;

        while(true)
        {
            offlineTimeTxt.text =  Translator.GetTranslationForId(Translation_Script.OFFLINE_FOR) + " " + TimeManager.FormatTimeSpan(TimeManager.instance.Time() - lastOnline);
            yield return DelayWait.oneSecond;
        }
    }

    private IEnumerator UpdateAdBoostTime()
    {
        DateTime lastOnline = Database.instance.locationDatas[location.ID].lastOnlineTime;
        float adboost = Database.instance.locationDatas[location.ID].adBoostTime;
        double timeLeft = adboost - (TimeManager.instance.Time() - lastOnline).TotalSeconds;

        while(timeLeft > 0)
        {
            adBoostTxt.text = TimeManager.FormatSeconds(timeLeft);
            yield return DelayWait.oneSecond;
            timeLeft = adboost - (TimeManager.instance.Time() - lastOnline).TotalSeconds;
        }

        // Show boost expired
        adBoostTxt.text = "00:00:00";
        adBoostTxt.color = new Color(1, 0, 0);
    }

    private IEnumerator UpdateSpinsTime()
    {
        Tuple<DateTime, int> spinsViewed = Database.instance.locationDatas[location.ID].spinsViewedToday;

        if(CalcSpinsAvailable(spinsViewed) == 0)
        {
            // If all spins used, show time left till new free spins
            DateTime newSpinsAvailable = spinsViewed.Item1.AddHours(SpinBehaviour.RESET_FREE_PER_HOURS);
            double timeLeft = (newSpinsAvailable - TimeManager.instance.Time()).TotalSeconds;

            while(timeLeft > 0)
            {
                spinsTxt.text = TimeManager.FormatSeconds(timeLeft);
                yield return DelayWait.oneSecond;
                timeLeft = (newSpinsAvailable - TimeManager.instance.Time()).TotalSeconds;
            }
        }

        // Show free spins are available
        spinsTxt.text = CalcSpinsAvailable(spinsViewed)+ "/" + SpinBehaviour.MAX_FREE;
        spinsTxt.color = new Color(1, 0, 0);
    }

    public static int CalcSpinsAvailable(Tuple<DateTime, int> spinsViewed)
    {
        if(spinsViewed == null)
        {
            return SpinBehaviour.MAX_FREE;
        }
        else
        {
            if(spinsViewed.Item1.AddHours(SpinBehaviour.RESET_FREE_PER_HOURS) <= TimeManager.instance.Time())
            {
                return SpinBehaviour.MAX_FREE;
            }
            else
            {
                return SpinBehaviour.MAX_FREE - spinsViewed.Item2;
            }
        }
    }
}