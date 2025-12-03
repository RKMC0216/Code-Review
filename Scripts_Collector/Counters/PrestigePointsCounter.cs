using UnityEngine;
using UnityEngine.UI;

public class PrestigePointsCounter : UpdateCounter
{
    [SerializeField]
    private Image ppImg;

    private void Start()
    {
        // Set prestige points sprite
        ppImg.sprite = Game.GetSpriteForMineral(Mineral.PRESTIGE_POINT);
    }

    protected override string CurrentValue()
    {
        return NumberFormatter.Format(data.prestigePoints);
    }
}