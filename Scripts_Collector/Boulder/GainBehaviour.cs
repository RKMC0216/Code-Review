using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using TMPro;

public class GainBehaviour : MonoBehaviour
{
    [SerializeField]
    private RectTransform rect;

    [SerializeField]
    private TMP_Text text;

    [HideInInspector]
    public Queue<GameObject> pool;

    public void Spawn(double value)
    {
        text.text = "+" + NumberFormatter.Format(value);
        StartCoroutine(Move());
    }

    private IEnumerator Move()
    {
        yield return StartCoroutine(TransformAnimationHelper.MoveAnchoredPosition(rect, rect.anchoredPosition + new Vector2(0, 150), rect.anchoredPosition + new Vector2(0, 300), 1));
        
        if(pool != null)
        {
            gameObject.SetActive(false);
            pool.Enqueue(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}