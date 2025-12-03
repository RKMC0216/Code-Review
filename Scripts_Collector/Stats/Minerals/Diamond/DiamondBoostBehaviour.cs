using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DiamondBoostBehaviour : MonoBehaviour
{
    [SerializeField]
    private GameObject boost, locked;

    [SerializeField]
    private Image boostImg;

    [SerializeField]
    private TMP_Text boostTxt;

    [HideInInspector]
    public int stage;

    private void Start()
    {
        boostTxt.text = "x" + BoostCollectorBehaviour.boosts[stage];
    }

    public void UpdateAvailability(bool isAvailable, bool isBought, bool isActiveBoost)
    {
        boost.SetActive(isAvailable);
        locked.SetActive(!isAvailable);

        boostImg.color = isAvailable ? isBought ? new Color(1, 1, 1, isActiveBoost ? 1 : .5f) : new Color(0, 0, 0) : new Color(.196f, .196f, .196f);
        boostTxt.color = isAvailable ? isBought ? new Color(.196f, .196f, .196f) : new Color(0, 0, 0) : new Color(.196f, .196f, .196f);
    }
}