using UnityEngine;
using System;

public abstract class GenericMenuItem : MonoBehaviour
{
    [HideInInspector]
    public MenuBehaviour menu;

    public Translation_Inspector title;
    public Fragment[] fragments;
    public int initialFragmentIndex;

    public virtual bool IsAllowedToClose()
    {
        return true;
    }

    public virtual void OnOpened(int fragmentIndex)
    {

    }

    public virtual void OnFragmentSwitched(int index)
    {

    }
}

[Serializable]
public class Fragment
{
    public Translation_Inspector title;
    public Sprite image;
    public GameObject content;
}