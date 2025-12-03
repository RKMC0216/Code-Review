using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class BoosterBehaviour : MonoBehaviour
{
    [SerializeField]
    private Slider slider;

    [SerializeField]
    private TextMultiLang titleTxt;

    [SerializeField]
    private TMP_Text buttonTxt, countdownTxt;

    private ProfitBooster profitBooster;
    private LocationData data;

    private void Awake()
    {
        profitBooster = Database.instance.activeLocation.profitBooster;
        data = Database.instance.activeLocation.data;
    }

    private void Start()
    {
        if(data.profitBoosterBoostTime > 0)
        {
            // If the user left the previous session with his boost still on, set the boost time to 0
            data.profitBoosterBoostTime = 0;

            // Also set the remaining recharge time to the max recharge time minus the amount of seconds that have passed since the last session
            data.profitBoosterRechargeTime = Math.Max(0, profitBooster.boostRechargeTime - (float)(TimeManager.instance.Time() - data.lastOnlineTime).TotalSeconds);
        }
        else if(data.profitBoosterRechargeTime > 0)
        {
            // Else if the profit booster was recharging, decrease the left over recharge time with the amount of seconds that have passed since the last session
            data.profitBoosterRechargeTime = Math.Max(0, data.profitBoosterRechargeTime - (float)(TimeManager.instance.Time() - data.lastOnlineTime).TotalSeconds);
        }

        if(data.profitBoosterRechargeTime > 0)
        {
            // The boost is recharging...
            StartCoroutine(Recharge());
        }
        else
        {
            // Boost is ready to be used!
            ShowBoostReadyToBeDeployed();
        }
    }

    private void ShowBoostReadyToBeDeployed()
    {
        // Show boost is ready to be used!
        titleTxt.translationId = Translation_Inspector.PROFIT_BOOSTER_READY;
        buttonTxt.text = "GO";
        countdownTxt.text = TimeManager.FormatSeconds(profitBooster.maxBoostTime);
        slider.value = 0;
    }

    private IEnumerator Boost()
    {
        // Update all incomes to include the profit booster
        transform.root.GetComponent<Game>().UpdateIncomes();
        titleTxt.translationId = Translation_Inspector.PROFIT_BOOSTER_ACTIVE;
        titleTxt.text += " +" + (Database.instance.activeLocation.profitBooster.profitMultiplier * 100) + "%";
        buttonTxt.text = "+1s";

        while (data.profitBoosterBoostTime > 0)
        {
            data.profitBoosterBoostTime -= Time.unscaledDeltaTime;
            slider.value = 1 - (data.profitBoosterBoostTime / profitBooster.maxBoostTime);
            countdownTxt.text = TimeManager.FormatSeconds(data.profitBoosterBoostTime + 1);
            yield return null;
        }

        // If the user leaves the game when the boost is active and then resumes it, the boost can have a negative value equal to the
        // amount of time the user was away (minus the boost time left). This time should be substracted from the recharge time
        data.profitBoosterRechargeTime = profitBooster.boostRechargeTime + data.profitBoosterBoostTime;

        // Set the boost time to 0 and update all incomes to remove the profit booster effect
        data.profitBoosterBoostTime = 0;
        transform.root.GetComponent<Game>().UpdateIncomes();

        // Start recharging the booster
        StartCoroutine(Recharge());
    }

    private IEnumerator Recharge()
    {
        // Show boost is recharging...
        titleTxt.translationId = Translation_Inspector.PROFIT_BOOSTER_RECHARGING;
        buttonTxt.text = "-1s";

        while(data.profitBoosterRechargeTime > 0)
        {
            data.profitBoosterRechargeTime -= Time.unscaledDeltaTime;
            slider.value = data.profitBoosterRechargeTime / profitBooster.boostRechargeTime;
            countdownTxt.text = TimeManager.FormatSeconds(data.profitBoosterRechargeTime + 1);
            yield return null;
        }

        // Set the recharge time to 0 and show that the boost is ready to be used
        data.profitBoosterRechargeTime = 0;
        ShowBoostReadyToBeDeployed();
    }

    public void OnButtonClicked()
    {
        if (data.profitBoosterBoostTime > 0)
        {
            // If the boost is active, add one second to the boost time
            data.profitBoosterBoostTime = Math.Min(data.profitBoosterBoostTime + 1, profitBooster.maxBoostTime);
        }
        else if(data.profitBoosterRechargeTime > 0)
        {
            // If the boost is recharging, reduce the recharge time by one second
            data.profitBoosterRechargeTime--;
        }
        else
        {
            // Else the boost is ready to be used, start it!
            data.profitBoosterBoostTime = profitBooster.maxBoostTime;
            StartCoroutine(Boost());
        }
    }
}