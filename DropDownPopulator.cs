using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;

public class DropdownPopulator : MonoBehaviour
{
    private TMP_Dropdown dropdown;
    private int sentDropdownValue;
    private bool loadedValue = false;
    private bool triedtoload = false;
    private List<string> OldfilesList = new List<string>();
    private List<string> files = new List<string>();
    private readonly string directoryPath1 = Path.Combine("Assets", "OWO", "Sensation Events");
    private readonly string directoryPath2 = Path.Combine("Assets", "OWO", "MicroSensation Events");

    void Start()
    {
        dropdown = GetComponent<TMP_Dropdown>();
        PopulateDropdownWithFilenames();
    }

    public void PopulateDropdownWithFilenames()
    {
        files = GetFilesFromDirectories(directoryPath1, directoryPath2);
        OldfilesList = files.ToList();

        // Clear current options in the dropdown
        dropdown.options.Clear();

        // If no files found, handle that scenario
        if (files.Count == 0)
        {
            dropdown.options.Add(new TMP_Dropdown.OptionData("No files found"));
            Debug.LogError("No files found in both directories");
        }
        else
        {
            // Add each filename as a new dropdown option
            dropdown.options.AddRange(files
                .Where(file => file.Length > 0)
                .Select(file => new TMP_Dropdown.OptionData(file)));
        }

        dropdown.RefreshShownValue();
    }
    public void LoadDropdownValue(int sentValue)
    {
        triedtoload=true;
        sentDropdownValue = sentValue;
    }
    private void Update()
    {
        if (!OldfilesList.SequenceEqual(GetFilesFromDirectories(directoryPath1, directoryPath2)))
        {
            PopulateDropdownWithFilenames();
        }
        if (dropdown != null && triedtoload)
        {
            if (dropdown.value != sentDropdownValue && !loadedValue)
            {
                dropdown.value = sentDropdownValue;
                loadedValue = true;
            }
            triedtoload = false;
        }
    }

    private List<string> GetFilesFromDirectories(string directoryPath1, string directoryPath2)
    {
        List<string> filesList = new List<string>();

        CheckAndAddFiles(filesList, directoryPath1);
        CheckAndAddFiles(filesList, directoryPath2);

        return filesList;
    }

    private void CheckAndAddFiles(List<string> filesList, string directoryPath)
    {
        if (Directory.Exists(directoryPath))
        {
            filesList.AddRange(Directory.GetFiles(directoryPath, "*.json")
                                        .Select(Path.GetFileNameWithoutExtension)
                                        .ToList());
        }
        else
        {
            Debug.LogError("Directory does not exist: " + directoryPath);
        }
    }
}
