using System.Collections.Generic;
using UnityEngine;

public class EvidenceManager : MonoBehaviour
{
    public static EvidenceManager Instance { get; private set; }

    // 內部記帳本：使用不重複的 HashSet 來記錄玩家「當前已經摘錄到」的資訊 ID
    private HashSet<string> playerDiscoveredEvidenceIDs = new HashSet<string>();

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // 確保切換場景時，記帳本不會被刷掉
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 當玩家找到新資訊時，由外部（如物件檢視器）呼叫此方法登記
    /// </summary>
    public void CollectEvidence(string id)
    {
        if (string.IsNullOrEmpty(id)) return;

        if (!playerDiscoveredEvidenceIDs.Contains(id))
        {
            playerDiscoveredEvidenceIDs.Add(id);
            Debug.Log($"[後台管理] 成功登記新資訊: {id}");
        }
    }

    /// <summary>
    /// 核心調閱功能 1：檢查玩家「有沒有」某一項特定的資訊 (供 GameManager 或 Ink 條件檢查)
    /// </summary>
    public bool HasEvidence(string id)
    {
        return playerDiscoveredEvidenceIDs.Contains(id);
    }

    /// <summary>
    /// 核心調閱功能 2：直接獲取玩家「目前找到的所有資訊 ID 清單」
    /// </summary>
    public HashSet<string> GetAllDiscoveredEvidenceIDs()
    {
        return playerDiscoveredEvidenceIDs;
    }

    /// <summary>
    /// 核心調閱功能 3：查詢玩家目前「總共」找到了幾項資訊
    /// </summary>
    public int GetCollectedCount()
    {
        return playerDiscoveredEvidenceIDs.Count;
    }

    /// <summary>
    /// 重設記帳本 (例如玩家重新開局時使用)
    /// </summary>
    public void ResetManager()
    {
        playerDiscoveredEvidenceIDs.Clear();
    }
}