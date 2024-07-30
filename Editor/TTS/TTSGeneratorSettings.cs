using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Elisu.TTSLocalizationKitEditor
{
    public class TTSGeneratorSettings : MonoBehaviour
    {
        [SerializeField] string googleTTSKeyFilePath;

        public string GoogleTTSKeyFilePath => googleTTSKeyFilePath;
    }

}