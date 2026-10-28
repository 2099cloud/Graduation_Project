using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// 掛載於 subobjectUIPrefab 上，處理在 VirtualBrowser 狀態下的拖曳、淡化與劃過關鍵字高亮效果
/// </summary>
public class SubObjectUIDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private Vector3 originalPosition;
    private Transform originalParent;
    private CanvasGroup canvasGroup;
    private Canvas parentCanvas;

    // 關鍵字高亮偵測變數
    private TextMeshProUGUI lastHoveredTMP;
    private int lastHoveredLinkIndex = -1;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
        parentCanvas = GetComponentInParent<Canvas>();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        // 僅在 VirtualBrowser 啟動時才可拖曳
        if (!IsBrowserActive()) return;

        // 通知 InspectManager 當前正在拖曳，鎖定面板不要隱藏
        if (InspectManager.Instance != null)
        {
            InspectManager.Instance.SetHoverDraggingState(true);
        }

        originalPosition = transform.position;
        originalParent = transform.parent;

        // 設定半透明
        canvasGroup.alpha = 0.5f;
        canvasGroup.blocksRaycasts = false; // 避免拖曳時擋住滑鼠 Raycast

        // 隱藏 Hover 面板內的其他同級 SubObjectUI
        if (originalParent != null)
        {
            foreach (Transform child in originalParent)
            {
                if (child != transform)
                {
                    child.gameObject.SetActive(false);
                }
            }
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!IsBrowserActive()) return;

        // 跟隨滑鼠位置移動
        if (parentCanvas != null && parentCanvas.renderMode != RenderMode.WorldSpace)
        {
            transform.position = eventData.position;
        }
        else
        {
            Vector3 worldPoint;
            if (RectTransformUtility.ScreenPointToWorldPointInRectangle(
                transform as RectTransform, eventData.position, eventData.pressEventCamera, out worldPoint))
            {
                transform.position = worldPoint;
            }
        }

        // 偵測拖曳物件是否劃過 FraudArticle 文章內文的關鍵字
        CheckArticleLinkUnderPointer(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!IsBrowserActive()) return;

        // 檢查當前釋放時是否停留在正確的比對關鍵字上
        TryVerifySubObjectOnRelease(eventData);

        // 復原最後高亮顯示的關鍵字顏色
        ResetLastHoveredLinkColor();

        // 恢復透明度與 Raycast
        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;

        // 恢復原始位置
        transform.position = originalPosition;

        // 重新顯示其他子物件
        if (originalParent != null)
        {
            foreach (Transform child in originalParent)
            {
                child.gameObject.SetActive(true);
            }
        }

        // 通知 InspectManager 拖曳結束，解除鎖定並啟動離開計時
        if (InspectManager.Instance != null)
        {
            InspectManager.Instance.SetHoverDraggingState(false);
            InspectManager.Instance.DelayClearHoverSubObjects(0.25f);
        }
    }

    /// <summary>
    /// 當拖曳結束時，驗證當前的 UI 與劃過的內文關鍵字是否匹配
    /// </summary>
    private void TryVerifySubObjectOnRelease(PointerEventData eventData)
    {
        if (lastHoveredTMP == null || lastHoveredLinkIndex == -1)
        {
            // 如果放開瞬間沒有記錄到，再用當前位置補測一次
            CheckArticleLinkUnderPointer(eventData);
        }

        // 修正邏輯：如果放開時沒有落在任何有效的 TMP 連結上（例如拖到空白處），直接返回，不算失敗比對
        if (lastHoveredTMP == null || lastHoveredLinkIndex == -1)
        {
            Debug.Log("<color=yellow>[SubObjectDrag] Released outside any valid TMP link. Not counted as failure.</color>");
            return;
        }

        // 取得當前正在檢視的主物件
        InspectableObject currentTarget = InspectManager.Instance != null ? InspectManager.Instance.GetCurrentTarget() : null;
        if (currentTarget == null)
        {
            Debug.Log("<color=yellow>[SubObjectDrag] Current Target is null. Not counted as failure.</color>");
            return;
        }

        // 取得停留在上方 Link 的 ID
        if (lastHoveredLinkIndex >= lastHoveredTMP.textInfo.linkCount) return;

        TMP_LinkInfo linkInfo = lastHoveredTMP.textInfo.linkInfo[lastHoveredLinkIndex];
        string hoveredSubID = linkInfo.GetLinkID();

        Debug.Log($"<color=green>[SubObjectDrag] Release matched Link ID: {hoveredSubID}</color>");

        bool matchedAny = false;

        // 遍歷當前 InspectableObject 的子物件比對子 ID
        foreach (var sub in currentTarget.subObjectInfos)
        {
            if (sub == null) continue;

            TextMeshProUGUI selfTMP = GetComponentInChildren<TextMeshProUGUI>();
            string currentUIText = selfTMP != null ? selfTMP.text : "";

            // 檢查 ID 是否相符，且文字符合
            if (sub.subID == hoveredSubID && (currentUIText == sub.foundSubName || currentUIText == sub.verifiedSubName))
            {
                matchedAny = true;
                bool isNewlyVerified = currentTarget.TryVerifySubInfo(sub.subID);

                if (isNewlyVerified)
                {
                    VirtualBrowserController.persistentVerifySuccessCount++;
                    if (VirtualBrowserController.Instance != null)
                    {
                        VirtualBrowserController.Instance.PlaySuccessEffectAtMousePosition(eventData.position, eventData.pressEventCamera);
                    }

                    GameManager gm = FindObjectOfType<GameManager>();
                    if (gm != null) gm.RecordVerificationResult(true);

                    if (SoundManager.Instance != null)
                    {
                        string soundID = VirtualBrowserController.Instance != null ? VirtualBrowserController.Instance.successSoundID : "Success";
                        SoundManager.Instance.PlaySound(soundID);
                    }
                    Debug.Log($"<color=green>[SubObjectDrag] Verify SUCCESS! Total Success: {VirtualBrowserController.persistentVerifySuccessCount}</color>");
                }

                if (selfTMP != null && !string.IsNullOrEmpty(sub.verifiedSubName))
                {
                    selfTMP.text = sub.verifiedSubName;
                }

                if (InspectManager.Instance != null)
                {
                    InspectManager.Instance.UpdateCurrentSlotProgress(currentTarget);
                }

                break;
            }
        }

        // 修正邏輯：有確實放開在 TMP 連結上，但 ID 或內容不相符（代表拖錯對應的關鍵字），這才算比對失敗！
        if (!matchedAny)
        {
            VirtualBrowserController.persistentVerifyFailCount++;
            GameManager gm = FindObjectOfType<GameManager>();
            if (gm != null)
            {
                gm.RecordVerificationResult(false);
            }
            Debug.Log($"<color=red>[SubObjectDrag] Verify FAIL! Released on link '{hoveredSubID}' but mismatched. Total Fail Count: {VirtualBrowserController.persistentVerifyFailCount}</color>");
        }
    }

    /// <summary>
    /// 確保拖曳期間只要停留在連結範圍內，就會持續保持黃色高亮的偵測函數
    /// </summary>
    private void CheckArticleLinkUnderPointer(PointerEventData eventData)
    {
        if (VirtualBrowserController.Instance == null || VirtualBrowserController.Instance.articleContentText == null) return;

        TextMeshProUGUI tmp = VirtualBrowserController.Instance.articleContentText as TextMeshProUGUI;
        if (tmp == null || !tmp.gameObject.activeInHierarchy) return;

        tmp.ForceMeshUpdate();

        Canvas canvas = tmp.GetComponentInParent<Canvas>();
        Camera targetCam = null;

        if (canvas != null)
        {
            if (canvas.renderMode == RenderMode.ScreenSpaceCamera)
            {
                targetCam = canvas.worldCamera != null ? canvas.worldCamera : (eventData.pressEventCamera != null ? eventData.pressEventCamera : Camera.main);
            }
            else if (canvas.renderMode == RenderMode.WorldSpace)
            {
                targetCam = eventData.pressEventCamera != null ? eventData.pressEventCamera : Camera.main;
            }
        }

        Vector2 mouseScreenPos = eventData.position;
        float offset = 35f;
        Vector2[] samplePoints = new Vector2[]
        {
            mouseScreenPos,
            mouseScreenPos + new Vector2(offset, 0),
            mouseScreenPos + new Vector2(-offset, 0),
            mouseScreenPos + new Vector2(0, offset),
            mouseScreenPos + new Vector2(0, -offset)
        };

        int linkIndex = -1;
        foreach (var pt in samplePoints)
        {
            linkIndex = TMP_TextUtilities.FindIntersectingLink(tmp, pt, targetCam);
            if (linkIndex != -1) break;
        }

        if (linkIndex != lastHoveredLinkIndex)
        {
            ResetLastHoveredLinkColor();
        }

        if (linkIndex != -1)
        {
            SetLinkColor(tmp, linkIndex, Color.yellow);
            lastHoveredTMP = tmp;
            lastHoveredLinkIndex = linkIndex;
        }
        else
        {
            lastHoveredTMP = null;
            lastHoveredLinkIndex = -1;
        }
    }

    private void SetLinkColor(TextMeshProUGUI tmp, int linkIndex, Color color)
    {
        if (tmp == null || tmp.textInfo == null || linkIndex < 0 || linkIndex >= tmp.textInfo.linkCount) return;

        TMP_LinkInfo linkInfo = tmp.textInfo.linkInfo[linkIndex];

        for (int i = 0; i < linkInfo.linkTextLength; i++)
        {
            int characterIndex = linkInfo.linkTextfirstCharacterIndex + i;
            if (characterIndex >= tmp.textInfo.characterInfo.Length) continue;

            TMP_CharacterInfo charInfo = tmp.textInfo.characterInfo[characterIndex];

            if (!charInfo.isVisible) continue;

            int materialIndex = charInfo.materialReferenceIndex;
            int vertexIndex = charInfo.vertexIndex;

            if (materialIndex >= tmp.textInfo.meshInfo.Length) continue;
            Color32[] vertexColors = tmp.textInfo.meshInfo[materialIndex].colors32;

            if (vertexIndex + 3 >= vertexColors.Length) continue;

            vertexColors[vertexIndex + 0] = color;
            vertexColors[vertexIndex + 1] = color;
            vertexColors[vertexIndex + 2] = color;
            vertexColors[vertexIndex + 3] = color;
        }

        tmp.UpdateVertexData(TMP_VertexDataUpdateFlags.All);
    }

    private void ResetLastHoveredLinkColor()
    {
        if (lastHoveredTMP != null)
        {
            lastHoveredTMP.ForceMeshUpdate();
            lastHoveredTMP = null;
            lastHoveredLinkIndex = -1;
        }
    }

    private bool IsBrowserActive()
    {
        return VirtualBrowserController.Instance != null &&
               VirtualBrowserController.Instance.backgroundPanel != null &&
               VirtualBrowserController.Instance.backgroundPanel.activeInHierarchy;
    }
}