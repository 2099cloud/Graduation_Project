using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SwitchBakedLight : MonoBehaviour
{
    public static SwitchBakedLight Instance { get; private set; }

    [System.Serializable]
    public struct LightmapPreset
    {
        public string configName;
        public Texture2D[] lightmapColor;
        public Texture2D[] lightmapDir;
        public Texture2D skyboxTexture;
        public Material skyboxMaterial;
    }

    [Header("Baked Lightmap Presets")]
    public List<LightmapPreset> presets = new List<LightmapPreset>();
    public string defaultPresetName = "LightingData_Night";

    private Dictionary<string, LightmapData[]> presetLightmaps = new Dictionary<string, LightmapData[]>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        InitializePresets();
    }

    private void Start()
    {
        if (!string.IsNullOrEmpty(defaultPresetName))
        {
            ApplyLightmapPreset(defaultPresetName);
        }
    }

    public void InitializePresets()
    {
        presetLightmaps.Clear();
        foreach (var preset in presets)
        {
            if (string.IsNullOrEmpty(preset.configName)) continue;

            int count = preset.lightmapColor != null ? preset.lightmapColor.Length : 0;
            LightmapData[] lmArray = new LightmapData[count];

            for (int i = 0; i < count; i++)
            {
                lmArray[i] = new LightmapData();
                if (preset.lightmapColor != null && i < preset.lightmapColor.Length)
                {
                    lmArray[i].lightmapColor = preset.lightmapColor[i];
                }

                if (preset.lightmapDir != null && i < preset.lightmapDir.Length)
                {
                    lmArray[i].lightmapDir = preset.lightmapDir[i];
                }
            }

            presetLightmaps[preset.configName.ToLower()] = lmArray;
        }
    }

    public void ApplyLightmapPreset(string configName)
    {
        string cleanName = configName.Trim();
        if (cleanName.StartsWith("LightingData."))
        {
            cleanName = cleanName.Replace("LightingData.", "LightingData_");
        }

        string key = cleanName.ToLower();

        if (presetLightmaps.TryGetValue(key, out LightmapData[] lightmaps))
        {
            LightmapSettings.lightmaps = lightmaps;

            LightmapPreset preset = presets.Find(p => p.configName.Equals(cleanName, StringComparison.OrdinalIgnoreCase));

            if (preset.skyboxMaterial != null)
            {
                RenderSettings.skybox = preset.skyboxMaterial;
            }

            if (preset.skyboxTexture != null && RenderSettings.skybox != null)
            {
                Material targetMat = RenderSettings.skybox;
                if (targetMat.HasProperty("_MainTex"))
                {
                    targetMat.SetTexture("_MainTex", preset.skyboxTexture);
                }
                else if (targetMat.HasProperty("_Tex"))
                {
                    targetMat.SetTexture("_Tex", preset.skyboxTexture);
                }
            }

            if (preset.skyboxMaterial != null || preset.skyboxTexture != null)
            {
                DynamicGI.UpdateEnvironment();
            }

            Debug.Log("[SwitchBakedLight] Applied lightmap preset: " + cleanName);
        }
        else
        {
            Debug.LogWarning("[SwitchBakedLight] Lightmap preset not found: " + configName);
        }
    }
}