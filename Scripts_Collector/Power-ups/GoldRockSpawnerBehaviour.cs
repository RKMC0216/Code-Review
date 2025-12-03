using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using System;

public class GoldRockSpawnerBehaviour : MonoBehaviour
{
    private const double ROCKS_SECONDS_OF_INCOME = 12;
    private const double MINIMUM_ROCKS_VALUE = 1E+4;
    private const float SUPER_TAP_DURATION = 20;

    [SerializeField]
    private GameObject goldRockPrefab, gainPrefab, multiplyPowerUpPopUp;

    [SerializeField]
    private RectTransform rect;

    private Game game;

    private void Start()
    {
        game = transform.root.GetComponent<Game>();
        StartCoroutine(Spawn());
    }

    private bool IsAllowedToSpawn()
    {
        // Gold rock are not allowed to spawn untill he has unlocked the Laser Drills (ID = 5) or has prestiged before
        return Database.instance.activeLocation.data.prestiges > 0 || Database.instance.activeLocation.GetCollectorForId(5).amountTotal > 0;
    }

    private IEnumerator Spawn()
    {
        // If gold rocks are not allowed to spawn yet, wait for that moment
        if (!IsAllowedToSpawn())
        {
            // Wait till gold rocks are allowed to spawn
            yield return new WaitUntil(IsAllowedToSpawn);

            // Wait a short moment before spawning the first gold rock
            yield return new WaitForSecondsRealtime(10);

            // Spawn the first gold rocks evurrr (for this location)
            SpawnGoldRock();

            // Wait a frame for the gold rock to spawn
            yield return null;
            
            // If location = earth, show click hint
            if (Database.instance.activeLocation.metaData.ID == Locations.EARTH)
            {
                game.ShowTutorial(new Tutorial(new List<TutorialStep>
                {
                    new ClickHintTutorialStep(transform.GetChild(0), () => transform.childCount == 0)
                }));
            }

            // Wait till the gold rock is gone, either by experation or clicking it
            yield return new WaitUntil(() => transform.childCount == 0);
        }

        WaitForSecondsRealtime wait = new WaitForSecondsRealtime(30);
        WaitUntil waitTillGone = new WaitUntil(() => transform.childCount == 0);

        while(true)
        {
            // Wait 30 seconds before spawning the next one
            yield return wait;

            // Spawn a gold rock
            SpawnGoldRock();

            // Wait a frame for the gold rock to spawn
            yield return null;
            // Wait till the gold rock is gone, either by experation or clicking it
            yield return waitTillGone;
        }
    }

    private void SpawnGoldRock()
    {
        GameObject goldRock = Instantiate(goldRockPrefab, transform);
        goldRock.GetComponent<RectTransform>().anchoredPosition = new Vector2(UnityEngine.Random.Range(-rect.rect.width / 2, rect.rect.width / 2), 
            UnityEngine.Random.Range(-rect.rect.height / 2, rect.rect.height / 2));
        goldRock.GetComponent<GoldRockBehaviour>().clicked = GoldRockClicked;
    }

    private void GoldRockClicked(Vector2 position)
    {
        // Determine what power-up this is
        PowerUp powerUp;

        // 12/100 chance to get a super tap power-up
        if (UnityEngine.Random.Range(0.0f, 1.0f) < .12f)
        {
            // SUPER TAP

            // 1 on 8 chance to be able to multiply this super tap if its not the first super tap
            if (UnityEngine.Random.Range(0, 8) == 0 && Database.instance.activeLocation.data.goldRockSuperTaps > 0)
            {
                powerUp = new PowerUp(PowerUpType.SUPER_TAP, SUPER_TAP_DURATION, 5);
            }
            else
            {
                powerUp = new PowerUp(PowerUpType.SUPER_TAP, SUPER_TAP_DURATION);
            }
        }
        else
        {
            // TIME WARP
            double rocks = Math.Max(MINIMUM_ROCKS_VALUE, Math.Floor(transform.root.GetComponent<Game>().CalcRawIncomePerSecond(false) * ROCKS_SECONDS_OF_INCOME));

            // Every 4th time warp is able to be multiplied, but only if the value is larger than the default value and if its not the first time warp
            if (Database.instance.activeLocation.data.goldRockTimeWarps > 0 &&
                Database.instance.activeLocation.data.goldRockTimeWarps % 4 == 0 && 
                rocks > MINIMUM_ROCKS_VALUE)
            {
                powerUp = new PowerUp(PowerUpType.TIME_WARP, rocks, 12);
            }
            else
            {
                powerUp = new PowerUp(PowerUpType.TIME_WARP, rocks);
            }
        }

        // Check if power-up is multiplyable and if ad is ready
        if(powerUp.IsMultiplyable() && Advertisement.instance.IsRewardedAdReady())
        {
            // Show power-up multiplier pop-up
            game.ShowPopUp(multiplyPowerUpPopUp, false, 
                (bool confirm) => 
            {
                if(confirm)
                {
                    // Show ad, if success set multiplied to true and grant power-up
                    game.ShowRewardedVideoAd(() =>
                    {
                        powerUp.Multiplied();
                        Database.instance.multiPowerWithAd++;

                        // Grant power-up
                        CompletePowerUp(powerUp, position);
                    });
                }
                else
                {
                    // Grant un-multiplied power-up
                    CompletePowerUp(powerUp, position);
                }
            },
                (GameObject content) =>
                {
                    // Init pop-up
                    content.GetComponent<AskMultiplyPowerUpBehaviour>().powerUp = powerUp;
                });
        }
        else
        {
            // If you don't have to wait for a callback, grant the power-up now
            CompletePowerUp(powerUp, position);

            // If on earth, and its the 5th gold rock clicked thats gives a bit of rocks, request feedback from the user
            if(Database.instance.activeLocation.metaData.ID == Locations.EARTH && powerUp.powerUpType == PowerUpType.TIME_WARP && Database.instance.activeLocation.data.goldRockTimeWarps == 5)
            {
                game.RequestFeedback();
            }
        }
    }

    private void CompletePowerUp(PowerUp powerUp, Vector2 position)
    {
        // If time warp, then show a gain text
        if (powerUp.powerUpType == PowerUpType.TIME_WARP)
        {
            GameObject gain = Instantiate(gainPrefab, transform);
            gain.GetComponent<RectTransform>().anchoredPosition = position;
            gain.GetComponent<GainBehaviour>().Spawn(powerUp.ActiveValue());
        }

        // Grant the actual power-up
        game.GrantPowerUp(powerUp);

        // Play gold rock sound
        AudioManager.instance.Play(Sound.GOLD_ROCK);
    }
}