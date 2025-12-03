using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class SellEmeraldsBehaviour : GenericMenuItem
{
    private const double EMERALD_SECONDS_WORTH_OF_INCOME = 75;

    [SerializeField]
    private InputField inputField;

    [SerializeField]
    private TMP_Text totalEmeraldsDigitsTxt, totalEmeraldsSuffixTxt, totalValueDigitsTxt, totalValueSuffixTxt;

    [SerializeField]
    private Button sellButton;

    private double amountToSell;
    private double emeraldValue;

    private void Start()
    {
        // Calc single emerald value, always minimum of 10K
        emeraldValue = Math.Floor(Math.Max(10E+3, EMERALD_SECONDS_WORTH_OF_INCOME * menu.game.CalcRawIncomePerSecond(false)));
        UpdateInputField(1);
    }

    public override void OnOpened(int fragmentIndex)
    {
        // If location = Earth and user has crushed a boulder but has never spent any emeralds yet: tell him to spend his first emerald
        if (fragmentIndex == 0 && Database.instance.activeLocation.metaData.ID == Locations.EARTH &&
            Database.instance.activeLocation.data.emeraldsSpent == 0)
        {
            if(Database.instance.activeLocation.data.lifetimeBouldersCrushed > 0)
            {
                // If an emerald was gained from boulder crush, it is in the middle of the tutorial to sell the first emerald, so tell him to sell it
                menu.game.ShowTutorial(new Tutorial(new List<TutorialStep>()
                {
                    new ExplainTutorialStep(Translation_Script.TUT_EMERALDS_UNLOCKED),
                    new ClickHintTutorialStep(sellButton.transform,
                        () => Database.instance.activeLocation.data.emeraldsSpent > 0),
                }));
            }
            else
            {
                // Else the user doesnt have any emeralds or got them from a different source, in that case still explain what they do
                menu.game.ShowTutorial(new Tutorial(new List<TutorialStep>()
                {
                    new ExplainTutorialStep(Translation_Script.TUT_EMERALDS_LOCKED),
                }));
            }
        }
    }

    public void OnMinButtonClicked()
    {
        UpdateInputField(1);
    }

    public void OnMinusButtonClicked()
    {
        UpdateInputField(amountToSell - 1);
    }

    public void OnPlusButtonClicked()
    {
        UpdateInputField(amountToSell + 1);
    }

    public void OnMaxButtonClicked()
    {
        UpdateInputField(Database.instance.Emeralds);
    }

    public void OnSellButtonClicked()
    {
        if(Database.instance.Emeralds >= amountToSell)
        {
            double value = ValueForEmeralds(amountToSell);

            Database.instance.SubstractEmeralds(amountToSell, false);
            Database.instance.activeLocation.AddRocks(value);
            menu.game.ShowResourcesEarned(new List<GrantedResource> { new GrantedResource(Grant.ROCKS, value) });

            if (amountToSell > Database.instance.Emeralds)
            {
                OnMaxButtonClicked();
            }
        }
    }

    public void OnInputValueChanged(string input)
    {
        if(string.IsNullOrEmpty(input))
        {
            amountToSell = 0;
        }
        else
        {
            amountToSell = double.Parse(input);
        }

        NumberFormatter.FormatInto(amountToSell, totalEmeraldsDigitsTxt, totalEmeraldsSuffixTxt, totalEmeraldsSuffixTxt.gameObject);
        NumberFormatter.FormatInto(ValueForEmeralds(amountToSell), totalValueDigitsTxt, totalValueSuffixTxt, totalValueSuffixTxt.gameObject);
        UpdateSellButton();
    }

    private void UpdateInputField(double amount)
    {
        // Max amount = (10^char limit) - 1
        // Example: char limit is 6
        // 10^6 = 1.000.000 (7 chars) - 1 = 999.999 (6 chars)
        inputField.text = Math.Min(Math.Pow(10, inputField.characterLimit) - 1, Math.Max(1, amount)).ToString();
    }

    private void UpdateSellButton()
    {
        sellButton.interactable = amountToSell > 0 && Database.instance.Emeralds >= amountToSell;
    }

    private double ValueForEmeralds(double amount)
    {
        return Math.Floor(amount * emeraldValue);
    }
}