using TMPro;
using UnityEngine;

public class FullSensationPlayClick : MonoBehaviour
{
    private TextMeshProUGUI sensationName;
    private OwoSensationBuilderAndTester sensationBuilder;

    void Awake()
    {
        sensationName = GetComponentInChildren<TextMeshProUGUI>();
        sensationBuilder = FindAnyObjectByType<OwoSensationBuilderAndTester>();
    }
    public void PlayOnClick()
    {
        sensationBuilder.PlayFullSensation(sensationName.text);
    }
}
