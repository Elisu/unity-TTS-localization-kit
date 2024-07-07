using UnityEditor.UIElements;
using UnityEditor;
using UnityEngine.UIElements;
using System.Threading.Tasks;
using UnityEngine.Localization.Tables;
using static TextAndAudioManagerBase;
using System;
using System.IO;

[CustomEditor(typeof(TextAndAudioManager<>), true)]
public class TextAndAudioManagerEditor : Editor
{
    public override VisualElement CreateInspectorGUI()
    {
        var root = new VisualElement();
        InspectorElement.FillDefaultInspector(root, serializedObject, this);

        var manager = (TextAndAudioManagerBase)target;
        var editButton = new Button(async () => await RegenerateAudioClips(manager.TextTableReference, manager.AudioTableReference, manager.Gender, manager.AudacityMacro))
        {
            text = "Regenerate audio clips"
        };

        root.Add(editButton);
        return root;
    }

    private async Task RegenerateAudioClips(TableReference stringTable, TableReference assetTable, VoiceGender gender, string audacityMacro)
    {
        try
        {
            var ttsSettings = FindObjectOfType<TTSGeneratorSettings>();

            if (ttsSettings == null)
            {
                throw new Exception("No TTS Settings found in scene. Generation canceled.");
            }

            if (IsRelativePath(ttsSettings.GoogleTTSKeyFilePath) == false)
            {
                throw new Exception($"Please provide a relative path within the project istead of {ttsSettings.GoogleTTSKeyFilePath}");
            }

            if (File.Exists(ttsSettings.GoogleTTSKeyFilePath) == false)
            {
                throw new Exception($"Google key file not found at {ttsSettings.GoogleTTSKeyFilePath}");
            }

            var tts = new TTSGenerator();
            await tts.RegenerateAudio(ttsSettings.GoogleTTSKeyFilePath, stringTable, assetTable, gender, audacityMacro);
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogError(ex);
        }


    }

    private static bool IsRelativePath(string path)
    {
        // Checks if the path is rooted
        return !Path.IsPathRooted(path);
    }
}
