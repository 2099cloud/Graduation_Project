using UnityEngine;
using TMPro;
using System.Collections.Generic;

public class LabelGenerator : MonoBehaviour
{
    public GameObject labelPrefab; // 拖入包含 Billboard 腳本的 Prefab
    public TextAsset csvFile;      // 拖入你的 CSV 檔案
    public float yOffset = 2.0f;   // 標籤距離物件中心點的高度偏移

    void Start()
    {
        GenerateLabelsFromCSV();
    }

    void GenerateLabelsFromCSV()
    {
        if (csvFile == null) return;

        // 讀取 CSV (假設第一行為標題：物件名稱,hierarchy內名稱)
        string[] lines = csvFile.text.Split('\n');

        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;

            string[] data = lines[i].Split(',');
            if (data.Length < 2) continue;

            string displayName = data[0].Trim(); // 要顯示在標籤上的文字
            string hierarchyName = data[1].Trim(); // 在 Hierarchy 中的名稱

            // 在場景中尋找該物件
            GameObject targetObj = GameObject.Find(hierarchyName);

            if (targetObj != null)
            {
                SpawnLabel(displayName, targetObj.transform);
            }
            else
            {
                Debug.LogWarning($"找不到物件: {hierarchyName}，請檢查名稱是否正確。");
            }
        }
    }

    void SpawnLabel(string text, Transform target)
    {
        // 1. 生成標籤，並直接設為目標物件的子物體，這樣目標移動時標籤會跟著動
        Vector3 spawnPos = target.position + Vector3.up * yOffset;
        GameObject newLabel = Instantiate(labelPrefab, spawnPos, Quaternion.identity, target);

        // 2. 修改文字
        TextMeshPro tmp = newLabel.GetComponentInChildren<TextMeshPro>();
        if (tmp != null)
        {
            tmp.text = text;
        }
    }
}