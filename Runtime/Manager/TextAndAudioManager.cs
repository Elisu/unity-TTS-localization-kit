using System;
using System.Threading.Tasks;
using UnityEngine.AddressableAssets;
using UnityEngine;
using UnityEngine.Localization.Settings;
using Debug = UnityEngine.Debug;
using System.Threading;

namespace Elisu.TTSLocalizationKit
{
    [RequireComponent(typeof(AudioSource))]
    public abstract class TextAndAudioManager<T> : TextAndAudioManagerBase where T : TextAndAudioManager<T>
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

        public override async Task SetEntryAsync(string key, Action<string> onDisplayText = null, bool waitTillAudioDone = true, CancellationToken cancellationToken = default)
        {
            if (localizedStringTable == null || localizedAssetTable == null)
            {
                Debug.LogWarning("Localization tables are not loaded, realoading initiated");
                LoadTables();
            }

            // Change whether locale has not changed, and reload if it has
            if (localizedStringTable.LocaleIdentifier != LocalizationSettings.SelectedLocale.Identifier)
            {
                LoadTables();
            }

            var stringEntry = localizedStringTable.GetEntry(key);
            if (stringEntry == null)
            {
                Debug.LogError($"Key {key} not found in the localization table {localizedStringTable.name}");
                return;
            }

            var localizedString = stringEntry.GetLocalizedString();

            if (textUI != null)
                textUI.text = localizedString;

            onDisplayText?.Invoke(localizedString);

            // Get the corresponding localized audio clip entry
            var audioEntry = localizedAssetTable.GetEntry(key);
            if (audioEntry == null)
            {
                Debug.LogWarning($"Key {key} not found in the localization table {localizedAssetTable.name}");
                return;
            }

            // Load the localized audio clip asynchronously using Addressables
            var handle = Addressables.LoadAssetAsync<AudioClip>(audioEntry.LocalizedValue);
            AudioClip audioClip;
            try
            {
                audioClip = await handle.Task;
            }
            catch (TaskCanceledException)
            {
                Debug.LogWarning($"Loading audio clip for key {key} was cancelled");
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (audioClip == null)
            {
                Debug.LogWarning($"Failed to load audio clip for key {key}");
                return;
            }

            audioSource.clip = audioClip;
            audioSource.Play();

            if (waitTillAudioDone)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(audioClip.length), cancellationToken);
                }
                catch (TaskCanceledException)
                {
                    Debug.LogWarning($"Waiting for audio clip for key {key} was cancelled");
                }
            }

            // Release the addressable asset
            Addressables.Release(handle);
        }
    }

}
