public class ProfitBooster
{
    public double profitMultiplier { get; private set; }
    public float maxBoostTime { get; private set; }
    public float boostRechargeTime { get; private set; }

    public ProfitBooster(double profitMultiplier, float maxBoostTime, float boostRechargeTime)
    {
        this.profitMultiplier = profitMultiplier;
        this.maxBoostTime = maxBoostTime;
        this.boostRechargeTime = boostRechargeTime;
    }
}