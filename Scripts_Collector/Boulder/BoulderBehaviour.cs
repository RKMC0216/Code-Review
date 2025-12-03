using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using UnityEngine.EventSystems;
using System.IO;

public class BoulderBehaviour : MonoBehaviour
{
    private const int STAGE_LENGTH = 100;
    private const float MIN_CLICK_INTERVAL = .1f;

    [SerializeField]
    private Sprite goldRockSprite;

    [SerializeField]
    public GameObject gainPrefab, explosionPrefab;

    private Sprite[] boulderSprites;
    private Sprite[] explosionSprites;

    [SerializeField]
    private Image img;

    [SerializeField]
    private TMP_Text lvlTxt;

    [SerializeField]
    private Slider lvlProgressSlider;

    public static double income;
    public static int goldRockTime;

    private Game game;
    private Location location;
    public double maxHP { get; private set; }
    private int stage;

    private double lvl;

    [HideInInspector]
    public bool clickable = true;
    private float lastClick;

    private Coroutine routine = null;

    private Queue<GameObject> gainPool = new Queue<GameObject>();

    private void Start()
    {
        game = transform.root.GetComponent<Game>();
        location = Database.instance.activeLocation;

        // Get boulder sprites for this location
        boulderSprites = Resources.LoadAll<Sprite>(Path.Combine("Boulder", "Big Rock", Database.instance.activeLocation.metaData.FolderName()));
        Array.Reverse(boulderSprites);

        // Get explosion sprites for this location
        explosionSprites = Resources.LoadAll<Sprite>(Path.Combine("Boulder", "Explosion", Database.instance.activeLocation.metaData.FolderName()));

        if(explosionSprites.Length == 4)
        {
            // Set explosion sprites to the right positions in the explosion prefab
            Image[] imgs = explosionPrefab.GetComponentsInChildren<Image>();
            imgs[1].sprite = explosionSprites[3];
            imgs[2].sprite = explosionSprites[1];
            imgs[3].sprite = explosionSprites[2];
            imgs[4].sprite = explosionSprites[0];
        }
        
        maxHP = CalcMaxHP();

        if(location.data.boulderHP <= 0)
        {
            ResetBoulder(false);
        }

        UpdateLevel();
        UpdateBoulderStage();
        SetLevelProgressSlider();

        StartCoroutine(WaitFrameBeforeCalculatingIncome());
    }

    private IEnumerator WaitFrameBeforeCalculatingIncome()
    {
        // Wait a frame for the collectors to be instantiated before calculating the income, else income would be set to NaN
        yield return null;
        UpdateIncome();
    }

    private void OnApplicationPause(bool pause)
    {
        if (pause && routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }
    }

    public void OnBoulderClicked(Vector2 position, bool hold)
    {
        // Check if the time since last click was long ago enough to prevent autoclickers
        if (!clickable || (!hold && Time.unscaledTime - lastClick < MIN_CLICK_INTERVAL))
        {
            return;
        }

        if(!hold)
        {
            // Click sound
            AudioManager.instance.Play(Sound.CRACKLE);
        }

        // Show gain
        ShowGain(position);

        // Dont damage if golden rock
        location.AddRockFromBoulderClick(income, goldRockTime <= 0);
        SetLevelProgressSlider();

        // Check if stage done
        if(CalcBoulderStage() != stage)
        {
            UpdateBoulderStage();

            // Check if boulder crushed
            if (stage == 0)
            {
                // Explode boulder, grant gem and update stage after animation is complete
                StartCoroutine(ExplodeForReward());
            }
        }

        // Check if level up
        if(CalcLevel(location.data.lifetimeBoulderClicks) > lvl)
        {
            UpdateLevel();
            UpdateIncome();

            // Make sure the slider is completely empty
            lvlProgressSlider.value = 1;

            // Notify level up
            game.NotifyLevelUp();
        }

        // Store time of this click
        lastClick = Time.unscaledTime;
    }

    private void ShowGain(Vector2 position)
    {
        if(gainPool.Count > 0)
        {
            // Get gain from pool
            GameObject gain = gainPool.Dequeue();
            gain.transform.position = position;
            gain.SetActive(true);
            gain.GetComponent<GainBehaviour>().Spawn(income);
        }
        else 
        {
            // Create new GameObject that will be added to the pool once anim is finished
            GameObject gain = Instantiate(gainPrefab, position, Quaternion.identity, transform);
            gain.GetComponent<GainBehaviour>().pool = gainPool;
            gain.GetComponent<GainBehaviour>().Spawn(income);
        }
    }

    private IEnumerator ExplodeForReward()
    {
        // Block clicks and hide boulder
        clickable = false;
        img.enabled = false;

        // Update img now already so it doesn't look weird when enabling and updating the img and the same time
        // Reset the boulder hp and log a boulder crush and reset the image
        ResetBoulder(true);
        UpdateBoulderStage();

        // Get a random mineral as a reward and grant it
        Mineral reward = 
            // If location = Earth and this is his first boulder crushed, always give him an emerald, so we can use it in the tutorial that will show
            Database.instance.activeLocation.metaData.ID == Locations.EARTH && Database.instance.activeLocation.data.lifetimeBouldersCrushed == 1
            ?
            Mineral.EMERALD
            :
            Game.RandomMineral(.65f, .30f, .03f, .02f);
        Database.instance.AddMineral(reward, 1);

        // Create object that will auto-play the explode animation
        GameObject explode = Instantiate(explosionPrefab, transform);
        explode.transform.Find("Reward").GetComponent<Image>().sprite = Game.GetSpriteForMineral(reward);

        // Crushed sound
        AudioManager.instance.Play(Sound.CRUSH);

        // After 2 seconds play gem gained sound
        AudioManager.instance.Play(Sound.GEMS_GAIN, 2f);

        // Wait till anim is done
        yield return new WaitUntil(() => explode == null);

        // Show boulder and unblock clicks
        img.enabled = true;
        clickable = true;

        // If location = Earth and this was his first boulder crushed ever and he hasnt spent emeralds before, show him how to use the emerald he just got
        if (Database.instance.activeLocation.metaData.ID == Locations.EARTH && 
            Database.instance.activeLocation.data.lifetimeBouldersCrushed == 1 && 
            reward == Mineral.EMERALD &&
            Database.instance.activeLocation.data.emeraldsSpent == 0)
        {
            game.ShowTutorial(new Tutorial(new List<TutorialStep>()
            {
                new ExplainTutorialStep(Translation_Script.TUT_CRUSH_1),
                new ExplainTutorialStep(Translation_Script.TUT_CRUSH_2),
                new ClickHintTutorialStep(transform.root.Find("UI").Find("Header").Find("Gems").Find("Emeralds"),
                    () => Database.instance.activeLocation.data.emeraldsSpent > 0,
                reversed: true),
            }));
        }
    }

    public void UpdateIncome()
    {
        income = CalcIncome();
    }

    private int CalcBoulderStage()
    {
        return (int) Math.Ceiling(location.data.boulderHP / STAGE_LENGTH);
    }

    public void UpdateBoulderStage()
    {
        stage = CalcBoulderStage();

        if (stage > 0 && stage <= boulderSprites.Length)
        {
            img.sprite = goldRockTime > 0 ? goldRockSprite : boulderSprites[CalcBoulderStage() - 1];
        }
    }

    private void SetLevelProgressSlider()
    {
        lvlProgressSlider.value = CalcLevelProgress();
    }

    private double CalcIncome()
    {
        if(goldRockTime > 0)
        {
            return Math.Max(game.CalcIncomePerSecond(false) / 5, CalcStandardIncome() * 2);
        }
        else
        {
            return CalcStandardIncome();
        }
    }

    private double CalcStandardIncome()
    {
        return Math.Floor(Math.Min(
            // Return either the actual income or the highest cap that is reached
            Math.Max(
                // This will be the highest cap for the early game, fairly high and will probably only be reached if cheating
                1E+10 + 1E+10 * Database.instance.activeLocation.data.prestigePoints,
                // This cap will eventualy surpass the prestige points based cap somewhere in the mid game and is reasonable to reach in the super late game
                game.CalcIncomePerSecond(true) / 50),
            // Income increases both linear and exponantionally every level, will be capped by the highest of the two previous mentioned values
            lvl * Math.Pow(1.2, lvl)
                * location.GetUpgradesBonusMultiplierFor(Collector.TARGET_CLICK_ID, UpgradeType.PROFITS)
                * location.GetPrestigePointsBonusMultiplier()
                * location.GetDiamondBoostMultiplierFor(Collector.TARGET_CLICK_ID)
                * location.data.multiplier
                * location.GetProfitBoosterBonusMultiplier()
                * location.GetActiveAdBonusMultiplier()));
    }

    public static double CalcLevel()
    {
        return CalcLevel(Database.instance.activeLocation.data.lifetimeBoulderClicks);
    }

    public static double CalcLevel(double xp)
    {
        return Math.Floor(CalcRawLevel(xp));
    }

    public static double CalcRawLevel(double xp)
    {
        // XP required for level lvl, this is the amount of xp/clicks required to level up to this level from the previous level
        // xpREQ(lvl) = 10 * lvl + 10

        // Total XP required for level lvl, this is the total xp/lifetime clicks required to reach this level
        // xpTOTAL(lvl) = ((xpREQ(lvl) + xpREQ(1)) / 2) * lvl

        // Level for total XP x, this is the level which the rock will be for the given total xp/lifetime clicks
        // lvl(x) = (-30 + sqrt(900 + 80x) / 20

        // Because we start off at lvl 1, add +1 to the formula
        return ((-30 + Math.Sqrt(900 + 80 * xp)) / 20) + 1;
    }

    private void UpdateLevel()
    {
        lvl = (int) CalcLevel();
        lvlTxt.text = "Lvl. " + lvl;
    }

    private float CalcLevelProgress()
    {
        return (float)(1 - (CalcRawLevel(location.data.lifetimeBoulderClicks) % 1));
    }

    private double CalcMaxHP()
    {
        return boulderSprites.Length * STAGE_LENGTH;
    }

    private void ResetBoulder(bool crushed)
    {
        location.data.boulderHP = maxHP;

        if(crushed)
        {
            location.data.sessionBouldersCrushed++;
        }
    }

    public IEnumerator BoulderHeld(Vector2 position)
    {
        // First click is manual
        OnBoulderClicked(position, false);

        while (true)
        {
            yield return DelayWait.oneFifthSecond;
            OnBoulderClicked(position, true);
        }
    }

    public void OnPointerDown(BaseEventData eventData)
    {
        if (routine == null)
        {
            routine = StartCoroutine(BoulderHeld(((PointerEventData)eventData).position));
        }
    }

    public void OnPointerUp(BaseEventData eventData)
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }
    }
}