using UnityEngine;
using TMPro;

public class SpecialPackPopUpBehaviour : GenericPopUpBehaviour
{
    [SerializeField]
    private TMP_Text bannerTitleTxt;

    [SerializeField]
    private SpecialPackBehaviour specialPackBehaviour;

    void Start()
    {
        bannerTitleTxt.text = SpecialPack.GetActiveSpecialPack().ID.Equals(SpecialPack.STARTER_PACK) ?
            Translator.GetTranslationForId(Translation_Script.ONE_TIME_OFFER)
            :
            Translator.GetTranslationForId(Translation_Script.LIMITED_TIME_OFFER);

        specialPackBehaviour.destroyWhenBought = false;
        specialPackBehaviour.boughtCallback = OnConfirmButtonClicked;
        specialPackBehaviour.offerExpiredCallback = OnCancelButtonClicked;
    }
}