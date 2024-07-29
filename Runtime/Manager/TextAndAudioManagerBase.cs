using TMPro;
using UnityEngine;
using UnityEngine.Localization.Tables;
using UnityEngine.Localization;
using System;
using System.Threading.Tasks;

public abstract class TextAndAudioManagerBase : MonoBehaviour
{
    public enum VoiceGender
    {
        Male,
        Female,
        Neutral
    }

    [Header("TTS Voice settings")]
    [SerializeField] VoiceGender gender = VoiceGender.Female;
    [SerializeField] string audacityMacro = string.Empty;


    [Header("Localization Settings")]
    [SerializeField] protected LocalizedStringTable textTable;
    [SerializeField] protected LocalizedAssetTable audioTable;

    [Header("UI text output")]
    [SerializeField] protected TMP_Text textUI;

    protected AudioSource audioSource;

    protected StringTable localizedStringTable;
    protected AssetTable localizedAssetTable;

    public TableReference TextTableReference => textTable.TableReference;
    public TableReference AudioTableReference => audioTable.TableReference;

    public VoiceGender Gender => gender;

    public string AudacityMacro => audacityMacro;

    public abstract Task SetEntryAsync(string key, Action<string> onDisplayText = null, bool waitTillAudioDone = true);

}
