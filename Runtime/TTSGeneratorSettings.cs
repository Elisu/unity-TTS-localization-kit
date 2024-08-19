using UnityEngine;

namespace Elisu.TTSLocalizationKit
{
    public class TTSGeneratorSettings : MonoBehaviour
    {
        [SerializeField] string googleTTSKeyFilePath;

        public string GoogleTTSKeyFilePath => googleTTSKeyFilePath;
    }

}