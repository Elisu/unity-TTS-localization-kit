using TMPro;
using UnityEngine;
using UnityEngine.Localization.Tables;
using UnityEngine.Localization;

public class TextAndAudioManagerBase : MonoBehaviour
{
    public enum GenderSetting
    {
        Male,
        Female
    }

    [Header("TTS Voice settings")]
    [SerializeField] GenderSetting gender = GenderSetting.Female;
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

    public GenderSetting Gender => gender;

    public string AudacityMacro => audacityMacro;

}
