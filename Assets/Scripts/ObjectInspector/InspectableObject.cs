using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class SubObjectInfo
{
    public string subID;
    public GameObject subGameObject;
    public bool isFound = false;
    public bool isVerified = false;

    [Header("Sub Object Names")]
    public string foundSubName = "";    // isFound 時的稱呼
    public string verifiedSubName = ""; // isVerified 時的稱呼

    [Header("Sub Object Ink Configuration")]
    public string foundInkKnot = "";    // isFound 時觸發的 Ink Knot
    public string verifiedInkKnot = ""; // isVerified 時觸發的 Ink Knot
}

public class InspectableObject : MonoBehaviour
{
    public string mainInfoID;
    public Sprite mainSprite;
    public bool useCustomCrop;
    public Rect cropRect;
    public bool isMainEvidenceUnlocked;

    [Header("Inspect Settings")]
    [Tooltip("若大於 0 則覆蓋 InspectManager 內設定的預設 inspectDistance，未填或 <=0 則依循 InspectManager")]
    public float overrideInspectDistance = -1f;

    [Header("Ink Configuration")]
    public string allFoundInkKnot = "";
    public string allVerifiedInkKnot = "";

    public List<SubObjectInfo> subObjectInfos = new List<SubObjectInfo>();

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

                Transform foundTransform = FindChildByName(transform, targetName);
                if (foundTransform != null)
                {
                    sub.subGameObject = foundTransform.gameObject;
                }
            }
        }
    }

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

    // 【修改】只計算當前 Active 的子物件總數
    public int GetActiveSubObjectCount()
    {
        if (subObjectInfos == null || subObjectInfos.Count == 0) return 0;
        int count = 0;
        foreach (var sub in subObjectInfos)
        {
            if (sub != null && sub.subGameObject != null && sub.subGameObject.activeInHierarchy)
            {
                count++;
            }
        }
        return count;
    }

    // 【修改】只檢查 Active 的子物件是否皆已找到
    public bool IsAllSubObjectsFound()
    {
        if (subObjectInfos == null || subObjectInfos.Count == 0) return false;
        foreach (var sub in subObjectInfos)
        {
            if (sub != null && sub.subGameObject != null && sub.subGameObject.activeInHierarchy)
            {
                if (!sub.isFound) return false;
            }
        }
        return true;
    }

    // 【修改】只檢查 Active 的子物件是否皆已驗證
    public bool IsAllSubObjectsVerified()
    {
        if (subObjectInfos == null || subObjectInfos.Count == 0) return false;
        foreach (var sub in subObjectInfos)
        {
            if (sub != null && sub.subGameObject != null && sub.subGameObject.activeInHierarchy)
            {
                if (!sub.isVerified) return false;
            }
        }
        return true;
    }

    // 【修改】只計算 Active 且已找到的數量
    public int GetFoundCount()
    {
        int count = 0;
        foreach (var sub in subObjectInfos)
        {
            if (sub != null && sub.subGameObject != null && sub.subGameObject.activeInHierarchy)
            {
                if (sub.isFound) count++;
            }
        }
        return count;
    }

    // 【新增】只計算 Active 且已驗證的數量
    public int GetVerifiedCount()
    {
        int count = 0;
        foreach (var sub in subObjectInfos)
        {
            if (sub != null && sub.subGameObject != null && sub.subGameObject.activeInHierarchy)
            {
                if (sub.isVerified) count++;
            }
        }
        return count;
    }

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

            if (sub.subGameObject == hitObj)
            {
                foundInfo = sub;
                index = i;
                return true;
            }

            if (hitObj.transform.IsChildOf(sub.subGameObject.transform))
            {
                foundInfo = sub;
                index = i;
                return true;
            }

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

    public bool TryVerifySubInfo(string targetSubID)
    {
        foreach (var sub in subObjectInfos)
        {
            if (sub.subID == targetSubID)
            {
                if (!sub.isVerified)
                {
                    sub.isVerified = true;

                    if (!string.IsNullOrEmpty(sub.verifiedInkKnot))
                    {
                        GameManager gm = FindObjectOfType<GameManager>();
                        if (gm != null)
                        {
                            gm.ExecuteKnot(sub.verifiedInkKnot);
                        }
                    }

                    return true;
                }
            }
        }
        return false;
    }
}