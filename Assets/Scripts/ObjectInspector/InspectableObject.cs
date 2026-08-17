using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class SubObjectInfo
{
    public string subID;          // 線索唯一識別碼（例如: "73N_Tracking"）
    public GameObject subGameObject;
    public bool isFound = false;   // 是否在 3D 檢視中找到
    public bool isVerified = false;// 是否在打詐網完成文字比對
}

public class InspectableObject : MonoBehaviour
{
    public string mainInfoID;
    public Sprite mainSprite;
    public bool useCustomCrop;
    public Rect cropRect;
    public bool isMainEvidenceUnlocked;

    public List<SubObjectInfo> subObjectInfos = new List<SubObjectInfo>();

    /// <summary>
    /// 當物件被 Instantiate 複製時（如 3D 檢視模式生成的 Clone），
    /// 重新將 subGameObject 的引用綁定到複製體內部的對應子物件上。
    /// </summary>
    /// 

    private void Start()
    {
        
    }
    public void RebindClonedReferences()
    {
        foreach (var sub in subObjectInfos)
        {
            if (sub.subGameObject != null)
            {
                string targetName = sub.subGameObject.name;

                // 在複製體自身的子物件樹中尋找同名 Transform
                Transform foundTransform = FindChildByName(transform, targetName);
                if (foundTransform != null)
                {
                    sub.subGameObject = foundTransform.gameObject;
                }
            }
        }
    }

    /// <summary>
    /// 遞迴尋找同名的子物件 Transform
    /// </summary>
    private Transform FindChildByName(Transform parent, string targetName)
    {
        foreach (Transform child in parent)
        {
            if (child.name == targetName)
                return child;

            Transform result = FindChildByName(child, targetName);
            if (result != null)
                return result;
        }
        return null;
    }

    // 檢查是否已在 3D 畫面中找齊所有線索
    public bool IsAllSubObjectsFound()
    {
        if (subObjectInfos.Count == 0) return true;
        foreach (var sub in subObjectInfos)
        {
            if (!sub.isFound) return false;
        }
        return true;
    }

    // 取得已在 3D 檢視中找到 (Found) 的數量，作為 UI 進度條填滿的依據
    public int GetFoundCount()
    {
        int count = 0;
        foreach (var sub in subObjectInfos)
        {
            if (sub.isFound) count++;
        }
        return count;
    }

    /// <summary>
    /// 精確且具備相容性的子物件判定邏輯
    /// </summary>
    public bool TryGetSubObjectInfo(GameObject hitObj, out SubObjectInfo foundInfo, out int index)
    {
        if (hitObj == null)
        {
            foundInfo = null;
            index = -1;
            return false;
        }

        for (int i = 0; i < subObjectInfos.Count; i++)
        {
            var sub = subObjectInfos[i];
            if (sub.subGameObject == null) continue;

            // 1. 精確物件引用比對
            if (sub.subGameObject == hitObj)
            {
                foundInfo = sub;
                index = i;
                return true;
            }

            // 2. 階層比對：若 Raycast 擊中的是 subGameObject 內部的 Mesh 或 Collider
            if (hitObj.transform.IsChildOf(sub.subGameObject.transform))
            {
                foundInfo = sub;
                index = i;
                return true;
            }

            // 3. 名稱彈性比對 (防護機制：確保 Clone 後名稱一致即可被精確點擊)
            if (hitObj.name == sub.subGameObject.name)
            {
                foundInfo = sub;
                index = i;
                return true;
            }
        }

        foundInfo = null;
        index = -1;
        return false;
    }

    public void MarkAsFound(int index)
    {
        if (index >= 0 && index < subObjectInfos.Count)
        {
            subObjectInfos[index].isFound = true;
        }
    }

    // --- 供 VirtualBrowserController 呼叫的比對驗證邏輯 ---
    public bool TryVerifySubInfo(string targetSubID)
    {
        foreach (var sub in subObjectInfos)
        {
            if (sub.subID == targetSubID)
            {
                if (!sub.isVerified)
                {
                    sub.isVerified = true; // 標記比對成功
                    return true;
                }
            }
        }
        return false;
    }
}