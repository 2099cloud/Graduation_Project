using System.Collections.Generic;
using UnityEngine;

#region 1. 數據結構
[System.Serializable]
public struct SubObjectInfo
{
    [Header("子物件與識別")]
    public GameObject subObjectMesh; // 子物件的 GameObject (必須含有 Collider)
    public string subObjectID;       // 子物件識別碼 (例如: Sub_Part1)

    [HideInInspector]
    public bool isFound;             // 內部標記：此線索是否已被找出
}
#endregion

/// <summary>
/// 掛載在場景中的 3D 可檢視物件本體上，負責存放線索資料與觸發檢視
/// </summary>
public class InspectableObject : MonoBehaviour
{
    [Header("主物件 (證據) 基礎配置")]
    public string mainInfoID = "Evidence_MainObject";

    [Header("主物件縮圖與裁切設定")]
    [Tooltip("請直接將主物件在線索欄要顯示的 Sprite 圖片拖入此處")]
    public Sprite mainSprite;

    [Tooltip("勾選後可以指定要顯示圖片中的哪一個區域")]
    public bool useCustomCrop = false;

    [Tooltip("像素座標與尺寸：X (左), Y (下), Width (寬), Height (高)")]
    public Rect cropRect = new Rect(0, 0, 100, 100);

    [Header("需尋找的子物件線索列表")]
    public List<SubObjectInfo> subObjectInfos = new List<SubObjectInfo>();

    [HideInInspector] public bool isMainEvidenceUnlocked = false;

    private void OnMouseDown()
    {
        if (InspectManager.Instance != null)
        {
            Debug.Log($"[場景探索] 點擊主物件 {gameObject.name}，開啟檢視器！");
            InspectManager.Instance.StartInspect(this);
        }
        else
        {
            Debug.LogError("[InspectableObject] 場景中找不到 InspectManager 單例！");
        }
    }

    #region 輔助方法
    public bool TryGetSubObjectInfo(GameObject hitSubObject, out SubObjectInfo foundInfo, out int listIndex)
    {
        for (int i = 0; i < subObjectInfos.Count; i++)
        {
            if (subObjectInfos[i].subObjectMesh != null &&
                subObjectInfos[i].subObjectMesh.name == hitSubObject.name)
            {
                foundInfo = subObjectInfos[i];
                listIndex = i;
                return true;
            }
        }
        foundInfo = default;
        listIndex = -1;
        return false;
    }

    public void MarkAsFound(int index)
    {
        if (index >= 0 && index < subObjectInfos.Count)
        {
            SubObjectInfo info = subObjectInfos[index];
            info.isFound = true;
            subObjectInfos[index] = info;
        }
    }

    public int GetFoundCount()
    {
        int count = 0;
        foreach (var sub in subObjectInfos)
        {
            if (sub.isFound) count++;
        }
        return count;
    }
    #endregion
}