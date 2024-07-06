using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class TTSGeneratorSettings : MonoBehaviour
{
    [SerializeField] string googleTTSKeyFilePath;

    public string GoogleTTSKeyFilePath => googleTTSKeyFilePath;
}
