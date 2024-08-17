using TMPro;
using UnityEngine;

public class SensationAppendclick : MonoBehaviour
{
    private TextMeshProUGUI sensationName;
    private OwoSensationBuilderAndTester sensationBuilder;

    private void OnEnable()
    {
        sensationName = GetComponentInChildren<TextMeshProUGUI>();
        sensationBuilder = FindAnyObjectByType<OwoSensationBuilderAndTester>();
    }
    public void AddToAppend()
    {
        sensationBuilder.AppendClickedObjects(sensationName.text);
    }
}
