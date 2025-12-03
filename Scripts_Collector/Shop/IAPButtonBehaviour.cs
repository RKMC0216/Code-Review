using TMPro;
using UnityEngine;
using UnityEngine.Purchasing;

public class IAPButtonBehaviour : MonoBehaviour
{
    [SerializeField]
    private TMP_Text priceTxt;

    public void OnProductFetched(Product product)
    {
        priceTxt.text = product.metadata.localizedPriceString;
    }
}