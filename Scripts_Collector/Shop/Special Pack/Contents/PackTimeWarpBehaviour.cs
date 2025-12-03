using UnityEngine;
using TMPro;

public class PackTimeWarpBehaviour : MonoBehaviour
{
    [SerializeField]
    private TMP_Text amountTxt, abbreviationTxt;

    public SpecialPackContent content;

    private void Start()
    {
        // Set abbreviation for the time warp
        abbreviationTxt.text = TimeWarpBehaviour.GetAbbreviationForTimeWarp(content.item);
        amountTxt.text = "x" + content.value;
    }
}