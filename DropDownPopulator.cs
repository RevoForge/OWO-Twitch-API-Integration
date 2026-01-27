using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;

public class DropdownPopulator : MonoBehaviour
{
    public bool microSensationOnly = false;
    private TMP_Dropdown dropdown;
    private int sentDropdownValue;
    private bool loadedValue = false;
    private bool triedtoload = false;
    private List<string> files = new List<string>();
    private readonly string directoryPath1 = Path.Combine("Assets", "OWO", "Sensation Events");
    private readonly string directoryPath2 = Path.Combine("Assets", "OWO", "MicroSensation Events");
    private List<string> cachedFiles = new();
    private float fileCheckTimer;
    private const float FILE_CHECK_INTERVAL = 1f;

    void Start()
    {
        dropdown = GetComponent<TMP_Dropdown>();
        PopulateDropdownWithFilenames();
    }

    public void PopulateDropdownWithFilenames()
    {
        if (microSensationOnly)
        {
            files = GetFilesFromDirectorie(directoryPath2);
        }
        else
        {
            files = GetFilesFromDirectories(directoryPath1, directoryPath2);
        }
        cachedFiles = files.ToList();
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
        bool dropdownUpdated = CheckAndUpdateFiles();

        if (dropdownUpdated)
        {
            TryApplyLoadedValue();
        }
    }
    private bool CheckAndUpdateFiles()
    {
        fileCheckTimer += Time.deltaTime;
        if (fileCheckTimer < FILE_CHECK_INTERVAL)
            return false;

        fileCheckTimer = 0f;

        var currentFiles = microSensationOnly
            ? GetFilesFromDirectories(directoryPath1, directoryPath2)
            : GetFilesFromDirectorie(directoryPath2);

        if (cachedFiles.SequenceEqual(currentFiles))
            return false;

        cachedFiles = currentFiles;
        PopulateDropdownWithFilenames();
        loadedValue = false; // force reload after repopulating
        return true;
    }
    private void TryApplyLoadedValue()
    {
        if (dropdown == null || !triedtoload || loadedValue)
            return;

        dropdown.value = sentDropdownValue;
        loadedValue = true;
        triedtoload = false;
    }
    private List<string> GetFilesFromDirectories(string directoryPath1, string directoryPath2)
    {
        List<string> filesList = new List<string>();

        CheckAndAddFiles(filesList, directoryPath1);
        CheckAndAddFiles(filesList, directoryPath2);

        return filesList;
    }
    private List<string> GetFilesFromDirectorie(string directoryPath)
    {
        List<string> filesList = new();
        CheckAndAddFiles(filesList, directoryPath);
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
