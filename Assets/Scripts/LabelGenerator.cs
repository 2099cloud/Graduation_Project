using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.UI;
using TMPro; // 支援 TextMeshPro

public class LabelGenerator : MonoBehaviour
{
    public GameObject labelPrefab;
    public TextAsset csvFile;
    public float yOffset = 2.0f;

    private List<GameObject> generatedLabels = new List<GameObject>();
    private PlayableDirector activeDirector;
    private Transform mainCameraTransform; // 快取攝影機 Transform 以節省效能

    void Start()
    {
        if (Camera.main != null)
        {
            mainCameraTransform = Camera.main.transform;
        }

        GenerateLabelsFromCSV();
        BindTimelineEvents();
    }

    void OnDestroy()
    {
        UnbindTimelineEvents();
    }

    void LateUpdate()
    {
        // 確保攝影機存在且列表中有 Label
        if (mainCameraTransform == null)
        {
            if (Camera.main != null) mainCameraTransform = Camera.main.transform;
            return;
        }

        // 讓所有 Label 在 LateUpdate 朝向攝影機
        foreach (GameObject label in generatedLabels)
        {
            if (label != null && label.activeInHierarchy)
            {
                // 正確面向攝影機方向（避免左右顛倒）
                label.transform.rotation = mainCameraTransform.rotation;
            }
        }
    }

    void GenerateLabelsFromCSV()
    {
        if (csvFile == null) return;

        string[] lines = csvFile.text.Split(new char[] { '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries);

        for (int i = 1; i < lines.Length; i++)
        {
            string[] data = lines[i].Split(',');

            // 修正點：因為要抓到第三縱列 (索引 2)，整行至少需要有 3 個欄位
            if (data.Length < 3) continue;

            // 抓取並清除可能的引號與空白
            string labelText = data[0].Replace("\"", "").Trim();       // 第一縱列：顯示文字
            string hierarchyName = data[2].Replace("\"", "").Trim();   // 第三縱列：Hierarchy 物件名稱

            if (string.IsNullOrEmpty(hierarchyName)) continue;

            GameObject targetObj = GameObject.Find(hierarchyName);

            if (targetObj != null)
            {
                SpawnLabel(targetObj.transform, labelText);
            }
            else
            {
                Debug.LogWarning("Target object not found: " + hierarchyName);
            }
        }
    }

    void SpawnLabel(Transform target, string textContent)
    {
        Vector3 spawnPos = target.position + Vector3.up * yOffset;
        GameObject newLabel = Instantiate(labelPrefab, spawnPos, Quaternion.identity, transform);

        // 自動抓取 Label Prefab 上的文字元件並賦值 (支援 UI Text 或 TextMeshProUGUI)
        Text uiText = newLabel.GetComponentInChildren<Text>();
        if (uiText != null)
        {
            uiText.text = textContent;
        }
        else
        {
            TextMeshProUGUI tmpText = newLabel.GetComponentInChildren<TextMeshProUGUI>();
            if (tmpText != null)
            {
                tmpText.text = textContent;
            }
        }

        generatedLabels.Add(newLabel);
    }

    private void BindTimelineEvents()
    {
        activeDirector = FindObjectOfType<PlayableDirector>();
        if (activeDirector != null)
        {
            activeDirector.played += OnTimelinePlayed;
            activeDirector.stopped += OnTimelineStopped;

            if (activeDirector.state == PlayState.Playing)
            {
                SetLabelsActive(false);
            }
        }
    }

    private void UnbindTimelineEvents()
    {
        if (activeDirector != null)
        {
            activeDirector.played -= OnTimelinePlayed;
            activeDirector.stopped -= OnTimelineStopped;
        }
    }

    private void OnTimelinePlayed(PlayableDirector director)
    {
        SetLabelsActive(false);
    }

    private void OnTimelineStopped(PlayableDirector director)
    {
        SetLabelsActive(true);
    }

    private void SetLabelsActive(bool isActive)
    {
        foreach (GameObject label in generatedLabels)
        {
            if (label != null)
            {
                label.SetActive(isActive);
            }
        }
    }
}