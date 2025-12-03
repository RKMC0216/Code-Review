using UnityEngine;
using System;

public class GenericPopUpBehaviour : MonoBehaviour
{
    public Action<bool> callback;

    public void OnConfirmButtonClicked()
    {
        callback?.Invoke(true);;
    }

    public void OnCancelButtonClicked()
    {
        callback?.Invoke(false);
    }
}