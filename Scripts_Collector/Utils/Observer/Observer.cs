using System.Collections.Generic;
using UnityEngine;

public abstract class Observer : MonoBehaviour, IObserver
{
    protected abstract List<IObserver> Registry();
    public abstract void OnValueChanged();

    private void Awake()
    {
        Registry().Add(this);
    }

    private void OnDestroy()
    {
        Registry().Remove(this);
    }
}