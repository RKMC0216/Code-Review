using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class PrestigeBehaviour : GenericMenuItem
{
    public const double PRESTIGE_WITHOUT_RESET_PRICE = 20;
    public const double EXTRA_PRESTIGE_POINTS_MULTIPLIER = .2;

    [SerializeField]
    private GameObject prestigeMethodPrefab, extraPrestigePointsPrefab, notAdvicedPrestigePrefab;

    [SerializeField]
    private TMP_Text currentDigitsTxt, currentSuffixTxt, currentEffectivenessTxt,
        claimableDigitsTxt, claimableSuffixTxt;

    [SerializeField]
    private GameObject claimableSuffixObj;

    [SerializeField]
    private Button claimButton;

    [SerializeField]
    private Image ppCurrentImg, ppClaimableImg;

    private Location location;

    private void Start()
    {
        location = Database.instance.activeLocation;

        ppCurrentImg.sprite = Game.GetSpriteForMineral(Mineral.PRESTIGE_POINT);
        ppClaimableImg.sprite = Game.GetSpriteForMineral(Mineral.PRESTIGE_POINT);

        SetCurrentPrestigePoints();
        SetPrestigePointsEffectiveness();

        StartCoroutine(UpdateValues());
    }

    public override void OnOpened(int fragmentIndex)
    {
        // Check if location = earth and never prestiged before
        if(Database.instance.activeLocation.metaData.ID == Locations.EARTH && Database.instance.activeLocation.data.prestiges == 0)
        {
            // Check if ready to prestige
            if(Database.instance.activeLocation.PrestigeAdviced())
            {
                // Tell to prestige
                menu.game.ShowTutorial(new Tutorial(new List<TutorialStep>
                {
                    new ExplainTutorialStep(Translation_Script.TUT_PRESTIGE_READY_1),
                    new ExplainTutorialStep(Translation_Script.TUT_PRESTIGE_READY_2),
                    new ExplainTutorialStep(Translation_Script.TUT_PRESTIGE_READY_3),
                    new ClickHintTutorialStep(claimButton.transform, () => menu.game.transform.Find(menu.game.popUpPrefab.name + Game.GO_CLONE) != null)
                }));
            }
            else
            {
                // Tell to collect more rocks
                menu.game.ShowTutorial(new Tutorial(new List<TutorialStep>
                {
                    new ExplainTutorialStep(Translation_Script.TUT_PRESTIGE_NOT_READY)
                }));
            }
        }
    }

    public void OnClaimButtonClicked()
    {
        if(location.CalcPrestigePointsForReset() > 0)
        {
            if(location.PrestigeAdviced())
            {
                AskPrestigeMethod();
            }
            else
            {
                NotifyPrestigeNotAdviced();
            }
        }
    }

    private void NotifyPrestigeNotAdviced()
    {
        // Ask if player is sure he wants to prestige
        menu.game.ShowPopUp(notAdvicedPrestigePrefab, true,
            (bool confirm) => 
            {
                if(confirm)
                {
                    // Prestige anyway
                    AskPrestigeMethod();
                }
                
                // Else do nothing
            });
    }

    private void AskPrestigeMethod()
    {
        menu.game.ShowPopUp(prestigeMethodPrefab, true,
            (bool premium) =>
            {
                double points = location.CalcPrestigePointsForReset();

                if (!premium)
                {
                    // Prestige and reset
                    menu.game.Prestige(points, true);
                }
                else
                {
                    // Prestige without reset, substract 20 rubies
                    if (Database.instance.Rubies >= PRESTIGE_WITHOUT_RESET_PRICE)
                    {
                        Database.instance.SubstractRubies(PRESTIGE_WITHOUT_RESET_PRICE, true);
                        menu.game.Prestige(points, false);
                    }
                    else
                    {
                        return;
                    }
                }

                SetCurrentPrestigePoints();
                SetPrestigePointsEffectiveness();
                AskExtraPrestigePoints(points);
            });
    }

    private void AskExtraPrestigePoints(double fullPoints)
    {
        // Check if video is ready, if not, cancel offering extra prestige points
        if(!Advertisement.instance.IsRewardedAdReady())
        {
            ShowPetRocksEarned(fullPoints);
            return;
        }

        menu.game.ShowPopUp(extraPrestigePointsPrefab, false,
            (bool confirm) =>
            {
                if(confirm)
                {
                    menu.game.ShowRewardedVideoAd(() =>
                    {
                        Database.instance.activeLocation.AddPrestigePoints(Math.Round(fullPoints * EXTRA_PRESTIGE_POINTS_MULTIPLIER));
                        Database.instance.extraPrestigeWithAd++;

                        SetCurrentPrestigePoints();
                        menu.game.UpdateIncomes();

                        ShowPetRocksEarned(Math.Round(fullPoints * (1 + EXTRA_PRESTIGE_POINTS_MULTIPLIER)));
                    });
                }
                else
                {
                    ShowPetRocksEarned(fullPoints);
                }
            }, 
            (GameObject content) => 
            {
                // Init pop-up
                content.GetComponent<AskExtraPrestigePointsBehaviour>().extraPoints = Math.Round(fullPoints * EXTRA_PRESTIGE_POINTS_MULTIPLIER);
            });
    }

    private void ShowPetRocksEarned(double points)
    {
        // Show earnings
        menu.game.ShowResourcesEarned(new List<GrantedResource> { new GrantedResource(Grant.PRESTIGE_POINTS, points) }, Sound.PRESTIGE);

        // At prestige #3, #12 and #30 on Earth request the user to rate the app
        if(Database.instance.activeLocation.metaData.ID == Locations.EARTH && (Database.instance.activeLocation.data.prestiges == 3 || Database.instance.activeLocation.data.prestiges == 12 || Database.instance.activeLocation.data.prestiges == 30))
        {
            StartCoroutine(WaitToAskForRate());
        }
    }

    private IEnumerator WaitToAskForRate()
    {
        // Wait till game has no overlays, except the menu item
        yield return new WaitUntil(() => menu.game.IsNoOverlays(false));

        // Ask for rate
        menu.game.RequestFeedback();
    }

    private IEnumerator UpdateValues()
    {
        while(true)
        {
            SetClaimablePrestigePoints();
            claimButton.interactable = location.CalcPrestigePointsForReset() > 0;
            yield return DelayWait.oneFifthSecond;
        }
    }

    private void SetCurrentPrestigePoints()
    {
        NumberFormatter.FormatInto(location.data.prestigePoints, currentDigitsTxt, currentSuffixTxt, currentSuffixTxt.gameObject);
    }

    private void SetPrestigePointsEffectiveness()
    {
        currentEffectivenessTxt.text = "+" + (location.GetPrestigePointEffectiveness() * 100) + "%";
    }

    private void SetClaimablePrestigePoints()
    {
        NumberFormatter.FormatInto(location.CalcPrestigePointsForReset(), claimableDigitsTxt, claimableSuffixTxt, claimableSuffixObj);
    }
}