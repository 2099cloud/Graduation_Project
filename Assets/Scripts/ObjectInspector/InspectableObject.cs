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
}

public class InspectableObject : MonoBehaviour
{
    public string mainInfoID;
    public Sprite mainSprite;
    public bool useCustomCrop;
    public Rect cropRect;
    public bool isMainEvidenceUnlocked;

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

    public bool IsAllSubObjectsFound()
    {
        if (subObjectInfos == null || subObjectInfos.Count == 0) return false;
        foreach (var sub in subObjectInfos)
        {
            if (!sub.isFound) return false;
        }
        return true;
    }

    public bool IsAllSubObjectsVerified()
    {
        if (subObjectInfos == null || subObjectInfos.Count == 0) return false;
        foreach (var sub in subObjectInfos)
        {
            if (!sub.isVerified) return false;
        }
        return true;
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
                    return true;
                }
            }
        }
        return false;
    }
}