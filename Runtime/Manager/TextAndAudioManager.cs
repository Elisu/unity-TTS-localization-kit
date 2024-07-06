using System;
using System.Threading.Tasks;
using UnityEngine.AddressableAssets;
using UnityEngine;
using Debug = UnityEngine.Debug;

[RequireComponent(typeof(AudioSource))]
public abstract class TextAndAudioManager<T> : TextAndAudioManagerBase where T: TextAndAudioManager<T>
{
    private static T instance;

    public static T Instance
    {
        get
        {

            if (instance == null)
            {
                instance = FindObjectOfType<T>();

                if (instance == null)
                {
                    GameObject singletonObject = new GameObject();
                    instance = singletonObject.AddComponent<T>();
                    singletonObject.name = typeof(T).ToString() + " (Singleton)";
                }
            }

            return instance;
        }
    }

    protected virtual void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = (T)this;

        audioSource = GetComponent<AudioSource>();
        LoadTables();
    }

    private void LoadTables()
    {
        localizedStringTable = textTable.GetTable();
        localizedAssetTable = audioTable.GetTable();
    }

    public async Task SetEntryAsync(string key, Action<string> onDisplayText = null, bool waitTillAudioDone = true)
    {
        var stringEntry = localizedStringTable.GetEntry(key);
        if (stringEntry == null)
        {
            Debug.LogError($"Key {key} not found in the localization table {localizedStringTable.name}");
        }

        var localizedString = stringEntry.GetLocalizedString();

        if (textUI != null)
            textUI.text = localizedString;

        if (onDisplayText != null)
            onDisplayText.Invoke(localizedString);
    

        // Get the corresponding localized audio clip entry
        var audioEntry = localizedAssetTable.GetEntry(key);
        if (audioEntry == null)
        {
            Debug.LogWarning($"Key {key} not found in the localization table {localizedAssetTable.name}");
            return;
        }

        // Load the localized audio clip asynchronously using Addressables
        var handle = Addressables.LoadAssetAsync<AudioClip>(audioEntry.LocalizedValue);
        AudioClip audioClip = await handle.Task;

        if (audioClip == null)
        {
            Debug.LogWarning($"Failed to load audio clip for key {key}");
            return;
        }

        audioSource.clip = audioClip;
        audioSource.Play();

        // Wait for the duration of the audio clip
        if (waitTillAudioDone)
            await Task.Delay(TimeSpan.FromSeconds(audioClip.length));

        // Release the addressable asset
        Addressables.Release(handle);
    }
}
