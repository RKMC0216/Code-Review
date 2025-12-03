using System;

public class PowerUp
{
    public PowerUpType powerUpType { get; private set; }
    public double value { get; private set; }
    public double multiplier { get; private set; }
    public bool isMultiplied { get; private set; } = false;

    public PowerUp(PowerUpType powerUpType, double value, double multiplier)
    {
        this.powerUpType = powerUpType;
        this.value = value;
        this.multiplier = multiplier;
    }

    public PowerUp(PowerUpType powerUpType, double value) : 
        this(powerUpType, value, 1)
    {

    }

    public void Multiplied()
    {
        isMultiplied = true;
    }

    public bool IsMultiplyable()
    {
        return multiplier > 1;
    }

    public double ActiveValue()
    {
        return isMultiplied ? MultipliedValue() : value;
    }

    public double MultipliedValue()
    {
        return Math.Floor(value * multiplier);
    }
}