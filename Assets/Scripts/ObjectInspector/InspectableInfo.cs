using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InspectableInfo : MonoBehaviour
{
    // 定義單一可疑子物件的配置資料
    [System.Serializable]
    public struct SubObjectInfo
    {
        [Header("子物件與識別")]
        public GameObject subObjectMesh; // 子物件的 GameObject (必須含有 Collider)
        public string infoID;            // 這個子物件對應的唯一線索 ID (例如: Info_FakeBill)

        [Header("縮圖大圖裁切配置 (UI 顯示用)")]
        public Texture2D atlasTexture;   // 縮圖合併大圖
        public Rect uvRect;              // 該線索在畫布大圖中的像素位置與大小

        [HideInInspector]
        public bool isFound;             // 內部標記：此線索是否已被找出
    }

    [Header("可被擷取的子物件線索列表")]
    public List<SubObjectInfo> subObjectInfos = new List<SubObjectInfo>();

    /// <summary>
    /// 探索期：當玩家在場景中用滑鼠點擊此主物件時觸發
    /// </summary>
    private void OnMouseDown()
    {
        // 確保點擊時，檢視器經理存在
        if (ObjectInspector.Instance != null)
        {
            Debug.Log($"[場景探索] 點擊了主物件 {gameObject.name}，直接複製本體開啟檢視器！");

            // 直接將「自己 (this.gameObject)」傳過去克隆
            ObjectInspector.Instance.StartInspect(this.gameObject);
        }
        else
        {
            Debug.LogError("找不到 ObjectInspector 實例！請確保場景中已掛載 ObjectInspector 腳本。");
        }
    }

    /// <summary>
    /// 外部調閱：根據擊中的子物件，尋找對應的線索設定
    /// </summary>
    public bool TryGetSubObjectInfo(GameObject hitSubObject, out SubObjectInfo foundInfo, out int listIndex)
    {
        for (int i = 0; i < subObjectInfos.Count; i++)
        {
            // 比對名字與物件是否相符
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

    /// <summary>
    /// 外部更新：標記某個子物件線索已被找到
    /// </summary>
    public void MarkAsFound(int index)
    {
        if (index >= 0 && index < subObjectInfos.Count)
        {
            SubObjectInfo info = subObjectInfos[index];
            info.isFound = true;
            subObjectInfos[index] = info; // 結構體(struct)需要重新指派回列表
        }
    }
}