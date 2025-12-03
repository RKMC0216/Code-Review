using UnityEngine;
using TMPro;

public abstract class ObservingCounter : Observer
{
    [SerializeField]
    private TMP_Text text;

    protected abstract string CurrentValue();

    private void Start()
    {
        UpdateCounterValue();
    }

    private void UpdateCounterValue()
    {
        text.text = CurrentValue();
    }

    public override void OnValueChanged()
    {
        UpdateCounterValue();
    }
}