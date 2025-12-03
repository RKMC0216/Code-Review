using UnityEngine;
using UnityEngine.UI;

public class RockCounter : MultiTextUpdateCounter
{
    [SerializeField]
    private Image rockImg, rockOutlineImg, rockShadowImg;

    private void Start()
    {
        // Set rock sprite and outline sprite
        rockImg.sprite = Game.GetSpriteForMineral(Mineral.ROCK);

        if(rockOutlineImg != null)
            rockOutlineImg.sprite = Game.GetSpriteOutlineForMineral(Mineral.ROCK);

        if(rockShadowImg != null)
            rockShadowImg.sprite = Game.GetSpriteOutlineForMineral(Mineral.ROCK);
    }

    protected override double CurrentValue()
    {
        return data.rocks;
    }
}