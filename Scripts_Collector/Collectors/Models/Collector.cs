using System;
using System.Collections.Generic;

public class Collector
{
    public const int TARGET_ALL_ID = 0;
    public const int TARGET_CLICK_ID = -1;

    public static int GENERATED_ID = 1;

    public int ID { get; private set; }
    public Dictionary<Language, string> name { get; private set; }
    public Dictionary<Language, string> shortName { get; private set; }

    public double baseIncome { get; private set; }
    public float baseDuration { get; private set; }
    public double basePrice { get; private set; }
    public double priceMultiplier { get; private set; }

    public double amountBought;
    public double amountFree;

    public double amountTotal
    {
        get
        {
            return amountBought + amountFree;
        }
    }

    public Collector(Dictionary<Language, string> name, Dictionary<Language, string> shortName, double baseIncome, float baseDuration, double basePrice, double priceMultiplier, LocationData data)
    {
        ID = GENERATED_ID;
        GENERATED_ID++;
        this.name = name;
        this.shortName = shortName;

        this.baseIncome = baseIncome;
        this.baseDuration = baseDuration;
        this.basePrice = basePrice;
        this.priceMultiplier = priceMultiplier;

        amountBought = data.collectorsBought.ContainsKey(ID) ? data.collectorsBought[ID] : 0;
        amountFree = data.collectorsFree.ContainsKey(ID) ? data.collectorsFree[ID] : 0;
    }

    public string GetName(bool useShortName = false)
    {
        if(useShortName)
        {
            return Translator.GetTranslationFromSet(shortName);
        }
        else
        {
            return Translator.GetTranslationFromSet(name);
        }
    } 

    public void Bought(double amount, bool paid)
    {
        if (paid)
        {
            amountBought += amount;

            if (Database.instance.activeLocation.data.collectorsBought.ContainsKey(ID))
            {
                Database.instance.activeLocation.data.collectorsBought[ID] += amount;
            }
            else
            {
                Database.instance.activeLocation.data.collectorsBought.Add(ID, amountBought);
            }
        }
        // Add to the free collectors
        else
        {
            amountFree += amount;

            if (Database.instance.activeLocation.data.collectorsFree.ContainsKey(ID))
            {
                Database.instance.activeLocation.data.collectorsFree[ID] += amount;
            }
            else
            {
                Database.instance.activeLocation.data.collectorsFree.Add(ID, amountFree);
            }
        }
    }

    private double DiscountedBasePrice(double discount)
    {
        return basePrice * discount;
    }

    public double CalcPrice(double amount, double discount)
    {
        return Math.Round(DiscountedBasePrice(discount) * 
            (Math.Pow(priceMultiplier, amountBought) * (Math.Pow(priceMultiplier, amount) - 1) / (priceMultiplier - 1)), 0);
    }

    public double CalcMaxBuyAmount(double availableFunds, double discount)
    {
        // Always return 1 or more
        return Math.Max(1, Math.Floor(Math.Log((availableFunds * (priceMultiplier - 1) /
            (DiscountedBasePrice(discount) * Math.Pow(priceMultiplier, amountBought))) + 1, priceMultiplier)));
    }

    public void Reset()
    {
        amountBought = 0;
        amountFree = 0;
    }
}