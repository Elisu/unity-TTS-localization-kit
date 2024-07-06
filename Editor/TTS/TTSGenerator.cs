using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using Newtonsoft.Json;
using UnityEditor.AddressableAssets;
using static TextAndAudioManagerBase;

public class TTSGenerator
{
    private string pythonScriptPath = "Packages/TTSLocalizationKit/Editor/TTS/google_tts.py";
    private string baseOutputDir = "Assets/GeneratedTTS";

    [Serializable]
    public class LocalizationData
    {
        public List<LocalizationEntry> entries;
    }

    [Serializable]
    public class LocalizationEntry
    {
        public string key;
        public Dictionary<string, LocalizedText> texts;
    }

    [Serializable]
    public class LocalizedText
    {
        public string value;
        public bool regenerate;
    }


    public async Task RegenerateAudio(string keyFilePath, TableReference localizedStringTable, TableReference localizedAudioTable, GenderSetting gender, string audacityMacro = "")
    {
        var outpurDirPath = Path.Combine(baseOutputDir, localizedAudioTable.TableCollectionName);
        string jsonFilePath = Path.Combine(outpurDirPath, "localization.json");
        var localizationData = new Dictionary<string, LocalizationEntry>();

        // Load existing data if the file exists
        Dictionary<string, LocalizationEntry> existingData = null;
        if (File.Exists(jsonFilePath))
        {
            var existingJson = await File.ReadAllTextAsync(jsonFilePath);
            existingData = JsonConvert.DeserializeObject<LocalizationData>(existingJson)?.entries
                           .ToDictionary(e => e.key, e => e);
        }

        foreach (var locale in LocalizationSettings.AvailableLocales.Locales)
        {
            var tableOperation = LocalizationSettings.StringDatabase.GetTableAsync(localizedStringTable, locale);
            await tableOperation.Task; // Wait for the async operation to complete

            var table = tableOperation.Result;
            if (table == null)
            {
                UnityEngine.Debug.LogError($"Localized string table for {locale} not found.");
                continue; // Skip this locale if the table is not found
            }

            foreach (var entry in table)
            {
                if (!localizationData.TryGetValue(entry.Value.Key, out var localizationEntry))
                {
                    localizationEntry = new LocalizationEntry
                    {
                        key = entry.Value.Key,
                        texts = new Dictionary<string, LocalizedText>()
                    };
                    localizationData.Add(entry.Value.Key, localizationEntry);
                }

                var localizedString = entry.Value.LocalizedValue;
                bool regenerate = existingData != null &&
                                  existingData.TryGetValue(entry.Value.Key, out var existingEntry) &&
                                  existingEntry.texts.TryGetValue(locale.Identifier.Code, out var existingText) &&
                                  existingText.value != localizedString;

                localizationEntry.texts[locale.Identifier.Code] = new LocalizedText
                {
                    value = localizedString,
                    regenerate = regenerate || existingData == null
                };
            }
        }

        var localizationEntries = new LocalizationData
        {
            entries = localizationData.Values.ToList()
        };

        string jsonContent = JsonConvert.SerializeObject(localizationEntries, Formatting.Indented);
        await WriteAllTextAsync(jsonFilePath, jsonContent);

        // Run the TTS script
        await RunTTSScriptAsync(jsonFilePath, outpurDirPath, keyFilePath, audacityMacro);

        // Add generated audio files to the localized asset table
        await AddAudioFilesToTable(jsonFilePath, outpurDirPath, localizedAudioTable);
    }

    private async Task RunTTSScriptAsync(string inputFilePathRelative, string outputPathRelative, string keyFilePathRelative, string macroName = "")
    {
        string currentDir = Directory.GetCurrentDirectory();
        string inputFile = Path.Combine(currentDir, inputFilePathRelative);
        string outputDir = Path.Combine(currentDir, outputPathRelative);
        string credentials = Path.Combine(currentDir, keyFilePathRelative);
        string pythonScript = Path.Combine(currentDir, pythonScriptPath);

        string arguments = $"\"{pythonScript}\" --file \"{inputFile}\" --output_dir \"{outputDir}\" --credentials \"{credentials}\"";

        if (!string.IsNullOrEmpty(macroName))
        {
            arguments += $" --macro_name \"{macroName}\"";
        }

        UnityEngine.Debug.Log($"Executing Python script: {arguments}");

        var processStartInfo = new ProcessStartInfo
        {
            FileName = "python",
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = processStartInfo };
        var outputBuilder = new System.Text.StringBuilder();
        var errorBuilder = new System.Text.StringBuilder();
        var taskCompletionSource = new TaskCompletionSource<bool>();

        process.OutputDataReceived += (sender, args) =>
        {
            if (args.Data != null)
            {
                outputBuilder.AppendLine(args.Data);
                UnityEngine.Debug.Log(args.Data);
            }
        };

        process.ErrorDataReceived += (sender, args) =>
        {
            if (args.Data != null)
            {
                errorBuilder.AppendLine(args.Data);
                UnityEngine.Debug.LogError(args.Data);
            }
        };

        process.EnableRaisingEvents = true;
        process.Exited += (sender, args) =>
        {
            if (process.ExitCode == 0)
            {
                taskCompletionSource.SetResult(true);
            }
            else
            {
                taskCompletionSource.SetException(new Exception($"Python script exited with code {process.ExitCode}: {errorBuilder.ToString()}"));
            }
            process.Dispose();
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await taskCompletionSource.Task;
        UnityEngine.Debug.Log("TTS script executed.");
        UnityEngine.Debug.Log($"Output: {outputBuilder}");
        UnityEngine.Debug.Log($"Errors: {errorBuilder}");

        // Refresh the Asset Database to reflect new files/folders
        AssetDatabase.Refresh();
        UnityEngine.Debug.Log("AssetDatabase refreshed.");
    }



    private async Task AddAudioFilesToTable(string jsonFilePath, string outputPathRelative, TableReference localizedAssetTableReference)
    {
        var jsonData = File.ReadAllText(jsonFilePath);
        var localizationData = JsonConvert.DeserializeObject<LocalizationData>(jsonData);

        foreach (var entry in localizationData.entries)
        {
            foreach (var locale in entry.texts.Keys)
            {
                if (localizedAssetTableReference == null)
                {
                    UnityEngine.Debug.LogError("Localized audio table not found.");
                    return;
                }

                var audioPath = Path.Combine(outputPathRelative, locale, $"{entry.key}_{locale}.mp3");
                if (File.Exists(audioPath))
                {
                    await AddAudioClipToTable(entry.key, Locale.CreateLocale(locale), audioPath, localizedAssetTableReference);
                    UnityEngine.Debug.Log("Audio added");
                }
            }
        }

        AssetDatabase.SaveAssets();
        UnityEngine.Debug.Log("Audio files added to localized asset table.");
    }

    private async Task AddAudioClipToTable(string key, Locale locale, string audioPath, TableReference tableReference)
    {
        var tableOperation = LocalizationSettings.AssetDatabase.GetTableAsync(tableReference, locale);
        await tableOperation.Task; // Wait for the async operation to complete

        var table = tableOperation.Result;

        if (table == null)
        {
            UnityEngine.Debug.LogError("Localized audio table not found.");
            return;
        }

        var audioClip = AssetDatabase.LoadAssetAtPath<AudioClip>(audioPath);
        if (audioClip == null)
        {
            UnityEngine.Debug.LogError($"Failed to load audio clip at path {audioPath}");
            return;
        }

        string guid = AssetDatabase.AssetPathToGUID(audioPath);
        if (string.IsNullOrEmpty(guid))
        {
            UnityEngine.Debug.LogError($"Failed to get GUID for audio clip at path {audioPath}");
            return;
        }

        var addressableSettings = AddressableAssetSettingsDefaultObject.Settings;
        var group = addressableSettings.DefaultGroup;
        var created = addressableSettings.CreateOrMoveEntry(guid, group);

        // Set the address of the entry to be the key for easy access
        created.SetAddress(key);

        var entry = table.GetEntry(key);
        if (entry == null)
        {
            table.AddEntry(key, guid);

            // Data has to also be added to shared table, otherwise it will be ignored
            table.CheckForMissingSharedTableDataEntries(MissingEntryAction.AddEntriesToSharedData);

            EditorUtility.SetDirty(table);
        }
    }

    private static async Task WriteAllTextAsync(string path, string contents)
    {
        // Ensure the directory exists
        string directory = Path.GetDirectoryName(path);
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Write the file
        await File.WriteAllTextAsync(path, contents);
    }
}
