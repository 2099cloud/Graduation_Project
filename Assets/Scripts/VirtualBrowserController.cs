using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Reflection;
using UnityEngine.EventSystems;

public class VirtualBrowserController : MonoBehaviour
{
    public static VirtualBrowserController Instance { get; private set; }

    [System.Serializable]
    public class PageButtonInfo
    {
        public GameObject buttonPrefab;
        public string targetPageID;
        public Vector2 position = Vector2.zero;
        public Vector2 size = new Vector2(100f, 100f);
    }

    [System.Serializable]
    public class WebPage
    {
        public string pageID;
        public Sprite backgroundSprite;
        public List<PageButtonInfo> pageButtons = new List<PageButtonInfo>();
        public FraudArticleSO articleData;
    }

    [Header("UI Settings")]
    public GameObject backgroundPanel;
    public TMP_Text articleContentText;
    public CanvasGroup fadeGroup;

    [Header("VFX Settings")]
    public GameObject successEffectPrefab;
    public float effectDistanceFromCamera = 5.0f;
    public Vector2 effectSensitivity = new Vector2(1.0f, 1.0f);

    [Header("Audio Settings")]
    public string successSoundID = "Success";

    [Header("Navigation Buttons")]
    public Button backButton;
    public Button forwardButton;
    public Button closeButton;
    public Button prevArticleSectionButton;
    public Button nextArticleSectionButton;

    public GameObject tasktext;

    [Header("Page Settings")]
    public string defaultPageID = "Home";
    public List<WebPage> webPages = new List<WebPage>();

    private Dictionary<string, WebPage> pageDict = new Dictionary<string, WebPage>();
    private Stack<string> backStack = new Stack<string>();
    private Stack<string> forwardStack = new Stack<string>();
    private WebPage currentPage;
    private TMPLinkHandler currentLinkHandler;
    private bool isBrowserOpen = false;
    private int currentArticleSectionIndex = 0;
    private List<GameObject> spawnedButtons = new List<GameObject>();
    private GameObject activeCursorEffect;

    // 獨立記錄第二關比對次數
    public static int persistentVerifyFailCount = 0;
    public static int persistentVerifySuccessCount = 0;

    private Transform originalSubObjectUIParent;
    private Dictionary<object, float> originalAxisSpeeds = new Dictionary<object, float>();
    private Dictionary<MonoBehaviour, bool> previousGameManagerScriptStates = new Dictionary<MonoBehaviour, bool>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        foreach (var page in webPages)
        {
            if (page != null && !string.IsNullOrEmpty(page.pageID))
            {
                pageDict[page.pageID] = page;
            }
        }

        if (articleContentText != null) articleContentText.gameObject.SetActive(false);
        if (backgroundPanel != null) backgroundPanel.SetActive(false);

        SetupNavigationButtons();
    }

    private void SetupNavigationButtons()
    {
        if (backButton != null) { backButton.onClick.RemoveAllListeners(); backButton.onClick.AddListener(GoBack); }
        if (forwardButton != null) { forwardButton.onClick.RemoveAllListeners(); forwardButton.onClick.AddListener(GoForward); }
        if (closeButton != null) { closeButton.onClick.RemoveAllListeners(); closeButton.onClick.AddListener(CloseBrowser); }
        if (prevArticleSectionButton != null) { prevArticleSectionButton.onClick.RemoveAllListeners(); prevArticleSectionButton.onClick.AddListener(GoToPrevArticleSection); }
        if (nextArticleSectionButton != null) { nextArticleSectionButton.onClick.RemoveAllListeners(); nextArticleSectionButton.onClick.AddListener(GoToNextArticleSection); }
    }

    private void Update()
    {
        if (isBrowserOpen)
        {
            if (Input.GetKeyDown(KeyCode.Escape)) { CloseBrowser(); return; }
            MaintainGameManagerControlsDisabled();
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            if (IsPointerOverUIElement()) return;
            DetectVirtualBrowserClick();
        }
    }

    private void DetectVirtualBrowserClick()
    {
        Camera mainCam = Camera.main;
        if (mainCam == null) return;

        Ray ray = mainCam.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 100f))
        {
            if (hit.collider.gameObject == gameObject || hit.collider.transform.IsChildOf(transform) || transform.IsChildOf(hit.collider.transform) || hit.collider.GetComponent<VirtualBrowserController>() != null)
            {
                OpenBrowser();
            }
        }
    }

    private bool IsPointerOverUIElement()
    {
        if (EventSystem.current == null) return false;
        PointerEventData pointerData = new PointerEventData(EventSystem.current) { position = Input.mousePosition };
        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerData, results);
        foreach (var result in results)
        {
            if (result.module is GraphicRaycaster)
            {
                if (fadeGroup != null && (result.gameObject == fadeGroup.gameObject || result.gameObject.transform.IsChildOf(fadeGroup.transform))) continue;
                return true;
            }
        }
        return false;
    }

    public void OpenBrowser()
    {
        if (backgroundPanel == null) return;
        backgroundPanel.SetActive(true);
        isBrowserOpen = true;

        PauseGameManagerControls();
        GameManager gm = FindObjectOfType<GameManager>();
        if (gm != null) gm.SetPlayerInputActive(false);

        if (fadeGroup != null) { fadeGroup.alpha = 0.65f; fadeGroup.blocksRaycasts = true; }
        if (tasktext != null) tasktext.SetActive(false);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        backStack.Clear();
        forwardStack.Clear();
        SwitchToPageInternal(defaultPageID, false, false);
    }

    public void CloseBrowser()
    {
        // 【新增】當玩家關閉虛擬瀏覽器時，記錄資訊查證能力的總結數據
        LogVerificationMetrics("browser_closed");

        UnbindCurrentLinkHandler();
        if (articleContentText != null) articleContentText.gameObject.SetActive(false);
        ClearSpawnedButtons();
        ResetArticleSectionButtons();

        if (backgroundPanel != null) backgroundPanel.SetActive(false);
        if (fadeGroup != null) { fadeGroup.alpha = 0f; fadeGroup.blocksRaycasts = false; }

        RestoreGameManagerControls();
        if (tasktext != null) tasktext.SetActive(true);

        isBrowserOpen = false;
        currentPage = null;
        backStack.Clear();
        forwardStack.Clear();

        GameManager gmManager = FindObjectOfType<GameManager>();
        if (gmManager != null)
        {
            gmManager.SetPlayerInputActive(true);
        }
    }

    public void SwitchToPage(string pageID) { SwitchToPageInternal(pageID, true, true); }

    private void SwitchToPageInternal(string pageID, bool recordBackHistory, bool clearForwardHistory)
    {
        if (!pageDict.ContainsKey(pageID)) return;
        UnbindCurrentLinkHandler();
        if (articleContentText != null) articleContentText.gameObject.SetActive(false);
        ClearSpawnedButtons();

        if (recordBackHistory && currentPage != null) backStack.Push(currentPage.pageID);
        if (clearForwardHistory) forwardStack.Clear();

        currentPage = pageDict[pageID];

        if (backgroundPanel != null)
        {
            Image bgImage = backgroundPanel.GetComponent<Image>();
            if (bgImage != null && currentPage.backgroundSprite != null) bgImage.sprite = currentPage.backgroundSprite;
        }

        currentArticleSectionIndex = 0;
        RenderPageButtons(currentPage);
        LoadArticleAndBindLinkHandler(currentPage);
        UpdateNavigationButtonsUI();
    }

    private void RenderPageButtons(WebPage page)
    {
        if (page == null || page.pageButtons == null || backgroundPanel == null) return;
        foreach (var btnInfo in page.pageButtons)
        {
            if (btnInfo == null || btnInfo.buttonPrefab == null) continue;
            GameObject btnObj = Instantiate(btnInfo.buttonPrefab, backgroundPanel.transform, false);
            spawnedButtons.Add(btnObj);
            Button btn = btnObj.GetComponent<Button>() ?? btnObj.GetComponentInChildren<Button>();
            RectTransform rectTransform = btnObj.GetComponent<RectTransform>();
            if (rectTransform != null) { rectTransform.anchoredPosition = btnInfo.position; rectTransform.sizeDelta = btnInfo.size; }
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                string targetID = btnInfo.targetPageID;
                btn.onClick.AddListener(() => SwitchToPage(targetID));
            }
            btnObj.SetActive(true);
        }
    }

    private void ResetArticleSectionButtons()
    {
        if (prevArticleSectionButton != null) prevArticleSectionButton.gameObject.SetActive(false);
        if (nextArticleSectionButton != null) nextArticleSectionButton.gameObject.SetActive(false);
    }

    private void ClearSpawnedButtons()
    {
        foreach (var btnObj in spawnedButtons) { if (btnObj != null) Destroy(btnObj); }
        spawnedButtons.Clear();
    }

    private void LoadArticleAndBindLinkHandler(WebPage page)
    {
        if (page == null || articleContentText == null) return;
        if (page.articleData != null && page.articleData.BodyCount > 0)
        {
            DisplayCurrentArticleSection();
            articleContentText.gameObject.SetActive(true);
        }
        else
        {
            articleContentText.gameObject.SetActive(false);
            UpdateArticleSectionButtonsUI();
            return;
        }

        currentLinkHandler = articleContentText.GetComponent<TMPLinkHandler>();
        if (currentLinkHandler != null) currentLinkHandler.OnLinkClicked += ProcessVerification;
    }

    private void DisplayCurrentArticleSection()
    {
        if (currentPage == null || currentPage.articleData == null || articleContentText == null) return;
        string sectionText = currentPage.articleData.GetBody(currentArticleSectionIndex);
        articleContentText.text = !string.IsNullOrEmpty(sectionText) ? sectionText : string.Empty;
        UpdateArticleSectionButtonsUI();
    }

    public void GoToPrevArticleSection()
    {
        if (currentPage == null || currentPage.articleData == null) return;
        if (currentArticleSectionIndex > 0) { currentArticleSectionIndex--; DisplayCurrentArticleSection(); }
    }

    public void GoToNextArticleSection()
    {
        if (currentPage == null || currentPage.articleData == null) return;
        if (currentArticleSectionIndex < currentPage.articleData.BodyCount - 1) { currentArticleSectionIndex++; DisplayCurrentArticleSection(); }
    }

    private void UnbindCurrentLinkHandler()
    {
        if (currentLinkHandler != null)
        {
            currentLinkHandler.OnLinkClicked -= ProcessVerification;
            currentLinkHandler = null;
        }
    }

    public void GoBack()
    {
        if (backStack.Count > 0)
        {
            if (currentPage != null) forwardStack.Push(currentPage.pageID);
            SwitchToPageInternal(backStack.Pop(), false, false);
        }
    }

    public void GoForward()
    {
        if (forwardStack.Count > 0)
        {
            if (currentPage != null) backStack.Push(currentPage.pageID);
            SwitchToPageInternal(forwardStack.Pop(), false, false);
        }
    }

    public void GoHome() { SwitchToPage(defaultPageID); }

    private void UpdateNavigationButtonsUI()
    {
        if (backButton != null) backButton.interactable = (backStack.Count > 0);
        if (forwardButton != null) forwardButton.interactable = (forwardStack.Count > 0);
        UpdateArticleSectionButtonsUI();
    }

    private void UpdateArticleSectionButtonsUI()
    {
        bool hasArticle = (currentPage != null && currentPage.articleData != null && currentPage.articleData.BodyCount > 0);
        if (prevArticleSectionButton != null) { prevArticleSectionButton.gameObject.SetActive(hasArticle); prevArticleSectionButton.interactable = hasArticle && (currentArticleSectionIndex > 0); }
        if (nextArticleSectionButton != null) { nextArticleSectionButton.gameObject.SetActive(hasArticle); nextArticleSectionButton.interactable = hasArticle && (currentArticleSectionIndex < currentPage.articleData.BodyCount - 1); }
    }

    /// <summary>
    /// 記錄資訊查證能力（Verification & Logic Profile）相關變數與查證精準度至後端 (keys translated to Chinese)
    /// </summary>
    private void LogVerificationMetrics(string actionEventName, string snippetID = "")
    {
        if (AnalyticsManager.Instance == null) return;

        int totalAttempts = persistentVerifySuccessCount + persistentVerifyFailCount;
        float precisionRate = (totalAttempts > 0) ? (float)persistentVerifySuccessCount / totalAttempts : 0f;

        Dictionary<string, object> metrics = new Dictionary<string, object>
        {
            { "動作事件", actionEventName },
            { "片段代號", snippetID },
            { "查證成功次數", persistentVerifySuccessCount },
            { "查證失敗次數", persistentVerifyFailCount },
            { "總嘗試次數", totalAttempts },
            { "查證精準率", precisionRate }
        };

        AnalyticsManager.Instance.TrackEvent("資訊查證邏輯檔案", metrics);
    }

    private void ProcessVerification(string snippetID)
    {
        Debug.Log($"<color=cyan>[VirtualBrowser Debug] ProcessVerification triggered with snippetID: {snippetID}</color>");

        if (InspectManager.Instance == null)
        {
            Debug.LogWarning("[VirtualBrowser Warning] InspectManager.Instance is NULL!");
            return;
        }

        InspectableObject currentTarget = InspectManager.Instance.GetCurrentTarget();
        if (currentTarget == null)
        {
            Debug.LogWarning("[VirtualBrowser Warning] Current target InspectableObject is NULL!");
            persistentVerifyFailCount++;
            GameManager gmFail = FindObjectOfType<GameManager>();
            if (gmFail != null) gmFail.RecordVerificationResult(false);

            // 【新增】記錄查證失敗數據
            LogVerificationMetrics("verify_fail_no_target", snippetID);

            Debug.Log($"<color=red>[VirtualBrowser Debug] Verify FAIL (No Target)! Total Fail Count: {persistentVerifyFailCount}</color>");
            return;
        }

        bool isMatch = currentTarget.TryVerifySubInfo(snippetID);
        if (isMatch)
        {
            persistentVerifySuccessCount++;
            InspectManager.Instance.UpdateCurrentSlotProgress(currentTarget);
            GameManager gm = FindObjectOfType<GameManager>();
            if (gm != null) gm.RecordVerificationResult(true);
            PlaySuccessEffectAtMousePosition(Input.mousePosition, Camera.main);
            if (SoundManager.Instance != null && !string.IsNullOrEmpty(successSoundID)) SoundManager.Instance.PlaySound(successSoundID);

            // 【新增】記錄查證成功數據
            LogVerificationMetrics("verify_success", snippetID);

            Debug.Log($"<color=green>[VirtualBrowser Debug] Verify SUCCESS! Total Success Count: {persistentVerifySuccessCount}</color>");
        }
        else
        {
            persistentVerifyFailCount++;
            GameManager gm = FindObjectOfType<GameManager>();
            if (gm != null) gm.RecordVerificationResult(false);

            // 【新增】記錄查證失敗數據
            LogVerificationMetrics("verify_fail", snippetID);

            Debug.Log($"<color=red>[VirtualBrowser Debug] Verify FAIL! Total Fail Count: {persistentVerifyFailCount}</color>");
        }
    }

    private Vector3 GetWorldPositionFromScreen(Vector2 screenPosition, Camera cam)
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return Vector3.zero;
        float nx = ((screenPosition.x / Screen.width) - 0.5f) * effectSensitivity.x + 0.5f;
        float ny = ((screenPosition.y / Screen.height) - 0.5f) * effectSensitivity.y + 0.5f;
        return cam.ViewportToWorldPoint(new Vector3(nx, ny, effectDistanceFromCamera));
    }

    private void SetLayerRecursively(GameObject obj, int newLayer)
    {
        if (obj == null) return;
        obj.layer = newLayer;
        foreach (Transform child in obj.transform) SetLayerRecursively(child.gameObject, newLayer);
    }

    public void PlaySuccessEffectAtMousePosition(Vector2 screenPosition, Camera cam)
    {
        if (successEffectPrefab == null) return;
        GameObject effectObj = Instantiate(successEffectPrefab, GetWorldPositionFromScreen(screenPosition, cam), Quaternion.identity);
        SetLayerRecursively(effectObj, LayerMask.NameToLayer("Effects"));
        foreach (var ps in effectObj.GetComponentsInChildren<ParticleSystem>()) ps.Play();
        Destroy(effectObj, 1.5f);
    }

    private void PauseGameManagerControls()
    {
        previousGameManagerScriptStates.Clear();
        originalAxisSpeeds.Clear();
        GameManager gm = FindObjectOfType<GameManager>();
        if (gm == null) return;
        if (gm.cinemachineCamera != null) SetCinemachineCameraSpeed(gm.cinemachineCamera, 0f);
        if (gm.cameraControlScripts != null)
        {
            foreach (var script in gm.cameraControlScripts)
            {
                if (script != null && !previousGameManagerScriptStates.ContainsKey(script))
                {
                    previousGameManagerScriptStates[script] = script.enabled;
                    script.enabled = false;
                }
            }
        }
    }

    private void MaintainGameManagerControlsDisabled()
    {
        GameManager gm = FindObjectOfType<GameManager>();
        if (gm == null) return;
        if (gm.cinemachineCamera != null) SetCinemachineCameraSpeed(gm.cinemachineCamera, 0f);
        if (gm.cameraControlScripts != null)
        {
            foreach (var script in gm.cameraControlScripts)
            {
                if (script != null && script.enabled) script.enabled = false;
            }
        }
    }

    private void RestoreGameManagerControls()
    {
        GameManager gm = FindObjectOfType<GameManager>();
        if (gm != null && gm.cinemachineCamera != null) RestoreCinemachineCameraSpeed();
        foreach (var kvp in previousGameManagerScriptStates)
        {
            if (kvp.Key != null) kvp.Key.enabled = kvp.Value;
        }
        previousGameManagerScriptStates.Clear();
    }

    private void SetCinemachineCameraSpeed(MonoBehaviour camScript, float targetSpeed)
    {
        if (camScript == null) return;
        System.Type type = camScript.GetType();
        foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
        {
            if (IsAxisType(field.FieldType))
            {
                object val = field.GetValue(camScript);
                if (val != null) ModifyAxisSpeed(camScript, field, val, targetSpeed);
            }
        }
    }

    private bool IsAxisType(System.Type type)
    {
        string name = type.Name;
        return name.Contains("AxisBase") || name.Contains("CinemachineOrbitalTransposer") || name.Contains("POV") || name.Contains("Axis");
    }

    private void ModifyAxisSpeed(MonoBehaviour owner, MemberInfo member, object axisStruct, float targetSpeed)
    {
        if (owner == null || axisStruct == null) return;
        System.Type axisType = axisStruct.GetType();
        FieldInfo speedField = axisType.GetField("m_MaxSpeed", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                             ?? axisType.GetField("MaxSpeed", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (speedField != null)
        {
            string key = $"{owner.GetInstanceID()}_{member.Name}";
            if (!originalAxisSpeeds.ContainsKey(key)) originalAxisSpeeds[key] = (float)speedField.GetValue(axisStruct);
            speedField.SetValue(axisStruct, targetSpeed);
            if (member is FieldInfo f) f.SetValue(owner, axisStruct);
        }
    }

    private void RestoreCinemachineCameraSpeed()
    {
        GameManager gm = FindObjectOfType<GameManager>();
        if (gm == null || gm.cinemachineCamera == null) return;
        MonoBehaviour camScript = gm.cinemachineCamera;
        foreach (var field in camScript.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
        {
            if (IsAxisType(field.FieldType))
            {
                string key = $"{camScript.GetInstanceID()}_{field.Name}";
                if (originalAxisSpeeds.TryGetValue(key, out float orig))
                {
                    object val = field.GetValue(camScript);
                    if (val != null) ModifyAxisSpeed(camScript, field, val, orig);
                }
            }
        }
        originalAxisSpeeds.Clear();
    }
}