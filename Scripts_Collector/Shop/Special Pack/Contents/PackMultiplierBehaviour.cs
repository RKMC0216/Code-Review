using UnityEngine;
using TMPro;

public class PackMultiplierBehaviour : MonoBehaviour
{
    [SerializeField]
    private TMP_Text amountTxt, multiplierTxt;

    public SpecialPackContent content;

    private void Start()
    {
        amountTxt.text = "+" + (content.value * 100) + "%";
        multiplierTxt.text = "x" + content.value;
    }
}