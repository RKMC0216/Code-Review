using System.Collections;
using UnityEngine;
using System;

public class PopUpBehaviour : MonoBehaviour
{
    [SerializeField]
    private RectTransform contentContainer;

    [HideInInspector]
    public GameObject contentPrefab;
    public Action<bool> callback;
    public bool backgroundClickCloses = false;
    public float closeAfterDelay;
    public Action closeCallback;
    public Action<GameObject> initContent;

    private void Start()
    {
        GameObject content = Instantiate(contentPrefab, contentContainer);

        content.GetComponent<GenericPopUpBehaviour>().callback =
            (bool confirm) =>
        {
            StartCoroutine(Close(confirm));
        };

        initContent?.Invoke(content);

        StartCoroutine(Open());
    }
    
    public void OnBackgroundClicked()
    {
        if(backgroundClickCloses)
        {
            StartCoroutine(Close());
        }
    }

    private IEnumerator Open()
    {
        yield return StartCoroutine(TransformAnimationHelper.Scale(contentContainer, new Vector3(0, 0), new Vector3(1, 1), .2f));

        if(closeAfterDelay > 0)
        {
            StartCoroutine(CloseAfterDelay());
        }
    }

    private IEnumerator Close()
    {
        yield return StartCoroutine(TransformAnimationHelper.Scale(contentContainer, new Vector3(1, 1), new Vector3(0, 0), .2f));
        closeCallback?.Invoke();
        Destroy(gameObject);
    }

    private IEnumerator Close(bool confirmed)
    {
        yield return StartCoroutine(TransformAnimationHelper.Scale(contentContainer, new Vector3(1, 1), new Vector3(0, 0), .2f));
        closeCallback?.Invoke();
        callback?.Invoke(confirmed);
        Destroy(gameObject);
    }

    private IEnumerator CloseAfterDelay()
    {
        yield return new WaitForSeconds(closeAfterDelay);
        StartCoroutine(Close());
    } 
}