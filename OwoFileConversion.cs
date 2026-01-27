using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using System.Windows.Forms; // Windows-only
using System.Linq;

public class OwoToJsonConverterUI : MonoBehaviour
{
    // Predefined output folders
    public string sensationsFolder = "Assets/OWO/Sensation Events";      // Lines WITH &
    public string microSensationsFolder = "Assets/OWO/MicroSensation Events"; // Lines WITHOUT &

    // Function to assign to UI Button
    public void ConvertOwoFilesButton()
    {
        string[] filesToProcess;

        // Select .owo files
        using OpenFileDialog fileDialog = new();
        fileDialog.Filter = "OWO Files (*.owo)|*.owo";
        fileDialog.Title = "Select .owo files to convert";
        fileDialog.Multiselect = true;

        if (fileDialog.ShowDialog() != DialogResult.OK)
        {
            Debug.Log("No file selected.");
            return;
        }

        // Use all selected files
        filesToProcess = fileDialog.FileNames;

        // Convert all selected files
        ConvertOwoFiles(filesToProcess);
    }


    private void ConvertOwoFiles(string[] owoFiles)
    {
        // Check output folders exist
        if (!Directory.Exists(sensationsFolder) || !Directory.Exists(microSensationsFolder))
        {
            Debug.LogError("One or both output folders do not exist.");
            return;
        }

        foreach (string file in owoFiles)
        {
            string fileName = Path.GetFileNameWithoutExtension(file);
            string line = File.ReadAllText(file).Trim(); // Single line per file

            if (string.IsNullOrEmpty(line)) continue;

            string cleanLine = CleanOwoLine(line);

            // Decide output folder based on presence of &
            string targetFolder = cleanLine.Contains("&") ? sensationsFolder : microSensationsFolder;
            string outputPath = Path.Combine(targetFolder, fileName + ".json");

            File.WriteAllText(outputPath, $"{{\n\"data\": \"{cleanLine}\"\n}}");

            Debug.Log($"Converted {fileName}.owo -> {outputPath}");
        }

        Debug.Log("All OWO files converted!");
    }

    private string CleanOwoLine(string line)
    {
        line = line.Trim();

        // Remove any prefix until the first numeric block with at least one comma
        Match firstBlockMatch = Regex.Match(line, @"\d+(?:,\d+)+");
        if (!firstBlockMatch.Success)
            return ""; // No numeric blocks found
        line = line.Substring(firstBlockMatch.Index);

        // Split by & to handle multiple blocks
        string[] blocks = line.Split('&');

        for (int i = 0; i < blocks.Length; i++)
        {
            string block = blocks[i];

            // Match numbers with commas
            Match numberMatch = Regex.Match(block, @"\d+(?:,\d+)*,?");
            string numbers = numberMatch.Success ? numberMatch.Value : "";

            // Match |number%number sequences after numbers
            Match pipeMatch = Regex.Match(block, @"\|[\d%,]*");
            string pipe = pipeMatch.Success ? pipeMatch.Value : "";

            blocks[i] = numbers + pipe;
        }

        // Recombine non-empty blocks with &
        string result = string.Join("&", blocks.Where(b => !string.IsNullOrEmpty(b)));

        // Remove repeated commas and trailing commas before | or &
        result = Regex.Replace(result, @",,+", ",");
        result = Regex.Replace(result, @",(\||&)$", "$1");

        return result;
    }







}
