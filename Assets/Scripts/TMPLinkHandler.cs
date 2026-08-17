using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// 處理 TextMeshProUGUI 內 <link="ID"> 標籤點擊事件的處理器。
/// 實作 IPointerDownHandler 確保 Unity EventSystem 能正確捕獲點擊事件。
/// </summary>
[RequireComponent(typeof(TextMeshProUGUI))]
public class TMPLinkHandler : MonoBehaviour, IPointerClickHandler, IPointerDownHandler
{
    private TextMeshProUGUI tmpText;

    // 文字點擊事件：傳出點擊到的 Link ID
    public System.Action<string> OnLinkClicked;

    private void Awake()
    {
        tmpText = GetComponent<TextMeshProUGUI>();
    }

    private void Start()
    {
        // 階段 0：啟動與掛載檢查（若連這條 Log都沒印出，代表腳本未掛載在當前顯示的物件上）
        if (tmpText != null)
        {
            Debug.Log($"<color=cyan>[TMPLinkHandler] 腳本已成功初始化！掛載物件：'{gameObject.name}' | RaycastTarget：{tmpText.raycastTarget}</color>");
        }
        else
        {
            Debug.LogError($"[TMPLinkHandler] 物件 '{gameObject.name}' 上找不到 TextMeshProUGUI 組件！");
        }
    }

    /// <summary>
    /// Unity EventSystem 核心機制：必須實現 IPointerDownHandler，
    /// EventSystem 才會認定此物件可接收滑鼠事件，進而觸發 OnPointerClick。
    /// </summary>
    public void OnPointerDown(PointerEventData eventData)
    {
        Debug.Log($"<color=orange>[TMPLinkHandler] 偵測到 PointerDown (按下滑鼠)！物件名稱：{gameObject.name}</color>");
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // 階段 1：確認 PointerClick 是否有順利觸發
        Debug.Log($"<color=yellow>[TMPLinkHandler] 點擊到文字區域！物件名稱：{gameObject.name}，點擊位置：{eventData.position}</color>");

        if (tmpText == null)
        {
            Debug.LogError("[TMPLinkHandler] TextMeshProUGUI 組件遺失！");
            return;
        }

        // 強制更新 Mesh 與 TextInfo，避免文字動態替換後 Link 碰撞資訊未更新
        tmpText.ForceMeshUpdate();

        // 檢查當前文字內容是否包含 <link> 標籤
        if (tmpText.textInfo == null || tmpText.textInfo.linkCount == 0)
        {
            Debug.LogWarning($"[TMPLinkHandler] 當前文字組件中沒有任何 <link> 標籤！文字內容為：{tmpText.text}");
            return;
        }

        // 階段 2：適配 Canvas 渲染模式的相機
        Canvas parentCanvas = tmpText.canvas;
        Camera targetCamera = null;

        if (parentCanvas != null && parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            targetCamera = eventData.pressEventCamera != null ? eventData.pressEventCamera : Camera.main;
        }

        // 階段 3：計算點擊座標是否落在 <link> 上
        int linkIndex = TMP_TextUtilities.FindIntersectingLink(tmpText, eventData.position, targetCamera);

        Debug.Log($"[TMPLinkHandler] 檢測到 Link 數量：{tmpText.textInfo.linkCount}，計算出的 linkIndex 為：{linkIndex}");

        if (linkIndex != -1)
        {
            TMP_LinkInfo linkInfo = tmpText.textInfo.linkInfo[linkIndex];
            string linkID = linkInfo.GetLinkID();
            string linkText = linkInfo.GetLinkText();

            Debug.Log($"<color=green>[TMPLinkHandler] 成功點擊 <link>！ID：'{linkID}'，顯示文字：'{linkText}'</color>");

            // 階段 4：檢查是否有腳本訂閱事件並發送
            if (OnLinkClicked != null)
            {
                Debug.Log($"[TMPLinkHandler] 正在觸發 OnLinkClicked 事件發送 ID: {linkID}...");
                OnLinkClicked.Invoke(linkID);
            }
            else
            {
                Debug.LogWarning("<color=orange>[TMPLinkHandler] OnLinkClicked 沒有任何訂閱者 (Subscriber)！請確認比對頁面/UI 腳本是否有 Listen 此事件。</color>");
            }
        }
        else
        {
            Debug.Log("[TMPLinkHandler] 點擊位置未落在任何 <link> 標籤範圍內。");
        }
    }
}