using UnityEngine;
using UnityEngine.UI;

public class PrestigePointsMultiTextCounter : MultiTextUpdateCounter
{
    [SerializeField]
    private Image ppImg;

    private void Start()
    {
        // Set prestige points sprite
        ppImg.sprite = Game.GetSpriteForMineral(Mineral.PRESTIGE_POINT);
    }

    protected override double CurrentValue()
    {
        return data.prestigePoints;
    }
}