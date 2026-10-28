using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Rendering.Universal;
using TMPro;
using System.Reflection;

/// <summary>
/// Global Singleton Manager for 3D inspection, dynamic UI slots, and camera handling.
/// </summary>
public class InspectManager : MonoBehaviour
{
    public static InspectManager Instance { get; private set; }

    [Header("UI References")]
    public CanvasGroup fadeGroup;
    public RectTransform inventoryHotbar;
    public TextMeshProUGUI titleText;
    public GameObject taskText;
    public GameObject closeButton; // 檢視專用關閉按鈕

    [Header("Sub Object UI Settings")]
    public GameObject subObjectUIPrefab;
    public Transform subObjectUIParent;
    public float subObjectUIOffsetY = 80f;
    public float subObjectUISpacing = 20f;

    [Header("Hotbar Animation Settings")]
    public float hotbarOffsetY = 100f;
    public float hotbarAnimDuration = 0.3f;

    [Header("Dynamic UI Slot Settings")]
    public Vector2 slotSize = new Vector2(80f, 80f);
    public Sprite slotFrameSprite;
    public Vector2 slotFrameSize = new Vector2(100f, 100f);
    public Color dimColor = new Color(0.35f, 0.35f, 0.35f, 1f);

    [Header("Rotation and Zoom Settings")]
    public float rotateSpeed = 5f;
    public float zoomSpeed = 2f;
    public float minScale = 0.5f;
    public float maxScale = 2.5f;

    [Header("Inspect Position and Layer")]
    public Vector3 isolatedSpawnCenter = new Vector3(0f, -1000f, 0f);
    public float inspectDistance = 1.5f;
    public Vector3 positionOffset = Vector3.zero;
    public LayerMask inspectLayer;

    [Header("VFX Settings")]
    [Tooltip("Success effect prefab spawned at mouse position")]
    public GameObject successEffectPrefab;

    [Tooltip("VFX Camera")]
    public Camera vfxCamera;

    [Tooltip("Effect distance from camera")]
    public float effectDistanceFromCamera = 5.0f;

    [Tooltip("Effect sensitivity adjustment")]
    public Vector2 effectSensitivity = new Vector2(1.0f, 1.0f);

    [Header("Audio Settings")]
    [Tooltip("Sound ID played when sub-object found successfully")]
    public string subObjectFoundSoundID = "Success";

    private GameObject clonedObject;
    private Camera inspectCamera;
    private bool isInspecting = false;
    private Vector3 originalClonedScale;
    private float currentZoomMultiplier = 1f;

    private InspectableObject currentTarget;

    private GameObject activeInspectEffect;

    private Dictionary<InspectableObject, Image> activeDimSlotImages = new Dictionary<InspectableObject, Image>();
    private Dictionary<InspectableObject, Image> activeColorSlotImages = new Dictionary<InspectableObject, Image>();
    private Dictionary<InspectableObject, GameObject> activeFrameSlotObjects = new Dictionary<InspectableObject, GameObject>();

    private List<GameObject> activeSubObjectUIs = new List<GameObject>();

    private GameObject currentHoverPanel;
    private List<GameObject> spawnedHoverSubUIs = new List<GameObject>();
    private Coroutine clearHoverCoroutine;

    private bool isHoverDragging = false;

    private bool isTimelinePlaying = false;

    private Dictionary<MonoBehaviour, bool> previousGameManagerScriptStates = new Dictionary<MonoBehaviour, bool>();

    private Dictionary<object, float> originalAxisSpeeds = new Dictionary<object, float>();

    private Vector2 hotbarOriginalPos;
    private Vector2 hotbarLoweredPos;
    private bool isHotbarVisible = false;
    private Coroutine hotbarAnimCoroutine;

    private Dictionary<string, string> labelDictionary = new Dictionary<string, string>();

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

        if (inventoryHotbar != null)
        {
            EnsureVerticalLayout();
            hotbarOriginalPos = inventoryHotbar.anchoredPosition;
            hotbarLoweredPos = hotbarOriginalPos - new Vector2(hotbarOffsetY, 0f);
            inventoryHotbar.anchoredPosition = hotbarLoweredPos;
            inventoryHotbar.gameObject.SetActive(false);
            isHotbarVisible = false;
        }

        if (closeButton != null)
        {
            closeButton.SetActive(false);
        }

        LoadLabelCSV();
    }

    private void Update()
    {
        CheckVirtualBrowserState();

        if (isInspecting)
        {
            MaintainGameManagerControlsDisabled();
            HandleInput();
            UpdateInspectEffectPosition();
            return;
        }

        if (Input.GetKeyDown(KeyCode.E) && !isTimelinePlaying)
        {
            ToggleHotbar();
        }
        HandleWorldObjectSelection();
    }

    private void CheckVirtualBrowserState()
    {
        if (VirtualBrowserController.Instance != null && VirtualBrowserController.Instance.backgroundPanel != null)
        {
            bool isBrowserOpen = VirtualBrowserController.Instance.backgroundPanel.activeInHierarchy;

            if (isBrowserOpen && !isHotbarVisible && !isInspecting && !isTimelinePlaying)
            {
                SetHotbarVisible(true, true);
            }
            else if (!isBrowserOpen && isHotbarVisible && !isInspecting && !isTimelinePlaying)
            {
                SetHotbarVisible(false, true);
            }
        }
    }

    private void LoadLabelCSV()
    {
        TextAsset csvAsset = Resources.Load<TextAsset>("CSV/LabelGenerator");
        if (csvAsset == null)
        {
            Debug.LogWarning("[InspectManager Debug] Resources/CSV/LabelGenerator.csv file not found.");
            return;
        }

        string[] lines = csvAsset.text.Split(new char[] { '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries);
        foreach (string line in lines)
        {
            string[] cols = line.Split(',');
            if (cols.Length >= 2)
            {
                string displayName = cols[0].Trim();
                string hierarchyName = cols[1].Trim();

                if (!string.IsNullOrEmpty(hierarchyName) && !labelDictionary.ContainsKey(hierarchyName))
                {
                    labelDictionary.Add(hierarchyName, displayName);
                }
            }
        }
    }

    public void ToggleHotbar()
    {
        SetHotbarVisible(!isHotbarVisible, true);
    }

    public void SetHotbarVisible(bool visible, bool animate = true)
    {
        if (inventoryHotbar == null) return;

        if (isTimelinePlaying && visible)
        {
            visible = false;
        }

        isHotbarVisible = visible;

        if (hotbarAnimCoroutine != null)
        {
            StopCoroutine(hotbarAnimCoroutine);
        }

        if (animate && gameObject.activeInHierarchy)
        {
            hotbarAnimCoroutine = StartCoroutine(AnimateHotbar(visible));
        }
        else
        {
            inventoryHotbar.anchoredPosition = visible ? hotbarOriginalPos : hotbarLoweredPos;
            inventoryHotbar.gameObject.SetActive(visible);
        }
    }

    private IEnumerator AnimateHotbar(bool show)
    {
        inventoryHotbar.gameObject.SetActive(true);

        Vector2 startPos = inventoryHotbar.anchoredPosition;
        Vector2 targetPos = show ? hotbarOriginalPos : hotbarLoweredPos;
        float elapsed = 0f;

        while (elapsed < hotbarAnimDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / hotbarAnimDuration);
            t = Mathf.SmoothStep(0f, 1f, t);
            inventoryHotbar.anchoredPosition = Vector2.Lerp(startPos, targetPos, t);
            yield return null;
        }

        inventoryHotbar.anchoredPosition = targetPos;
        if (!show)
        {
            inventoryHotbar.gameObject.SetActive(false);
            ClearHoverSubObjectsImmediate();
        }
        hotbarAnimCoroutine = null;
    }

    public void SetTimelinePlaying(bool isPlaying)
    {
        isTimelinePlaying = isPlaying;

        if (inventoryHotbar != null && isPlaying)
        {
            SetHotbarVisible(false, false);
        }

        if (!isPlaying)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    public void SetHotbarActive(bool active)
    {
        SetHotbarVisible(active, true);
    }

    private void HandleWorldObjectSelection()
    {
        if (Input.GetMouseButtonDown(0))
        {
            if (isTimelinePlaying) return;

            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                PointerEventData pointerData = new PointerEventData(EventSystem.current)
                {
                    position = Input.mousePosition
                };
                List<RaycastResult> results = new List<RaycastResult>();
                EventSystem.current.RaycastAll(pointerData, results);

                int uiLayer = LayerMask.NameToLayer("UI");
                RaycastResult uiHit = results.Find(r => r.gameObject.layer == uiLayer);

                if (uiHit.isValid) return;
            }

            Camera mainCam = Camera.main;
            if (mainCam == null) return;

            Ray ray = mainCam.ScreenPointToRay(Input.mousePosition);

            if (Physics.Raycast(ray, out RaycastHit hit, 100f))
            {
                InspectableObject target = hit.collider.GetComponent<InspectableObject>();
                if (target == null) target = hit.collider.GetComponentInChildren<InspectableObject>();
                if (target == null) target = hit.collider.GetComponentInParent<InspectableObject>();

                if (target != null && target.enabled)
                {
                    StartInspect(target);
                }
            }
        }
    }

    private void EnsureVerticalLayout()
    {
        HorizontalLayoutGroup oldHorizontal = inventoryHotbar.GetComponent<HorizontalLayoutGroup>();
        if (oldHorizontal != null) Destroy(oldHorizontal);

        VerticalLayoutGroup layout = inventoryHotbar.GetComponent<VerticalLayoutGroup>();
        if (layout == null) layout = inventoryHotbar.gameObject.AddComponent<VerticalLayoutGroup>();

        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        layout.spacing = 15f;
    }

    public void StartInspect(InspectableObject target)
    {
        if (isInspecting || target == null || !target.enabled || isTimelinePlaying) return;

        GameManager gmCheck = FindObjectOfType<GameManager>();
        if (gmCheck != null)
        {
            gmCheck.RecordObjectInspected(target);
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        SetCurrentTarget(target);
        isInspecting = true;

        if (closeButton != null)
        {
            closeButton.SetActive(true);
        }

        PauseGameManagerControls();

        if (vfxCamera == null)
        {
            GameObject vfxCamObj = GameObject.Find("VFX Camera");
            if (vfxCamObj != null) vfxCamera = vfxCamObj.GetComponent<Camera>();
        }

        GameManager gm = FindObjectOfType<GameManager>();
        if (gm != null)
        {
            gm.SetPlayerInputActive(false);
        }

        Camera mainCam = Camera.main;
        if (mainCam == null)
        {
            isInspecting = false;
            if (closeButton != null) closeButton.SetActive(false);
            RestoreGameManagerControls();
            if (gm != null) gm.SetPlayerInputActive(true);
            return;
        }

        if (fadeGroup != null)
        {
            fadeGroup.alpha = 0.65f;
            fadeGroup.blocksRaycasts = true;
        }

        if (inventoryHotbar != null && !isTimelinePlaying)
        {
            SetHotbarVisible(true, true);
        }

        if (taskText != null) taskText.SetActive(false);

        SetupMainObjectSlot(currentTarget);
        CreateInspectCameraURP(mainCam);

        float activeDistance = (target.overrideInspectDistance > 0f) ? target.overrideInspectDistance : inspectDistance;

        Vector3 targetPosition = inspectCamera.transform.position + (inspectCamera.transform.forward * activeDistance);
        targetPosition += inspectCamera.transform.right * positionOffset.x;
        targetPosition += inspectCamera.transform.up * positionOffset.y;
        targetPosition += inspectCamera.transform.forward * positionOffset.z;

        Quaternion targetRotation = Quaternion.LookRotation(inspectCamera.transform.forward, inspectCamera.transform.up);

        clonedObject = Instantiate(target.gameObject, targetPosition, targetRotation);
        clonedObject.name = $"[INSPECT_CLONE] {target.gameObject.name}";

        RebindCloneSubObjectsState(target, clonedObject);

        originalClonedScale = clonedObject.transform.localScale;
        currentZoomMultiplier = 1f;

        int layerIndex = MaskToLayer(inspectLayer);
        SetLayerRecursively(clonedObject, layerIndex);

        if (titleText != null)
        {
            titleText.gameObject.SetActive(true);
            string hierarchyName = target.gameObject.name;
            titleText.text = labelDictionary.TryGetValue(hierarchyName, out string displayName) ? displayName : hierarchyName;
        }

        UpdateSubObjectUIs();
    }

    private void RebindCloneSubObjectsState(InspectableObject originalTarget, GameObject cloneObj)
    {
        InspectableObject cloneInspect = cloneObj.GetComponent<InspectableObject>() ?? cloneObj.GetComponentInChildren<InspectableObject>();
        if (cloneInspect == null || cloneInspect.subObjectInfos == null) return;

        foreach (var sub in cloneInspect.subObjectInfos)
        {
            if (sub != null && sub.subGameObject != null)
            {
                string childName = sub.subGameObject.name;
                Transform foundT = FindChildByName(cloneObj.transform, childName);
                if (foundT != null)
                {
                    sub.subGameObject = foundT.gameObject;
                }
            }
        }
    }

    private Transform FindChildByName(Transform parent, string targetName)
    {
        foreach (Transform child in parent)
        {
            if (child.name == targetName) return child;
            Transform res = FindChildByName(child, targetName);
            if (res != null) return res;
        }
        return null;
    }

    public void SetupMainObjectSlot(InspectableObject target)
    {
        if (inventoryHotbar == null || target == null) return;

        if (!activeDimSlotImages.ContainsKey(target))
        {
            GameObject slotGo = new GameObject($"Slot_{target.mainInfoID}", typeof(RectTransform));
            slotGo.transform.SetParent(inventoryHotbar, false);

            RectTransform slotRect = slotGo.GetComponent<RectTransform>();
            slotRect.sizeDelta = slotSize;
            slotRect.anchorMin = new Vector2(0.5f, 0.5f);
            slotRect.anchorMax = new Vector2(0.5f, 0.5f);
            slotRect.pivot = new Vector2(0.5f, 0.5f);

            GameObject frameGo = new GameObject("FrameIcon", typeof(RectTransform), typeof(Image));
            frameGo.transform.SetParent(slotGo.transform, false);

            RectTransform frameRect = frameGo.GetComponent<RectTransform>();
            frameRect.anchorMin = new Vector2(0.5f, 0.5f);
            frameRect.anchorMax = new Vector2(0.5f, 0.5f);
            frameRect.pivot = new Vector2(0.5f, 0.5f);
            frameRect.sizeDelta = slotFrameSize;

            Image frameImg = frameGo.GetComponent<Image>();
            frameImg.sprite = slotFrameSprite;
            frameImg.raycastTarget = false;
            frameGo.SetActive(false);

            GameObject dimGo = new GameObject("DimIcon", typeof(RectTransform), typeof(Image));
            dimGo.transform.SetParent(slotGo.transform, false);

            RectTransform dimRect = dimGo.GetComponent<RectTransform>();
            dimRect.anchorMin = Vector2.zero;
            dimRect.anchorMax = Vector2.one;
            dimRect.sizeDelta = Vector2.zero;
            dimRect.pivot = new Vector2(0.5f, 0.5f);

            Image dimImg = dimGo.GetComponent<Image>();
            dimImg.raycastTarget = false;

            GameObject colorGo = new GameObject("ColorIcon", typeof(RectTransform), typeof(Image));
            colorGo.transform.SetParent(slotGo.transform, false);

            RectTransform colorRect = colorGo.GetComponent<RectTransform>();
            colorRect.anchorMin = Vector2.zero;
            colorRect.anchorMax = Vector2.one;
            colorRect.sizeDelta = Vector2.zero;
            colorRect.pivot = new Vector2(0.5f, 0.5f);

            Image colorImg = colorGo.GetComponent<Image>();
            colorImg.raycastTarget = false;

            Image btnGraphic = slotGo.AddComponent<Image>();
            btnGraphic.color = new Color(0f, 0f, 0f, 0f);
            btnGraphic.raycastTarget = true;

            Button slotBtn = slotGo.AddComponent<Button>();
            slotBtn.targetGraphic = btnGraphic;

            InspectableObject capturedTarget = target;
            slotBtn.onClick.AddListener(() => SetCurrentTarget(capturedTarget));

            HotbarSlotPointerHandler hoverHandler = slotGo.AddComponent<HotbarSlotPointerHandler>();
            hoverHandler.targetObject = capturedTarget;

            activeDimSlotImages[target] = dimImg;
            activeColorSlotImages[target] = colorImg;
            activeFrameSlotObjects[target] = frameGo;
        }

        Image dimImage = activeDimSlotImages[target];
        Image colorImage = activeColorSlotImages[target];

        if (dimImage != null && colorImage != null && target.mainSprite != null)
        {
            dimImage.sprite = target.mainSprite;
            colorImage.sprite = target.mainSprite;

            dimImage.type = Image.Type.Filled;
            dimImage.fillMethod = Image.FillMethod.Vertical;
            dimImage.fillOrigin = (int)Image.OriginVertical.Top;
            dimColor.a = 1f;
            dimImage.color = dimColor;

            colorImage.type = Image.Type.Filled;
            colorImage.fillMethod = Image.FillMethod.Vertical;
            colorImage.fillOrigin = (int)Image.OriginVertical.Top;
            colorImage.color = Color.white;

            SetupAspectRatio(dimImage.gameObject, target.mainSprite);
            SetupAspectRatio(colorImage.gameObject, target.mainSprite);

            UpdateSlotFillProgress(target);
        }
    }

    private void SetupAspectRatio(GameObject targetGo, Sprite sprite)
    {
        AspectRatioFitter fitter = targetGo.GetComponent<AspectRatioFitter>();
        if (fitter == null) fitter = targetGo.AddComponent<AspectRatioFitter>();

        fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        if (sprite != null && sprite.rect.height > 0)
        {
            fitter.aspectRatio = sprite.rect.width / sprite.rect.height;
        }
    }

    public void SetCurrentTarget(InspectableObject target)
    {
        if (target == null) return;
        currentTarget = target;

        if (isInspecting)
        {
            UpdateSubObjectUIs();
        }
    }

    public void RegisterEvidenceSlot(InspectableObject target)
    {
        SetupMainObjectSlot(target);
    }

    private void UpdateSlotFillProgress(InspectableObject target)
    {
        if (target == null || !activeDimSlotImages.ContainsKey(target) || !activeColorSlotImages.ContainsKey(target)) return;

        Image dimImage = activeDimSlotImages[target];
        Image colorImage = activeColorSlotImages[target];
        if (dimImage == null || colorImage == null) return;

        int totalSub = target.GetActiveSubObjectCount();
        int foundSub = target.GetFoundCount();
        int verifiedSub = target.GetVerifiedCount();

        float foundRatio = (totalSub > 0) ? (float)foundSub / totalSub : 1f;
        dimImage.fillAmount = foundRatio;

        float verifiedRatio = (totalSub > 0) ? (float)verifiedSub / totalSub : 1f;
        colorImage.fillAmount = verifiedRatio;

        bool shouldShow = foundSub > 0;

        if (activeFrameSlotObjects.TryGetValue(target, out GameObject frameGo) && frameGo != null)
        {
            frameGo.SetActive(shouldShow);
        }

        dimImage.enabled = shouldShow;
        colorImage.enabled = shouldShow;
        dimImage.gameObject.SetActive(shouldShow);
        colorImage.gameObject.SetActive(shouldShow);

        if (verifiedRatio >= 1f && totalSub > 0)
        {
            if (!target.isMainEvidenceUnlocked)
            {
                target.isMainEvidenceUnlocked = true;

                if (EvidenceManager.Instance != null)
                {
                    EvidenceManager.Instance.CollectEvidence(target.mainInfoID);
                }
            }
        }
    }

    private void UpdateSubObjectUIs()
    {
        ClearSubObjectUIs();

        if (currentTarget == null || currentTarget.subObjectInfos == null || subObjectUIPrefab == null) return;

        Transform parentTransform = subObjectUIParent;

        if (parentTransform == null)
        {
            Canvas canvas = null;
            if (inventoryHotbar != null)
            {
                canvas = inventoryHotbar.GetComponentInParent<Canvas>();
            }
            if (canvas == null)
            {
                canvas = FindObjectOfType<Canvas>();
            }
            if (canvas == null) return;

            GameObject container = GameObject.Find("[SubObjectUI_Container]");
            if (container == null)
            {
                container = new GameObject("[SubObjectUI_Container]", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
                container.transform.SetParent(canvas.transform, false);

                RectTransform containerRect = container.GetComponent<RectTransform>();
                containerRect.anchorMin = new Vector2(0.5f, 0f);
                containerRect.anchorMax = new Vector2(0.5f, 0f);
                containerRect.pivot = new Vector2(0.5f, 0f);

                HorizontalLayoutGroup layout = container.GetComponent<HorizontalLayoutGroup>();
                layout.childAlignment = TextAnchor.MiddleCenter;
                layout.childControlWidth = false;
                layout.childControlHeight = false;
                layout.childForceExpandWidth = false;
                layout.childForceExpandHeight = false;

                ContentSizeFitter fitter = container.GetComponent<ContentSizeFitter>();
                fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }

            RectTransform cRect = container.GetComponent<RectTransform>();
            cRect.anchoredPosition = new Vector2(0f, subObjectUIOffsetY);

            HorizontalLayoutGroup hLayout = container.GetComponent<HorizontalLayoutGroup>();
            if (hLayout != null) hLayout.spacing = subObjectUISpacing;

            parentTransform = container.transform;
        }

        foreach (var sub in currentTarget.subObjectInfos)
        {
            if (sub != null && sub.subGameObject != null && sub.subGameObject.activeInHierarchy)
            {
                GameObject uiObj = Instantiate(subObjectUIPrefab, parentTransform, false);
                activeSubObjectUIs.Add(uiObj);

                TextMeshProUGUI tmpText = uiObj.GetComponentInChildren<TextMeshProUGUI>();
                if (tmpText != null)
                {
                    if (sub.isVerified)
                    {
                        tmpText.text = !string.IsNullOrEmpty(sub.verifiedSubName) ? sub.verifiedSubName : tmpText.text;
                    }
                    else if (sub.isFound)
                    {
                        tmpText.text = !string.IsNullOrEmpty(sub.foundSubName) ? sub.foundSubName : tmpText.text;
                    }
                }
            }
        }
    }

    private void ClearSubObjectUIs()
    {
        foreach (var uiObj in activeSubObjectUIs)
        {
            if (uiObj != null) Destroy(uiObj);
        }
        activeSubObjectUIs.Clear();
    }

    public void ShowHoverSubObjects(InspectableObject target, Transform slotTransform)
    {
        if (clearHoverCoroutine != null)
        {
            StopCoroutine(clearHoverCoroutine);
            clearHoverCoroutine = null;
        }

        if (currentHoverPanel != null) return;

        if (target == null || target.subObjectInfos == null || subObjectUIPrefab == null) return;

        List<SubObjectInfo> foundSubs = target.subObjectInfos.FindAll(s => s != null && s.subGameObject != null && s.subGameObject.activeInHierarchy && s.isFound);
        if (foundSubs.Count == 0) return;

        currentHoverPanel = new GameObject("[HoverSubObjectPanel]", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        currentHoverPanel.transform.SetParent(slotTransform, false);

        currentHoverPanel.AddComponent<HoverPanelPointerHandler>();

        RectTransform panelRect = currentHoverPanel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(1f, 0.5f);
        panelRect.anchorMax = new Vector2(1f, 0.5f);
        panelRect.pivot = new Vector2(0f, 0.5f);
        panelRect.anchoredPosition = new Vector2(15f, 0f);

        VerticalLayoutGroup layout = currentHoverPanel.GetComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        layout.spacing = 10f;

        ContentSizeFitter fitter = currentHoverPanel.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        foreach (var sub in foundSubs)
        {
            GameObject uiObj = Instantiate(subObjectUIPrefab, currentHoverPanel.transform, false);
            spawnedHoverSubUIs.Add(uiObj);

            if (uiObj.GetComponent<SubObjectUIDragHandler>() == null)
            {
                uiObj.AddComponent<SubObjectUIDragHandler>();
            }

            TextMeshProUGUI tmpText = uiObj.GetComponentInChildren<TextMeshProUGUI>();
            if (tmpText != null)
            {
                if (sub.isVerified)
                {
                    tmpText.text = !string.IsNullOrEmpty(sub.verifiedSubName) ? sub.verifiedSubName : tmpText.text;
                }
                else
                {
                    tmpText.text = !string.IsNullOrEmpty(sub.foundSubName) ? sub.foundSubName : tmpText.text;
                }
            }
        }
    }

    public void SetHoverDraggingState(bool isDragging)
    {
        isHoverDragging = isDragging;
        if (isDragging)
        {
            CancelDelayClearHover();
        }
    }

    public void DelayClearHoverSubObjects(float delay = 0.25f)
    {
        if (isHoverDragging) return;

        if (clearHoverCoroutine != null) StopCoroutine(clearHoverCoroutine);
        clearHoverCoroutine = StartCoroutine(HoverDelayClear(delay));
    }

    public void CancelDelayClearHover()
    {
        if (clearHoverCoroutine != null)
        {
            StopCoroutine(clearHoverCoroutine);
            clearHoverCoroutine = null;
        }
    }

    private IEnumerator HoverDelayClear(float delay)
    {
        yield return new WaitForSeconds(delay);

        if (!isHoverDragging)
        {
            ClearHoverSubObjectsImmediate();
        }
        clearHoverCoroutine = null;
    }

    public void ClearHoverSubObjectsImmediate()
    {
        if (isHoverDragging) return;

        CancelDelayClearHover();

        foreach (var uiObj in spawnedHoverSubUIs)
        {
            if (uiObj != null) Destroy(uiObj);
        }
        spawnedHoverSubUIs.Clear();

        if (currentHoverPanel != null)
        {
            Destroy(currentHoverPanel);
            currentHoverPanel = null;
        }
    }

    /// <summary>
    /// 記錄線索蒐集能力（Search & Exploration Profile）相關變數與分析維度至後端 (keys translated to Chinese)
    /// </summary>
    private void LogSearchExplorationMetrics(string actionEventName)
    {
        if (AnalyticsManager.Instance == null) return;

        InspectableObject[] allInspectables = FindObjectsOfType<InspectableObject>(true);
        int totalObjects = 0;
        int foundObjects = 0;
        int totalSubInfos = 0;
        int foundSubInfos = 0;

        if (allInspectables != null)
        {
            foreach (var obj in allInspectables)
            {
                if (obj == null || obj.name.Contains("[INSPECT_CLONE]")) continue;
                totalObjects++;

                if (obj.GetFoundCount() > 0 || obj.isMainEvidenceUnlocked)
                {
                    foundObjects++;
                }

                if (obj.subObjectInfos != null)
                {
                    foreach (var sub in obj.subObjectInfos)
                    {
                        if (sub != null)
                        {
                            totalSubInfos++;
                            if (sub.isFound || sub.isVerified)
                            {
                                foundSubInfos++;
                            }
                        }
                    }
                }
            }
        }

        // 計算分析維度：探索廣度 (Object Coverage) 與 細節敏銳度 (Sub-object Granularity)
        float objectCoverage = (totalObjects > 0) ? (float)foundObjects / totalObjects : 0f;
        float subObjectGranularity = (totalSubInfos > 0) ? (float)foundSubInfos / totalSubInfos : 0f;

        Dictionary<string, object> metrics = new Dictionary<string, object>
        {
            { "動作事件", actionEventName },
            { "目前目標代號", currentTarget != null ? currentTarget.mainInfoID : "None" },
            { "已發現物件數", foundObjects },
            { "總物件數", totalObjects },
            { "物件探索覆蓋率", objectCoverage },
            { "已發現子線索數", foundSubInfos },
            { "總子線索數", totalSubInfos },
            { "子線索細節敏銳度", subObjectGranularity }
        };

        AnalyticsManager.Instance.TrackEvent("線索蒐集探索檔案", metrics);
    }

    public void StopInspect()
    {
        if (!isInspecting) return;

        // 【新增】當玩家結束檢視時，記錄當前的線索蒐集與探索能力總結
        LogSearchExplorationMetrics("inspect_closed");

        isInspecting = false;

        if (closeButton != null)
        {
            closeButton.SetActive(false);
        }

        if (titleText != null) titleText.gameObject.SetActive(false);

        ClearSubObjectUIs();

        if (taskText != null) taskText.SetActive(true);

        if (activeInspectEffect != null)
        {
            Destroy(activeInspectEffect);
            activeInspectEffect = null;
        }

        if (currentTarget != null)
        {
            GameManager gm = FindObjectOfType<GameManager>();
            if (gm != null)
            {
                if (currentTarget.IsAllSubObjectsFound() && !string.IsNullOrEmpty(currentTarget.allFoundInkKnot))
                {
                    gm.ExecuteKnot(currentTarget.allFoundInkKnot);
                }

                if (currentTarget.IsAllSubObjectsVerified() && !string.IsNullOrEmpty(currentTarget.allVerifiedInkKnot))
                {
                    gm.ExecuteKnot(currentTarget.allVerifiedInkKnot);
                }
            }
        }

        if (fadeGroup != null)
        {
            fadeGroup.alpha = 0f;
            fadeGroup.blocksRaycasts = false;
        }

        if (inventoryHotbar != null)
        {
            SetHotbarVisible(false, true);
        }

        if (clonedObject != null) Destroy(clonedObject);

        RemoveInspectCameraURP();
        RestoreGameManagerControls();

        GameManager gmManager = FindObjectOfType<GameManager>();
        if (gmManager != null)
        {
            bool isBrowserOpen = VirtualBrowserController.Instance != null &&
                                 VirtualBrowserController.Instance.backgroundPanel != null &&
                                 VirtualBrowserController.Instance.backgroundPanel.activeInHierarchy;

            if (!isBrowserOpen)
            {
                gmManager.SetPlayerInputActive(true);
            }
        }
    }

    private void CreateInspectCameraURP(Camera mainCam)
    {
        if (inspectCamera != null) return;

        GameObject camGo = new GameObject("InspectCamera");
        camGo.transform.position = isolatedSpawnCenter;
        camGo.transform.rotation = mainCam.transform.rotation;

        inspectCamera = camGo.AddComponent<Camera>();
        inspectCamera.cullingMask = inspectLayer.value;

        var inspectCameraData = inspectCamera.GetUniversalAdditionalCameraData();
        inspectCameraData.renderType = CameraRenderType.Overlay;

        var mainCameraData = mainCam.GetUniversalAdditionalCameraData();
        if (mainCameraData != null)
        {
            mainCameraData.cameraStack.Insert(0, inspectCamera);
        }

        mainCam.cullingMask &= ~inspectLayer.value;
    }

    private void RemoveInspectCameraURP()
    {
        Camera mainCam = Camera.main;
        if (mainCam != null && inspectCamera != null)
        {
            mainCam.cullingMask |= inspectLayer.value;

            var mainCameraData = mainCam.GetUniversalAdditionalCameraData();
            if (mainCameraData != null)
            {
                mainCameraData.cameraStack.Remove(inspectCamera);
            }
        }

        if (inspectCamera != null)
        {
            Destroy(inspectCamera.gameObject);
            inspectCamera = null;
        }
    }

    private void HandleInput()
    {
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1))
        {
            StopInspect();
            return;
        }

        if (clonedObject == null || inspectCamera == null) return;

        if (Input.GetMouseButton(0))
        {
            float rotX = Input.GetAxis("Mouse X") * rotateSpeed;
            float rotY = Input.GetAxis("Mouse Y") * rotateSpeed;

            clonedObject.transform.Rotate(inspectCamera.transform.up, -rotX, Space.World);
            clonedObject.transform.Rotate(inspectCamera.transform.right, rotY, Space.World);
        }

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.01f)
        {
            currentZoomMultiplier += scroll * zoomSpeed;
            currentZoomMultiplier = Mathf.Clamp(currentZoomMultiplier, minScale, maxScale);
            clonedObject.transform.localScale = originalClonedScale * currentZoomMultiplier;
        }

        if (Input.GetMouseButtonDown(0))
        {
            Debug.Log("[InspectManager Debug] 偵測到滑鼠左鍵按下 (GetMouseButtonDown)。");

            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                string blockedUIName = "未知 UI 物件";
                bool isActualUI = false;

                if (EventSystem.current.currentSelectedGameObject != null)
                {
                    blockedUIName = EventSystem.current.currentSelectedGameObject.name;
                    isActualUI = true;
                }
                else
                {
                    PointerEventData pointerData = new PointerEventData(EventSystem.current)
                    {
                        position = Input.mousePosition
                    };
                    List<RaycastResult> results = new List<RaycastResult>();
                    EventSystem.current.RaycastAll(pointerData, results);

                    if (results.Count > 0)
                    {
                        blockedUIName = results[0].gameObject.name;

                        if (blockedUIName != "Floor" && results[0].gameObject.layer == LayerMask.NameToLayer("UI"))
                        {
                            isActualUI = true;
                        }
                    }
                }

                if (isActualUI)
                {
                    Debug.Log($"<color=orange>[InspectManager Debug] 點擊被阻擋：滑鼠目前正處於 UI 元素上方。阻擋的元兇是 UI: [{blockedUIName}]</color>");
                    return;
                }
                else
                {
                    Debug.Log($"[InspectManager Debug] 偵測到 IsPointerOverGameObject 誤判抓到了非 UI 物件 [{blockedUIName}]，已自動略過並允許 3D 點擊。");
                }
            }

            Ray ray = inspectCamera.ScreenPointToRay(Input.mousePosition);
            Debug.Log($"[InspectManager Debug] 從 inspectCamera 發射射線。滑鼠螢幕座標: {Input.mousePosition} | 射線起點: {ray.origin} | 射線方向: {ray.direction}");

            if (Physics.Raycast(ray, out RaycastHit hit, 100f, inspectLayer))
            {
                GameObject hitGo = hit.collider.gameObject;
                Debug.Log($"<color=green>[InspectManager Debug] 射線成功擊中碰撞體！</color> 擊中物件名稱: {hitGo.name} | 擊中圖層(Layer): {LayerMask.LayerToName(hitGo.layer)} | 距離: {hit.distance}");

                InspectableObject clonedInfo = clonedObject.GetComponent<InspectableObject>();
                if (clonedInfo == null) clonedInfo = clonedObject.GetComponentInChildren<InspectableObject>();

                if (clonedInfo != null && currentTarget != null)
                {
                    Debug.Log($"[InspectManager Debug] 成功取得 clonedInfo 與 currentTarget。開始比對子物件資訊...");

                    if (clonedInfo.TryGetSubObjectInfo(hitGo, out SubObjectInfo foundSubInfo, out int index))
                    {
                        Debug.Log($"<color=green>[InspectManager Debug] 子物件比對成功！</color> 找到對應 SubObject 索引: {index} | subID: {foundSubInfo.subID}");

                        if (foundSubInfo.subGameObject != null && foundSubInfo.subGameObject.activeInHierarchy)
                        {
                            Debug.Log($"[InspectManager Debug] 子物件狀態檢查：ActiveInHierarchy 為 true。目前 isFound 狀態: {foundSubInfo.isFound}");

                            if (!foundSubInfo.isFound)
                            {
                                Debug.Log($"<color=cyan>[InspectManager Debug] 首次發現該子物件！準備標記為 Found 並解鎖...</color>");
                                clonedInfo.MarkAsFound(index);
                                currentTarget.MarkAsFound(index);

                                if (!string.IsNullOrEmpty(foundSubInfo.foundInkKnot))
                                {
                                    GameManager gm = FindObjectOfType<GameManager>();
                                    if (gm != null)
                                    {
                                        Debug.Log($"[InspectManager Debug] 觸發對應的 Ink Knot: {foundSubInfo.foundInkKnot}");
                                        gm.ExecuteKnot(foundSubInfo.foundInkKnot);
                                    }
                                }

                                PlaySuccessEffectAtMousePosition(Input.mousePosition);

                                if (SoundManager.Instance != null && !string.IsNullOrEmpty(subObjectFoundSoundID))
                                {
                                    SoundManager.Instance.PlaySound(subObjectFoundSoundID);
                                }

                                UpdateSlotFillProgress(currentTarget);
                                UpdateSubObjectUIs();

                                // 【新增】當玩家成功點擊並發現新的子線索時，立即記錄線索蒐集能力數據
                                LogSearchExplorationMetrics("sub_object_found");
                            }
                            else
                            {
                                Debug.LogWarning($"[InspectManager Warning] 點擊到的子物件 [{hitGo.name}] 已經是 isFound = true 狀態了，故不重複觸發。");
                            }
                        }
                        else
                        {
                            Debug.LogWarning($"[InspectManager Warning] 點擊到的子物件匹配成功，但其 subGameObject 為 null 或在階層中是關閉的 (Inactive)。");
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"<color=orange>[InspectManager Warning] 射線打中了名為 [{hitGo.name}] 的碰撞體，但在 InspectableObject 的 subObjectInfos 列表中找不到任何匹配的子物件參考！</color>");
                    }
                }
                else
                {
                    Debug.LogError("[InspectManager Error] clonedInfo 或 currentTarget 為 null，無法進行子物件比對。");
                }
            }
            else
            {
                Debug.LogWarning($"<color=yellow>[InspectManager Warning] 射線發射後未擊中任何屬於 inspectLayer ({inspectLayer.value}) 層的 Collider。可能點空了，或者目標不在圖層內。</color>");
            }
        }
    }

    private Vector3 GetWorldPositionFromScreen(Vector2 screenPosition, Camera cam)
    {
        if (cam == null) cam = vfxCamera != null ? vfxCamera : Camera.main;
        if (cam == null) return Vector3.zero;

        float normalizedX = (screenPosition.x / Screen.width) - 0.5f;
        float normalizedY = (screenPosition.y / Screen.height) - 0.5f;

        normalizedX *= effectSensitivity.x;
        normalizedY *= effectSensitivity.y;

        float finalViewportX = normalizedX + 0.5f;
        float finalViewportY = normalizedY + 0.5f;

        Vector3 viewportPoint = new Vector3(finalViewportX, finalViewportY, effectDistanceFromCamera);
        return cam.ViewportToWorldPoint(viewportPoint);
    }

    public void PlaySuccessEffectAtMousePosition(Vector2 screenPosition)
    {
        if (successEffectPrefab == null) return;

        Camera targetCam = vfxCamera != null ? vfxCamera : Camera.main;
        Vector3 worldPosition = GetWorldPositionFromScreen(screenPosition, targetCam);

        GameObject effectObj = Instantiate(successEffectPrefab, worldPosition, Quaternion.identity);

        int effectsLayer = LayerMask.NameToLayer("Effects");
        SetLayerRecursively(effectObj, effectsLayer);

        ParticleSystem[] systems = effectObj.GetComponentsInChildren<ParticleSystem>();
        foreach (var ps in systems)
        {
            ps.Play();
        }

        Destroy(effectObj, 1.5f);
    }

    private void UpdateInspectEffectPosition()
    {
    }

    private void SetLayerRecursively(GameObject obj, int newLayer)
    {
        if (obj == null) return;
        obj.layer = newLayer;
        foreach (Transform child in obj.transform)
        {
            if (child != null)
            {
                SetLayerRecursively(child.gameObject, newLayer);
            }
        }
    }

    private int MaskToLayer(LayerMask mask)
    {
        int bitmask = mask.value;
        if (bitmask == 0) return 0;

        int layer = 0;
        while ((bitmask & 1) == 0)
        {
            bitmask >>= 1;
            layer++;
        }
        return layer;
    }

    public InspectableObject GetCurrentTarget()
    {
        return currentTarget;
    }

    public void UpdateCurrentSlotProgress(InspectableObject target)
    {
        UpdateSlotFillProgress(target);
        if (isInspecting && target == currentTarget)
        {
            UpdateSubObjectUIs();
        }
    }

    private void PauseGameManagerControls()
    {
        previousGameManagerScriptStates.Clear();
        originalAxisSpeeds.Clear();

        GameManager gm = FindObjectOfType<GameManager>();
        if (gm == null) return;

        if (gm.cinemachineCamera != null)
        {
            SetCinemachineCameraSpeed(gm.cinemachineCamera, 0f);
        }

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

        if (gm.cinemachineCamera != null)
        {
            SetCinemachineCameraSpeed(gm.cinemachineCamera, 0f);
        }

        if (gm.cameraControlScripts != null)
        {
            foreach (var script in gm.cameraControlScripts)
            {
                if (script != null && script.enabled)
                {
                    script.enabled = false;
                }
            }
        }
    }

    private void RestoreGameManagerControls()
    {
        GameManager gm = FindObjectOfType<GameManager>();
        if (gm != null && gm.cinemachineCamera != null)
        {
            RestoreCinemachineCameraSpeed();
        }

        foreach (var kvp in previousGameManagerScriptStates)
        {
            if (kvp.Key != null)
            {
                kvp.Key.enabled = kvp.Value;
            }
        }
        previousGameManagerScriptStates.Clear();
    }

    private void SetCinemachineCameraSpeed(MonoBehaviour camScript, float targetSpeed)
    {
        if (camScript == null) return;

        System.Type type = camScript.GetType();
        FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        PropertyInfo[] properties = type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        foreach (var field in fields)
        {
            if (IsAxisType(field.FieldType))
            {
                object axisValue = field.GetValue(camScript);
                if (axisValue != null)
                {
                    ModifyAxisSpeed(camScript, field, axisValue, targetSpeed);
                }
            }
        }

        foreach (var prop in properties)
        {
            if (prop.CanRead && prop.CanWrite && IsAxisType(prop.PropertyType))
            {
                object axisValue = prop.GetValue(camScript);
                if (axisValue != null)
                {
                    ModifyAxisSpeed(camScript, prop, axisValue, targetSpeed);
                }
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
                             ?? axisType.GetField("MaxSpeed", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                             ?? InspectManager_ModifyAxisSpeed_Fallback(axisType);

        PropertyInfo speedProp = axisType.GetProperty("m_MaxSpeed", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                              ?? axisType.GetProperty("MaxSpeed", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        float currentSpeed = 0f;
        bool found = false;

        if (speedField != null)
        {
            currentSpeed = (float)speedField.GetValue(axisStruct);
            found = true;
        }
        else if (speedProp != null && speedProp.CanRead)
        {
            currentSpeed = (float)speedProp.GetValue(axisStruct);
            found = true;
        }

        if (found)
        {
            string key = $"{owner.GetInstanceID()}_{member.Name}";
            if (!originalAxisSpeeds.ContainsKey(key))
            {
                originalAxisSpeeds[key] = currentSpeed;
            }

            if (speedField != null) speedField.SetValue(axisStruct, targetSpeed);
            else if (speedProp != null && speedProp.CanWrite) speedProp.SetValue(axisStruct, targetSpeed);

            if (member is FieldInfo fInfo) fInfo.SetValue(owner, axisStruct);
            else if (member is PropertyInfo pInfo && pInfo.CanWrite) pInfo.SetValue(owner, axisStruct);
        }
    }

    private FieldInfo InspectManager_ModifyAxisSpeed_Fallback(System.Type axisType)
    {
        return axisType.GetField("m_Speed", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            ?? axisType.GetField("Speed", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
    }

    private void RestoreCinemachineCameraSpeed()
    {
        GameManager gm = FindObjectOfType<GameManager>();
        if (gm == null || gm.cinemachineCamera == null) return;

        MonoBehaviour camScript = gm.cinemachineCamera;
        System.Type type = camScript.GetType();

        FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        PropertyInfo[] properties = type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        foreach (var field in fields)
        {
            if (IsAxisType(field.FieldType))
            {
                string key = $"{camScript.GetInstanceID()}_{field.Name}";
                if (originalAxisSpeeds.TryGetValue(key, out float origSpeed))
                {
                    object axisValue = field.GetValue(camScript);
                    if (axisValue != null)
                    {
                        ModifyAxisSpeed(camScript, field, axisValue, origSpeed);
                    }
                }
            }
        }

        originalAxisSpeeds.Clear();
    }
}

public class HotbarSlotPointerHandler : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public InspectableObject targetObject;

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (VirtualBrowserController.Instance != null && IsBrowserActive())
        {
            InspectManager.Instance.ShowHoverSubObjects(targetObject, transform);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (VirtualBrowserController.Instance != null && IsBrowserActive())
        {
            InspectManager.Instance.DelayClearHoverSubObjects(0.25f);
        }
    }

    private bool IsBrowserActive()
    {
        return VirtualBrowserController.Instance.backgroundPanel != null &&
               VirtualBrowserController.Instance.backgroundPanel.activeInHierarchy;
    }
}

public class HoverPanelPointerHandler : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public void OnPointerEnter(PointerEventData eventData)
    {
        InspectManager.Instance.CancelDelayClearHover();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        InspectManager.Instance.DelayClearHoverSubObjects(0.25f);
    }
}