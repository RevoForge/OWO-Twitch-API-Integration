using Michsky.UI.Shift;
using OWOGame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class OwoSensationBuilderAndTester : MonoBehaviour
{

    public enum MuscleGroup
    {
        FrontMuscles,
        BackMuscles,
        PectoralL,
        PectoralR,
        DorsalL,
        DorsalR,
        ArmL,
        ArmR,
        LumbarL,
        LumbarR,
        AbdominalL,
        AbdominalR
    }


    [System.Serializable]
    public class MuscleData
    {
        public bool useOveride = false;
        public int intensityOveride = 100;
    }
    [System.Serializable]
    public class AppendedMicroSensations
    {
        public string data;
    }

    private MicroSensation startup;
    private MicroSensation sensationEvent;
    [Header("MicroSensation File Names to Append ")]
    public List<string> fileNames;
    [Header("Appended File Name To Save or Load")]
    public string appendFileName;

    [Header("File Settings")]
    public TextMeshProUGUI sensationName;
    public TextMeshProUGUI appendName;
    public TMP_InputField inputField;

    //[Header("MicroSensation Settings")]
    private int frequency = 100;
    private float duration = 1.0f;
    private int intensityPercentage = 25;
    private float rampUpInMills = 0f;
    private float rampDownInMills = 0f;
    private float exitDelay = 0f;

    //[Header("Front Muscles")]
    [HideInInspector]
    public MuscleData pectoralR;
    [HideInInspector]
    public MuscleData pectoralL;
    [HideInInspector]
    public MuscleData abdominalR;
    [HideInInspector]
    public MuscleData abdominalL;
    [HideInInspector]
    public MuscleData armR;
    [HideInInspector]
    public MuscleData armL;

    //[Header("Back Muscles")]
    [HideInInspector]
    public MuscleData dorsalR;
    [HideInInspector]
    public MuscleData dorsalL;
    [HideInInspector]
    public MuscleData lumbarR;
    [HideInInspector]
    public MuscleData lumbarL;

    private bool prevBackMuscles = false;
    private bool prevFrontMuscles = false;
    private bool prevFrontMuscleCheck = false;
    private bool prevBackMuscleCheck = false;
    [HideInInspector]
    public string[] availableFiles;
    [HideInInspector]
    public string[] availableFiles2;
    [HideInInspector]
    public string[] AppendavailableFiles;
    [HideInInspector]
    public string[] AppendavailableFiles2;
    // UI References
    public Slider durationSlider;
    public Slider frequancySlider;
    public Slider intensitySlider;
    public Slider rampUpSlider;
    public Slider rampDownSlider;
    public Slider exitDelaySlider;
    public Slider PectoralLSlider;
    public Slider PectoralRSlider;
    public Slider DorsalLSlider;
    public Slider DorsalRSlider;
    public Slider ArmLSlider;
    public Slider ArmRSlider;
    public Slider LumbarLSlider;
    public Slider LumbarRSlider;
    public Slider AbdominalLSlider;
    public Slider AbdominalRSlider;
    public Button musclesFrontB;
    public Button musclesBackB;
    public Button musclesPectoralLb;
    public Button musclesPectoralRb;
    public Button musclesDorsalRb;
    public Button musclesDorsalLb;
    public Button musclesArmLb;
    public Button musclesArmRb;
    public Button musclesLumbarLb;
    public Button musclesLumbarRb;
    public Button musclesAbdominalLb;
    public Button musclesAbdominalRb;
    public Button musclesPectoralLO;
    public Button musclesPectoralRO;
    public Button musclesDorsalRO;
    public Button musclesDorsalLO;
    public Button musclesArmLO;
    public Button musclesArmRO;
    public Button musclesLumbarLO;
    public Button musclesLumbarRO;
    public Button musclesAbdominalLO;
    public Button musclesAbdominalRO;
    public TimedEvent startupEvent;
    public SplashScreenManager splashScreenManager;

    private Dictionary<MuscleGroup, bool> muscleStatus = new();
    // Variables for loading names into GameObjects
    public GameObject FileNamePrefab;
    public Transform parentTransform;
    public GameObject referenceSibling;
    private HashSet<string> instantiatedFiles = new();

    public Transform parentTransform2;
    public GameObject FileNamePrefab2;
    public GameObject referenceSibling2;
    private HashSet<string> instantiatedFiles2 = new();

    // Variables for appending names to GameObjects
    public GameObject AppendFileNamePrefab;
    public Transform AppendparentTransform;
    public GameObject AppendreferenceSibling;
    private HashSet<string> AppendinstantiatedFiles = new();

    // Variables for appending clicked objects
    public GameObject ActiveAppendFileNamePrefab;
    public Transform ActiveAppendparentTransform;
    private string FilesToAppend = "";
    private List<GameObject> activeAppendedFiles = new();
    private List<string> loadedJsonContents = new();

    private Dictionary<int, MuscleGroup> muscleIdMap = new Dictionary<int, MuscleGroup>
    {
        {1, MuscleGroup.PectoralL},
        {0, MuscleGroup.PectoralR},
        {7, MuscleGroup.DorsalL},
        {6, MuscleGroup.DorsalR},
        {5, MuscleGroup.ArmL},
        {4, MuscleGroup.ArmR},
        {9, MuscleGroup.LumbarL},
        {8, MuscleGroup.LumbarR},
        {3, MuscleGroup.AbdominalL},
        {2, MuscleGroup.AbdominalR}
    };
    private delegate Muscle OverrideMuscleFunc();


    private void Start()
    {
        foreach (MuscleGroup muscle in Enum.GetValues(typeof(MuscleGroup)))
        {
            muscleStatus[muscle] = false;
        }
        intensitySlider.value = intensityPercentage;
        frequancySlider.value = frequency;
        rampUpSlider.value = rampUpInMills;
        rampDownSlider.value = rampDownInMills;
        durationSlider.value = duration;
        exitDelaySlider.value = exitDelay;

        // "Sartup Sensation" I added For Debugging
        startup = SensationsFactory.Create(100, 1, 25, 1, 1, 0);
        RefreshAvailableFiles();
    }

    public void Intensitysliderchange()
    {
        intensityPercentage = (int)intensitySlider.value;
    }
    public void Frequancysliderchange()
    {
        frequency = (int)frequancySlider.value;
    }
    public void RampUpSliderChange()
    {
        rampUpInMills = Mathf.Round(rampUpSlider.value * 10f) / 10f;
    }
    public void RampDownSliderChange()
    {
        rampDownInMills = Mathf.Round(rampDownSlider.value * 10f) / 10f;
    }
    public void DurationChange()
    {
        duration = durationSlider.value;
    }
    public void ExitDelayChange()
    {
        exitDelay = exitDelaySlider.value;
    }
    public void PectoralLChange()
    {
        pectoralL.intensityOveride = (int)PectoralLSlider.value;
    }
    public void PectoralRChange()
    {
        pectoralR.intensityOveride = (int)PectoralRSlider.value;
    }
    public void DorsalLChange()
    {
        dorsalL.intensityOveride = (int)DorsalLSlider.value;
    }
    public void DorsalRChange()
    {
        dorsalR.intensityOveride = (int)DorsalRSlider.value;
    }
    public void ArmLChange()
    {
        armL.intensityOveride = (int)ArmLSlider.value;
    }
    public void ArmRChange()
    {
        armR.intensityOveride = (int)ArmRSlider.value;
    }
    public void LumbarLChange()
    {
        lumbarL.intensityOveride = (int)LumbarLSlider.value;
    }
    public void LumbarRChange()
    {
        lumbarR.intensityOveride = (int)LumbarRSlider.value;
    }
    public void AbdominalLChange()
    {
        abdominalL.intensityOveride = (int)AbdominalLSlider.value;
    }
    public void AbdominalRChange()
    {
        abdominalR.intensityOveride = (int)AbdominalRSlider.value;
    }

    public void ToggleMuscleStatus(string actionString)
    {
        var parts = actionString.Split('_');
        if (parts.Length != 2)
        {
            Debug.LogError($"Invalid action string: {actionString}");
            return;
        }

        var muscleGroupName = parts[0];
        var action = parts[1];

        if (Enum.TryParse(muscleGroupName, out MuscleGroup group))
        {
            switch (action)
            {
                case "ON":
                    muscleStatus[group] = true;
                    break;
                case "OFF":
                    muscleStatus[group] = false;
                    break;
                default:
                    Debug.LogError($"Invalid action: {action}");
                    break;
            }
        }
        else
        {
            Debug.LogError($"Invalid muscle group string: {muscleGroupName}");
        }
    }

    public void ToggleMuscleOverride(string actionString)
    {
        var parts = actionString.Split('_');
        if (parts.Length != 2)
        {
            Debug.LogError($"Invalid action string: {actionString}");
            return;
        }

        var muscleName = parts[0];
        var action = parts[1];

        // Define a dictionary to map muscle names to their respective objects
        Dictionary<string, MuscleData> muscleMapping = new()
        {
        { "pectoralR", pectoralR },
        { "pectoralL", pectoralL },
        { "abdominalR", abdominalR },
        { "abdominalL", abdominalL },
        { "armR", armR },
        { "armL", armL },
        { "dorsalR", dorsalR },
        { "dorsalL", dorsalL },
        { "lumbarR", lumbarR },
        { "lumbarL", lumbarL }
    };

        // Check if the muscle name is valid and retrieve the corresponding muscle object
        if (!muscleMapping.TryGetValue(muscleName, out var muscle))
        {
            Debug.LogError($"Invalid muscle string: {muscleName}");
            return;
        }

        // Toggle the muscle's override based on the action
        switch (action)
        {
            case "ON":
                muscle.useOveride = true;
                break;
            case "OFF":
                muscle.useOveride = false;
                break;
            default:
                Debug.LogError($"Invalid action: {action}");
                return;
        }
    }

    private void Update()
    {
        // If backMuscles is true, the back individual muscles should be false
        if (muscleStatus[MuscleGroup.BackMuscles] && !prevBackMuscles)
        {
            ClearSpecificBackMuscles();
            prevBackMuscleCheck = IsSpecificBackMuscleActive();
        }
        // If frontMuscles is true, the front individual muscles should be false
        if (muscleStatus[MuscleGroup.FrontMuscles] && !prevFrontMuscles)
        {
            ClearSpecificFrontMuscles();
            prevFrontMuscleCheck = IsSpecificFrontMuscleActive();
        }
        // If any specific back muscle is active, clear the backMuscles flag
        if (IsSpecificBackMuscleActive() != prevBackMuscleCheck)
        {
            if (muscleStatus[MuscleGroup.BackMuscles]) musclesBackB.onClick.Invoke();
            prevBackMuscleCheck = IsSpecificBackMuscleActive();
        }
        // If any specific front muscle is active, clear the frontMuscles flag
        if (IsSpecificFrontMuscleActive() != prevFrontMuscleCheck)
        {
            if (muscleStatus[MuscleGroup.FrontMuscles]) musclesFrontB.onClick.Invoke();
            prevFrontMuscleCheck = IsSpecificFrontMuscleActive();
        }
        prevBackMuscles = muscleStatus[MuscleGroup.BackMuscles];
        prevFrontMuscles = muscleStatus[MuscleGroup.FrontMuscles];
    }

    private void ClearMuscleStatusIfActive(MuscleGroup muscleGroup, Button associatedButton)
    {
        if (muscleStatus[muscleGroup])
        {
            muscleStatus[muscleGroup] = false;
            associatedButton.onClick.Invoke();
        }
    }

    private void ClearSpecificBackMuscles()
    {
        ClearMuscleStatusIfActive(MuscleGroup.DorsalL, musclesDorsalLb);
        ClearMuscleStatusIfActive(MuscleGroup.DorsalR, musclesDorsalRb);
        ClearMuscleStatusIfActive(MuscleGroup.LumbarL, musclesLumbarLb);
        ClearMuscleStatusIfActive(MuscleGroup.LumbarR, musclesLumbarRb);
    }

    private void ClearSpecificFrontMuscles()
    {
        ClearMuscleStatusIfActive(MuscleGroup.PectoralL, musclesPectoralLb);
        ClearMuscleStatusIfActive(MuscleGroup.PectoralR, musclesPectoralRb);
        ClearMuscleStatusIfActive(MuscleGroup.AbdominalL, musclesAbdominalLb);
        ClearMuscleStatusIfActive(MuscleGroup.AbdominalR, musclesAbdominalRb);
        ClearMuscleStatusIfActive(MuscleGroup.ArmL, musclesArmLb);
        ClearMuscleStatusIfActive(MuscleGroup.ArmR, musclesArmRb);
    }

    private bool IsSpecificBackMuscleActive()
    {
        return muscleStatus[MuscleGroup.DorsalL] || muscleStatus[MuscleGroup.DorsalR] || muscleStatus[MuscleGroup.LumbarR] || muscleStatus[MuscleGroup.LumbarL];
    }

    private bool IsSpecificFrontMuscleActive()
    {
        return muscleStatus[MuscleGroup.PectoralR] || muscleStatus[MuscleGroup.PectoralL] || muscleStatus[MuscleGroup.AbdominalR] || muscleStatus[MuscleGroup.AbdominalL] || muscleStatus[MuscleGroup.ArmL] || muscleStatus[MuscleGroup.ArmR];
    }

    public void SendHapticButtonPressed()
    {
        SendHapticEventBasedOnMuscles();
    }

    public void StopHapticButtonPressed()
    {
        OWO.Stop();
    }

    private Muscle[] GetSelectedMuscles()
    {
        List<Muscle> muscles = new();

        if (muscleStatus[MuscleGroup.FrontMuscles])
        {
            AddMuscles(muscles, MuscleGroup.FrontMuscles, Muscle.Front);
        }
        else
        {
            AddMuscle(muscles, MuscleGroup.PectoralL, Muscle.Pectoral_L, () =>
                pectoralL.useOveride ? Muscle.Pectoral_L.WithIntensity(pectoralL.intensityOveride) : Muscle.Pectoral_L);

            AddMuscle(muscles, MuscleGroup.PectoralR, Muscle.Pectoral_R, () =>
                pectoralR.useOveride ? Muscle.Pectoral_R.WithIntensity(pectoralR.intensityOveride) : Muscle.Pectoral_R);

            AddMuscle(muscles, MuscleGroup.AbdominalL, Muscle.Abdominal_L, () =>
                abdominalL.useOveride ? Muscle.Abdominal_L.WithIntensity(abdominalL.intensityOveride) : Muscle.Abdominal_L);

            AddMuscle(muscles, MuscleGroup.AbdominalR, Muscle.Abdominal_R, () =>
                abdominalR.useOveride ? Muscle.Abdominal_R.WithIntensity(abdominalR.intensityOveride) : Muscle.Abdominal_R);

            AddMuscle(muscles, MuscleGroup.ArmL, Muscle.Arm_L, () =>
                armL.useOveride ? Muscle.Arm_L.WithIntensity(armL.intensityOveride) : Muscle.Arm_L);

            AddMuscle(muscles, MuscleGroup.ArmR, Muscle.Arm_R, () =>
                armR.useOveride ? Muscle.Arm_R.WithIntensity(armR.intensityOveride) : Muscle.Arm_R);
        }
        if (muscleStatus[MuscleGroup.BackMuscles])
        {
            AddMuscles(muscles, MuscleGroup.BackMuscles, Muscle.Back);
        }
        else
        {
            AddMuscle(muscles, MuscleGroup.DorsalL, Muscle.Dorsal_L, () =>
                dorsalL.useOveride ? Muscle.Dorsal_L.WithIntensity(dorsalL.intensityOveride) : Muscle.Dorsal_L);

            AddMuscle(muscles, MuscleGroup.DorsalR, Muscle.Dorsal_R, () =>
                dorsalR.useOveride ? Muscle.Dorsal_R.WithIntensity(dorsalR.intensityOveride) : Muscle.Dorsal_R);

            AddMuscle(muscles, MuscleGroup.LumbarL, Muscle.Lumbar_L, () =>
                lumbarL.useOveride ? Muscle.Lumbar_L.WithIntensity(lumbarL.intensityOveride) : Muscle.Lumbar_L);

            AddMuscle(muscles, MuscleGroup.LumbarR, Muscle.Lumbar_R, () =>
                lumbarR.useOveride ? Muscle.Lumbar_R.WithIntensity(lumbarR.intensityOveride) : Muscle.Lumbar_R);
        }
        return muscles.ToArray();
    }

    private void AddMuscles(List<Muscle> muscles, MuscleGroup group, Muscle[] musclesToAdd)
    {
        if (muscleStatus[group])
        {
            muscles.AddRange(musclesToAdd);
        }
    }

    private void AddMuscle(List<Muscle> muscles, MuscleGroup group, Muscle defaultMuscle, OverrideMuscleFunc overrideFunc)
    {
        if (muscleStatus[group])
        {
            muscles.Add(overrideFunc());
        }
    }

    /* For Use With Mulitple Suits
    private void CheckConnection()
    {
        if (OWO.ConnectionState != ConnectionState.Connected)
        {
            if (OWO.DiscoveredApps.Length >= 1)
            {
                OWO.Connect(OWO.DiscoveredApps);  
                startupEvent.ConnectedBypass();
                StartCoroutine(StartupPulse());
            }
            else
            {
                splashScreenManager.FailedConnection();
            }
        }
    }
    */
    public async void InitializeOWO()
    {
        /* 
        // For Use With Mulitple Suits Scans for 5 seconds then connects all it finds different suits must be on a different IP
        Debug.Log("Initializing suit");
        OWO.StartScan();
        Invoke(nameof(CheckConnection), 5f);
        */

        // This is not needed when trying multiple suits
        var cts = new CancellationTokenSource();
        _ = OWO.AutoConnect();
        var timeoutTask = Task.Delay(TimeSpan.FromSeconds(10), cts.Token);
        while (OWO.ConnectionState != ConnectionState.Connected && !timeoutTask.IsCompleted)
        {
            await Task.Delay(100);
        }
        if (OWO.ConnectionState == ConnectionState.Connected)
        {
            cts.Cancel();
            startupEvent.ConnectedBypass();
            StartCoroutine(StartupPulse());
        }
        else
        {
            splashScreenManager.FailedConnection();
        }
        // ----------------------------------------------
    }

    private IEnumerator StartupPulse()
    {
        yield return new WaitForSeconds(1f);
        Debug.Log("Startup Pulse");
        float elapsedTime = 0f;
        const float timeInterval = 1f;

        while (elapsedTime < 3f)
        {
            OWO.Send(startup.WithMuscles(Muscle.All));
            yield return new WaitForSeconds(timeInterval);
            elapsedTime += timeInterval;
        }
    }

    private void SendHapticEventBasedOnMuscles()
    {
        sensationEvent = SensationsFactory.Create(frequency, duration, intensityPercentage, rampUpInMills, rampDownInMills, exitDelay);
        OWO.Send(sensationEvent.WithMuscles(GetSelectedMuscles().ToArray()));
    }

    public void RefreshAvailableFiles()
    {
        string directoryPath = "Assets/OWO/MicroSensation Events";
        if (Directory.Exists(directoryPath))
        {
            availableFiles = Directory.GetFiles(directoryPath, "*.json").Select(Path.GetFileNameWithoutExtension).ToArray();
        }
        else
        {
            availableFiles = new string[0];
        }
        string directoryPath2 = "Assets/OWO/Sensation Events";
        if (Directory.Exists(directoryPath2))
        {
            availableFiles2 = Directory.GetFiles(directoryPath2, "*.json").Select(Path.GetFileNameWithoutExtension).ToArray();
        }
        else
        {
            availableFiles2 = new string[0];
        }
        ProcessNames(availableFiles2, FileNamePrefab2, referenceSibling2, parentTransform2, instantiatedFiles2);
        ProcessNames(availableFiles, FileNamePrefab, referenceSibling, parentTransform, instantiatedFiles);
        ProcessNames(availableFiles, AppendFileNamePrefab, AppendreferenceSibling, AppendparentTransform, AppendinstantiatedFiles);
    }


    public void AppendClickedObjects(string filenametoadd)
    {
        FilesToAppend = string.IsNullOrEmpty(FilesToAppend) ? filenametoadd : $"{FilesToAppend},{filenametoadd}";
        CreateAppendedGameObject(filenametoadd);
    }

    public void ClearAppendedObjects()
    {
        foreach (var obj in activeAppendedFiles)
        {
            Destroy(obj);
        }
        FilesToAppend = "";
        activeAppendedFiles.Clear();
    }

    public void SaveAppendedFilesString()
    {
        ProcessAppendedFilesString("save");
    }

    public void TestAppendedFilesString()
    {
        ProcessAppendedFilesString("test");
    }

    private void ProcessNames(IEnumerable<string> files, GameObject prefab, GameObject sibling, Transform parentTransform, HashSet<string> instantiatedSet)
    {
        int insertionIndex = sibling.transform.GetSiblingIndex() + 1;

        foreach (string fileName in files)
        {
            if (fileName.Length > 0)
            {
                if (instantiatedSet.Contains(fileName))
                {
                    continue;
                }

                GameObject newFileObject = Instantiate(prefab, parentTransform);
                SetupGameObjectText(newFileObject, fileName);
                newFileObject.transform.SetSiblingIndex(insertionIndex);
                insertionIndex++;
                instantiatedSet.Add(fileName);
            }
        }
    }

    private void SetupGameObjectText(GameObject obj, string text)
    {
        TextMeshProUGUI tmp = obj.GetComponentInChildren<TextMeshProUGUI>();

        if (tmp != null)
        {
            tmp.text = text.Trim();
        }
        else
        {
            Debug.LogError("No TextMeshProUGUI component found on the prefab.");
        }
    }

    private void CreateAppendedGameObject(string filename)
    {
        GameObject newFileObject = Instantiate(ActiveAppendFileNamePrefab, ActiveAppendparentTransform);
        SetupGameObjectText(newFileObject, filename);
        activeAppendedFiles.Add(newFileObject);
    }

    private void ProcessAppendedFilesString(string action)
    {
        if (FilesToAppend.Length == 0)
        {
            Debug.Log("No Files To Append");
            return;
        }

        FilesToAppend = FilesToAppend.TrimEnd(',');

        string[] names = FilesToAppend.Split(',');
        foreach (string name in names)
        {
            string directoryPath = "Assets/OWO/MicroSensation Events";
            if (!Directory.Exists(directoryPath))
            {
                Debug.Log("Folder Not Found");
                return;
            }

            string path = Path.Combine(directoryPath, name + ".json");
            if (File.Exists(path))
            {
                string jsonData = File.ReadAllText(path);
                AppendedMicroSensations sensationFromJson = JsonUtility.FromJson<AppendedMicroSensations>(jsonData);
                loadedJsonContents.Add(sensationFromJson.data);
            }
            else
            {
                Debug.Log($"File {path} does not exist.");
            }
        }

        string combinedData = string.Join("&", loadedJsonContents);
        loadedJsonContents.Clear();
        if (action == "save")
        {
            AppendedMicroSensations combinedSensation = new AppendedMicroSensations { data = combinedData };
            string combinedJson = JsonUtility.ToJson(combinedSensation, true);

            string directoryPath2 = "Assets/OWO/Sensation Events";
            if (!Directory.Exists(directoryPath2))
            {
                Directory.CreateDirectory(directoryPath2);
            }
            char[] charsToTrim = { '\u200B', ' ', '\t', '\n', '\r' };
            string filename = appendName.text.TrimEnd(charsToTrim);
            string outputPath = Path.Combine(directoryPath2, filename + ".json");

            File.WriteAllText(outputPath, combinedJson);
            Debug.Log($"Saved To {outputPath}");

            FilesToAppend = "";
            ClearAppendedObjects();
            RefreshAvailableFiles();
        }
        else if (action == "test")
        {
            OWO.Send(Sensation.Parse(combinedData));
        }
    }
    public void PlayFullSensation(string filename)
    {
        string directoryPath = "Assets/OWO/Sensation Events";
        if (!Directory.Exists(directoryPath))
        {
            Debug.Log("Folder Not Found");
            return;
        }

        string path = Path.Combine(directoryPath, filename + ".json");
        if (File.Exists(path))
        {
            string jsonData = File.ReadAllText(path);
            AppendedMicroSensations sensationFromJson = JsonUtility.FromJson<AppendedMicroSensations>(jsonData);
            OWO.Send(Sensation.Parse(sensationFromJson.data));
        }
        else
        {
            Debug.Log($"File {path} does not exist.");
        }
    }

    public void SaveHapticEvent()
    {
        sensationEvent = SensationsFactory.Create(frequency, duration, intensityPercentage, rampUpInMills, rampDownInMills, exitDelay);
        AppendedMicroSensations sensationtojson = new()
        {
            data = sensationEvent.WithMuscles(GetSelectedMuscles().ToArray())
        };

        string directoryPath = "Assets/OWO/MicroSensation Events";
        if (!Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }
        char[] charsToTrim = { '\u200B', ' ', '\t', '\n', '\r' };
        string filename = sensationName.text.TrimEnd(charsToTrim);
        string path = Path.Combine(directoryPath, filename + ".json");
        string jsonData = JsonUtility.ToJson(sensationtojson, true);
        File.WriteAllText(path, jsonData);
        Debug.Log($"Saved To {path}");
        RefreshAvailableFiles();
    }

    public void LoadFromFile()
    {
        StartCoroutine(LoadingFromFile());
    }
    private void SetSliderValues(Slider slider, MuscleData customObject)
    {
        if (slider.value != 100)
        {
            slider.value = 100;
            customObject.intensityOveride = 100;
        }
    }
    private void InvokeStatusAndOverride(bool statusCondition, Button buttonToInvoke, MuscleData muscleObject, Button overrideButtonToInvoke)
    {
        if (statusCondition)
        {
            buttonToInvoke.onClick.Invoke();
        }
        if (muscleObject.useOveride)
        {
            overrideButtonToInvoke.onClick.Invoke();
        }
    }
    private void HandleMuscle(Button muscleButton, Slider muscleSlider, MuscleData muscleObject, Button overrideButton, int intensity)
    {
        muscleButton.onClick.Invoke();
        muscleSlider.value = muscleObject.intensityOveride = intensity;

        if (intensity < 100 && !muscleObject.useOveride)
        {
            overrideButton.onClick.Invoke();
        }
    }

    public IEnumerator LoadingFromFile()
    {
        SetSliderValues(AbdominalRSlider, abdominalL);
        SetSliderValues(AbdominalLSlider, abdominalR);
        SetSliderValues(LumbarRSlider, lumbarL);
        SetSliderValues(LumbarLSlider, lumbarR);
        SetSliderValues(ArmRSlider, armL);
        SetSliderValues(ArmLSlider, armR);
        SetSliderValues(DorsalRSlider, dorsalL);
        SetSliderValues(DorsalLSlider, dorsalR);
        SetSliderValues(PectoralRSlider, pectoralL);
        SetSliderValues(PectoralLSlider, pectoralR);
        InvokeStatusAndOverride(muscleStatus[MuscleGroup.AbdominalR], musclesAbdominalRb, abdominalR, musclesAbdominalRO);
        InvokeStatusAndOverride(muscleStatus[MuscleGroup.AbdominalL], musclesAbdominalLb, abdominalL, musclesAbdominalLO);
        InvokeStatusAndOverride(muscleStatus[MuscleGroup.LumbarR], musclesLumbarRb, lumbarR, musclesLumbarRO);
        InvokeStatusAndOverride(muscleStatus[MuscleGroup.LumbarL], musclesLumbarLb, lumbarL, musclesLumbarLO);
        InvokeStatusAndOverride(muscleStatus[MuscleGroup.ArmR], musclesArmRb, armR, musclesArmRO);
        InvokeStatusAndOverride(muscleStatus[MuscleGroup.ArmL], musclesArmLb, armL, musclesArmLO);
        InvokeStatusAndOverride(muscleStatus[MuscleGroup.DorsalR], musclesDorsalRb, dorsalR, musclesDorsalRO);
        InvokeStatusAndOverride(muscleStatus[MuscleGroup.DorsalL], musclesDorsalLb, dorsalL, musclesDorsalLO);
        InvokeStatusAndOverride(muscleStatus[MuscleGroup.PectoralR], musclesPectoralRb, pectoralR, musclesPectoralRO);
        InvokeStatusAndOverride(muscleStatus[MuscleGroup.PectoralL], musclesPectoralLb, pectoralL, musclesPectoralLO);

        yield return new WaitForSeconds(0.5f);

        string directoryPath = "Assets/OWO/MicroSensation Events";
        if (!Directory.Exists(directoryPath))
        {
            Debug.Log("Folder Not Found");
            yield break;
        }

        char[] charsToTrim = { '\u200B', ' ', '\t', '\n', '\r' };
        string trimmedInput = inputField.text.TrimEnd(charsToTrim);
        string path = Path.Combine(directoryPath, trimmedInput + ".json");

        if (File.Exists(path))
        {
            string jsonData = File.ReadAllText(path);
            AppendedMicroSensations sensationFromJson = JsonUtility.FromJson<AppendedMicroSensations>(jsonData);

            string[] parts = sensationFromJson.data.Split('|');
            string sensationsData = parts[0];

            try
            {
                string[] values = sensationsData.Split(',');
                int frequency = int.Parse(values[0]);
                float duration = (float.Parse(values[1]) / 10);
                int intensityPercentage = int.Parse(values[2]);
                float rampUpInMills = (float.Parse(values[3]) / 1000);
                float rampDownInMills = (float.Parse(values[4]) / 1000);
                float exitDelay = (float.Parse(values[5]) / 10);

                intensitySlider.value = intensityPercentage;
                frequancySlider.value = frequency;
                rampUpSlider.value = rampUpInMills;
                rampDownSlider.value = rampDownInMills;
                durationSlider.value = duration;
            }
            catch (FormatException)
            {
                Debug.LogError("Error parsing primary values from JSON data.");
            }

            if (parts.Length > 1)
            {
                string muscleDataString = parts[1];
                string[] muscleDataList = muscleDataString.Split(',');

                Dictionary<MuscleGroup, int> muscleIntensityMap = new();

                foreach (string muscleData in muscleDataList)
                {
                    string[] muscleParts = muscleData.Split('%');
                    try
                    {
                        int muscleId = int.Parse(muscleParts[0]);
                        int muscleIntensity = int.Parse(muscleParts[1]);

                        if (muscleIdMap.ContainsKey(muscleId))
                        {
                            MuscleGroup muscleGroup = muscleIdMap[muscleId];
                            muscleIntensityMap[muscleGroup] = muscleIntensity;
                        }
                        else
                        {
                            Debug.LogError($"Unknown muscle ID: {muscleId}");
                        }
                    }
                    catch (FormatException)
                    {
                        Debug.LogError("Error parsing muscle values from JSON data.");
                    }
                }
                foreach (KeyValuePair<MuscleGroup, int> entry in muscleIntensityMap)
                {
                    MuscleGroup muscle = entry.Key;
                    int intensity = entry.Value;

                    switch (muscle)
                    {
                        case MuscleGroup.PectoralL:
                            HandleMuscle(musclesPectoralLb, PectoralLSlider, pectoralL, musclesPectoralLO, intensity);
                            break;

                        case MuscleGroup.PectoralR:
                            HandleMuscle(musclesPectoralRb, PectoralRSlider, pectoralR, musclesPectoralRO, intensity);
                            break;

                        case MuscleGroup.DorsalL:
                            HandleMuscle(musclesDorsalLb, DorsalLSlider, dorsalL, musclesDorsalLO, intensity);
                            break;

                        case MuscleGroup.DorsalR:
                            HandleMuscle(musclesDorsalRb, DorsalRSlider, dorsalR, musclesDorsalRO, intensity);
                            break;

                        case MuscleGroup.ArmL:
                            HandleMuscle(musclesArmLb, ArmLSlider, armL, musclesArmLO, intensity);
                            break;

                        case MuscleGroup.ArmR:
                            HandleMuscle(musclesArmRb, ArmRSlider, armR, musclesArmRO, intensity);
                            break;

                        case MuscleGroup.LumbarL:
                            HandleMuscle(musclesLumbarLb, LumbarLSlider, lumbarL, musclesLumbarLO, intensity);
                            break;

                        case MuscleGroup.LumbarR:
                            HandleMuscle(musclesLumbarRb, LumbarRSlider, lumbarR, musclesLumbarRO, intensity);
                            break;

                        case MuscleGroup.AbdominalL:
                            HandleMuscle(musclesAbdominalLb, AbdominalLSlider, abdominalL, musclesAbdominalLO, intensity);
                            break;

                        case MuscleGroup.AbdominalR:
                            HandleMuscle(musclesAbdominalRb, AbdominalRSlider, abdominalR, musclesAbdominalRO, intensity);
                            break;

                        default:
                            Debug.LogError($"Unknown muscle group: {muscle}");
                            break;
                    }
                }
            }
        }
    }
}
