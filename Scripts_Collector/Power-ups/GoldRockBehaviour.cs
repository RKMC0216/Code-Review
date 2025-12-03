using UnityEngine;
using System;

public class GoldRockBehaviour : MonoBehaviour
{
    public Action<Vector2> clicked;

    public void OnGoldRockClicked()
    {
        clicked?.Invoke(transform.localPosition);
        Destroy(gameObject);
    }
}