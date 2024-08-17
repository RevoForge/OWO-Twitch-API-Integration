using TMPro;
using UnityEngine;

public class SensationLoadClick : MonoBehaviour
{
    private TextMeshProUGUI sensationName;
    private OwoSensationBuilderAndTester sensationBuilder;

    void Awake()
    {
        sensationName = GetComponentInChildren<TextMeshProUGUI>();
        sensationBuilder = FindAnyObjectByType<OwoSensationBuilderAndTester>();
    }
    public void LoadOnClick()
    {
        sensationBuilder.inputField.text = sensationName.text;
        sensationBuilder.LoadFromFile();
    }


}
