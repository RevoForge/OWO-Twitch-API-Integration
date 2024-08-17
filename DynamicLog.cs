using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DynamicLog : MonoBehaviour
{
    public TMP_Text logText;
    public ScrollRect scrollRect;
    public float entrySpacing = 0.5f; // Spacing between entries

    private float preferredHeight;
    private RectTransform rectTransform;

    private void Start()
    {
        rectTransform = logText.GetComponent<RectTransform>();
    }

    public void AddEntry(string entry)
    {
        logText.text += entry + "\n";
        AdjustHeight();
        StartCoroutine(ScrollToBottom());
    }

    private void AdjustHeight()
    {
        preferredHeight = logText.preferredHeight + entrySpacing;
        Vector2 oldSizeDelta = rectTransform.sizeDelta;
        rectTransform.sizeDelta = new Vector2(oldSizeDelta.x, preferredHeight);
    }


    // Coroutine to ensure we scroll after the UI updates
    private IEnumerator ScrollToBottom()
    {
        yield return new WaitForEndOfFrame();
        scrollRect.normalizedPosition = new Vector2(0, 0);
    }
}
