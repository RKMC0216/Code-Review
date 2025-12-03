using UnityEngine;
using TMPro;

public class PackRubyBehaviour : MonoBehaviour
{
    [SerializeField]
    private TMP_Text amountTxt;

    public SpecialPackContent content;

    private void Start()
    {
        amountTxt.text = "x" + content.value;
    }
}