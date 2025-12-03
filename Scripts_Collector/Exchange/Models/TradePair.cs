using System;

public class TradePair
{
    public Mineral fromMineral { get; private set; }
    private double fromAmount;
    private Func<double> FromAmountFunc;

    public Mineral toMineral { get; private set; }
    private double toAmount;

    private Action Bought;

    public double selectedAmount = 1;

    public TradePair(Mineral fromMineral, double fromAmount, Mineral toMineral, double toAmount)
    {
        this.fromMineral = fromMineral;
        this.fromAmount = fromAmount;
        this.toMineral = toMineral;
        this.toAmount = toAmount;
    }

    public TradePair(Mineral fromMineral, Func<double> FromAmountFunc, Mineral toMineral, double toAmount, Action Bought) :
        this(fromMineral, 0, toMineral, toAmount)
    {
        this.FromAmountFunc = FromAmountFunc;
        this.Bought = Bought;
    }

    public double GetFromAmount()
    {
        if(FromAmountFunc != null)
        {
            return FromAmountFunc() * selectedAmount;
        }
        else
        {
            return fromAmount * selectedAmount;
        }
    }

    public double GetToAmount()
    {
        return toAmount * selectedAmount;
    }

    public bool CanTrade()
    {
        return Database.instance.GetMineralAmount(fromMineral) >= GetFromAmount();
    }

    public void Trade()
    {
        if (Database.instance.SubstractMineral(fromMineral, GetFromAmount(), true))
        {
            Database.instance.AddMineral(toMineral, GetToAmount());
            Bought?.Invoke();
        }
    }

    public bool IsInteractivePrice()
    {
        return FromAmountFunc != null;
    }
}