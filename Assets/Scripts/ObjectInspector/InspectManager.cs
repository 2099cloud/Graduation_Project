using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Global Singleton Manager for 3D inspection, dynamic UI slots, and camera handling.
/// </summary>
public class InspectManager : MonoBehaviour
{
    public static InspectManager Instance { get; private set; }

    [Header("UI References")]
    public CanvasGroup fadeGroup;
    public RectTransform inventoryHotbar;

    [Header("Hotbar Animation Settings")]
    public float hotbarOffsetY = 100f;
    public float hotbarAnimDuration = 0.3f;

    [Header("Dynamic UI Slot Settings")]
    public Vector2 slotSize = new Vector2(80f, 80f);
    [Tooltip("Low saturation / dim color when not fully unlocked")]
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

    private GameObject clonedObject;
    private Camera inspectCamera;
    private bool isInspecting = false;
    private Vector3 originalClonedScale;
    private float currentZoomMultiplier = 1f;

    private InspectableObject currentTarget;

    private Dictionary<InspectableObject, Image> activeDimSlotImages = new Dictionary<InspectableObject, Image>();
    private Dictionary<InspectableObject, Image> activeColorSlotImages = new Dictionary<InspectableObject, Image>();

    private bool isTimelinePlaying = false;

    private Dictionary<MonoBehaviour, bool> previousGameManagerScriptStates = new Dictionary<MonoBehaviour, bool>();

    private Vector2 hotbarOriginalPos;
    private Vector2 hotbarLoweredPos;
    private bool isHotbarVisible = false;
    private Coroutine hotbarAnimCoroutine;

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
            EnsureHorizontalLayout();
            hotbarOriginalPos = inventoryHotbar.anchoredPosition;
            hotbarLoweredPos = hotbarOriginalPos - new Vector2(0f, hotbarOffsetY);
            inventoryHotbar.anchoredPosition = hotbarLoweredPos;
            inventoryHotbar.gameObject.SetActive(false);
            isHotbarVisible = false;
        }
    }

    private void Update()
    {
        if (!isInspecting)
        {
            if (Input.GetKeyDown(KeyCode.E) && !isTimelinePlaying)
            {
                ToggleHotbar();
            }
            HandleWorldObjectSelection();
            return;
        }

        MaintainGameManagerControlsDisabled();
        HandleInput();
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
        }
        hotbarAnimCoroutine = null;
    }

    /// <summary>
    /// Call from Timeline or GameManager to toggle Timeline playing state.
    /// </summary>
    public void SetTimelinePlaying(bool isPlaying)
    {
        isTimelinePlaying = isPlaying;
        Debug.Log($"[InspectManager Debug] Timeline playing state changed to: {isPlaying}");

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
            if (isTimelinePlaying)
            {
                Debug.LogWarning("[InspectManager Debug] World object click ignored because isTimelinePlaying is TRUE.");
                return;
            }

            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                PointerEventData pointerData = new PointerEventData(EventSystem.current)
                {
                    position = Input.mousePosition
                };
                List<RaycastResult> results = new List<RaycastResult>();
                EventSystem.current.RaycastAll(pointerData, results);

                foreach (var r in results)
                {
                    if (fadeGroup != null && (r.gameObject == fadeGroup.gameObject || r.gameObject.transform.IsChildOf(fadeGroup.transform)))
                    {
                        continue;
                    }

                    Debug.Log($"[InspectManager Debug] Click blocked by UI: [{r.gameObject.name}] (Layer: {LayerMask.LayerToName(r.gameObject.layer)})");
                    return;
                }
            }

            Camera mainCam = Camera.main;
            if (mainCam == null)
            {
                Debug.LogWarning("[InspectManager Debug] MainCamera tag not found! Cannot cast Raycast.");
                return;
            }

            Ray ray = mainCam.ScreenPointToRay(Input.mousePosition);
            Debug.Log($"[InspectManager Debug] Casting Raycast from camera: {mainCam.name}, mouse pos: {Input.mousePosition}");

            if (Physics.Raycast(ray, out RaycastHit hit, 100f))
            {
                Debug.Log($"[InspectManager Debug] Raycast hit collider: [{hit.collider.gameObject.name}] (Layer: {LayerMask.LayerToName(hit.collider.gameObject.layer)})");

                InspectableObject target = hit.collider.GetComponent<InspectableObject>();
                if (target == null) target = hit.collider.GetComponentInChildren<InspectableObject>();
                if (target == null) target = hit.collider.GetComponentInParent<InspectableObject>();

                if (target != null && target.enabled)
                {
                    Debug.Log($"[InspectManager Debug] InspectableObject found: {target.name}. Opening inspect mode.");
                    StartInspect(target);
                }
                else if (target != null && !target.enabled)
                {
                    Debug.LogWarning($"[InspectManager Debug] Hit [{hit.collider.name}], but InspectableObject component is disabled.");
                }
                else
                {
                    Debug.LogWarning($"[InspectManager Debug] Hit [{hit.collider.name}], but no InspectableObject script attached in hierarchy.");
                }
            }
            else
            {
                Debug.Log("[InspectManager Debug] Raycast missed all 3D colliders.");
            }
        }
    }

    private void EnsureHorizontalLayout()
    {
        HorizontalLayoutGroup layout = inventoryHotbar.GetComponent<HorizontalLayoutGroup>();
        if (layout == null) layout = inventoryHotbar.gameObject.AddComponent<HorizontalLayoutGroup>();

        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        layout.spacing = 15f;
    }

    public void StartInspect(InspectableObject target)
    {
        if (isInspecting || target == null || !target.enabled || isTimelinePlaying)
        {
            Debug.LogWarning($"[InspectManager Debug] Cannot StartInspect. isInspecting={isInspecting}, targetIsNull={target == null}, targetDisabled={(target != null && !target.enabled)}, isTimelinePlaying={isTimelinePlaying}");
            return;
        }

        Debug.Log($"[InspectManager Debug] Starting inspect mode for target: {target.name}");

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        SetCurrentTarget(target);
        isInspecting = true;

        PauseGameManagerControls();

        Camera mainCam = Camera.main;
        if (mainCam == null)
        {
            Debug.LogError("[InspectManager Debug] MainCamera not found! Aborting inspect.");
            isInspecting = false;
            RestoreGameManagerControls();
            return;
        }

        if (fadeGroup != null)
        {
            fadeGroup.alpha = 0.65f;
            fadeGroup.blocksRaycasts = false;
        }

        if (inventoryHotbar != null && !isTimelinePlaying)
        {
            SetHotbarVisible(true, true);
        }

        SetupMainObjectSlot(currentTarget);
        CreateInspectCameraURP(mainCam);

        Vector3 targetPosition = inspectCamera.transform.position + (inspectCamera.transform.forward * inspectDistance);
        targetPosition += inspectCamera.transform.right * positionOffset.x;
        targetPosition += inspectCamera.transform.up * positionOffset.y;
        targetPosition += inspectCamera.transform.forward * positionOffset.z;

        Quaternion targetRotation = Quaternion.LookRotation(inspectCamera.transform.forward, inspectCamera.transform.up);

        clonedObject = Instantiate(target.gameObject, targetPosition, targetRotation);
        clonedObject.name = $"[INSPECT_CLONE] {target.gameObject.name}";

        originalClonedScale = clonedObject.transform.localScale;
        currentZoomMultiplier = 1f;

        int layerIndex = MaskToLayer(inspectLayer);
        SetLayerRecursively(clonedObject, layerIndex);

        Debug.Log($"[InspectManager Debug] Cloned object [{clonedObject.name}] created on Layer: {LayerMask.LayerToName(layerIndex)}");
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

            activeDimSlotImages[target] = dimImg;
            activeColorSlotImages[target] = colorImg;

            Debug.Log($"[InspectManager Debug] Dynamic UI slot created: Slot_{target.mainInfoID}");
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
        Debug.Log($"[InspectManager Debug] Current target switched to: {target.mainInfoID}");
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

        int totalSub = (target.subObjectInfos != null) ? target.subObjectInfos.Count : 0;
        int foundSub = target.GetFoundCount();
        int verifiedSub = GetVerifiedCount(target);

        float foundRatio = (totalSub > 0) ? (float)foundSub / totalSub : 1f;
        dimImage.fillAmount = foundRatio;

        float verifiedRatio = (totalSub > 0) ? (float)verifiedSub / totalSub : 1f;
        colorImage.fillAmount = verifiedRatio;

        dimImage.enabled = true;
        colorImage.enabled = true;
        dimImage.gameObject.SetActive(true);
        colorImage.gameObject.SetActive(true);

        Debug.Log($"[InspectManager Debug] Update [{target.mainInfoID}] UI -> 3D Found: {foundSub}/{totalSub} ({foundRatio:P0}), Verified: {verifiedSub}/{totalSub} ({verifiedRatio:P0})");

        if (verifiedRatio >= 1f && totalSub > 0)
        {
            if (!target.isMainEvidenceUnlocked)
            {
                target.isMainEvidenceUnlocked = true;
                Debug.Log($"[Evidence Unlocked] Object {target.mainInfoID} fully verified and unlocked!");

                if (EvidenceManager.Instance != null)
                {
                    EvidenceManager.Instance.CollectEvidence(target.mainInfoID);
                }
            }
        }
    }

    private int GetVerifiedCount(InspectableObject target)
    {
        if (target == null || target.subObjectInfos == null) return 0;

        int count = 0;
        foreach (var sub in target.subObjectInfos)
        {
            if (sub != null && sub.isVerified)
            {
                count++;
            }
        }
        return count;
    }

    public void StopInspect()
    {
        if (!isInspecting) return;
        isInspecting = false;

        Debug.Log("[InspectManager Debug] Exiting 3D inspect mode.");

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
            mainCameraData.cameraStack.Add(inspectCamera);
        }

        mainCam.cullingMask &= ~inspectLayer.value;
        Debug.Log("[InspectManager Debug] InspectCamera URP Overlay setup completed.");
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
            Debug.Log("[InspectManager Debug] ESC or Right-click detected, requesting exit from inspect mode.");
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
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                PointerEventData pointerData = new PointerEventData(EventSystem.current)
                {
                    position = Input.mousePosition
                };
                List<RaycastResult> results = new List<RaycastResult>();
                EventSystem.current.RaycastAll(pointerData, results);

                foreach (var r in results)
                {
                    if (fadeGroup != null && (r.gameObject == fadeGroup.gameObject || r.gameObject.transform.IsChildOf(fadeGroup.transform)))
                    {
                        continue;
                    }

                    Debug.Log($"[InspectManager Debug] Clicked on UI [{r.gameObject.name}] during inspect mode, ignoring sub-evidence raycast.");
                    return;
                }
            }

            Ray ray = inspectCamera.ScreenPointToRay(Input.mousePosition);
            Debug.Log($"[InspectManager Debug] Inspect mode Raycast cast at pos: {Input.mousePosition}");

            if (Physics.Raycast(ray, out RaycastHit hit, 100f, inspectLayer))
            {
                Debug.Log($"[InspectManager Debug] Inspect mode Raycast hit cloned object: [{hit.collider.gameObject.name}]");
                GameObject hitGo = hit.collider.gameObject;

                InspectableObject clonedInfo = clonedObject.GetComponent<InspectableObject>();
                if (clonedInfo == null) clonedInfo = clonedObject.GetComponentInChildren<InspectableObject>();

                if (clonedInfo != null && currentTarget != null)
                {
                    if (clonedInfo.TryGetSubObjectInfo(hitGo, out SubObjectInfo foundSubInfo, out int index))
                    {
                        if (!foundSubInfo.isFound)
                        {
                            Debug.Log($"[InspectManager Debug] Sub-evidence found: (Index: {index})!");
                            clonedInfo.MarkAsFound(index);
                            currentTarget.MarkAsFound(index);

                            UpdateSlotFillProgress(currentTarget);
                        }
                        else
                        {
                            Debug.Log($"[InspectManager Debug] Sub-evidence was already found previously.");
                        }
                    }
                    else
                    {
                        Debug.Log($"[InspectManager Debug] Hit object [{hitGo.name}] is not registered in sub-evidence list.");
                    }
                }
            }
            else
            {
                Debug.Log("[InspectManager Debug] Inspect mode Raycast missed all Inspect Layer objects.");
            }
        }
    }

    private void SetLayerRecursively(GameObject obj, int newLayer)
    {
        if (obj == null) return;
        obj.layer = newLayer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, newLayer);
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
    }

    private void PauseGameManagerControls()
    {
        previousGameManagerScriptStates.Clear();
        GameManager gm = FindObjectOfType<GameManager>();
        if (gm == null)
        {
            Debug.LogWarning("[InspectManager Debug] GameManager object not found in scene.");
            return;
        }

        Debug.Log("[InspectManager Debug] Pausing GameManager control scripts and Cinemachine cameras.");

        MonoBehaviour[] cinemachineCameras = new MonoBehaviour[]
        {
            gm.firstPersonCamera,
            gm.thirdPersonCamera,
            gm.timelineCamera
        };

        foreach (var cam in cinemachineCameras)
        {
            if (cam != null && !previousGameManagerScriptStates.ContainsKey(cam))
            {
                previousGameManagerScriptStates[cam] = cam.enabled;
                cam.enabled = false;
            }
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

        MonoBehaviour[] cinemachineCameras = new MonoBehaviour[]
        {
            gm.firstPersonCamera,
            gm.thirdPersonCamera,
            gm.timelineCamera
        };

        foreach (var cam in cinemachineCameras)
        {
            if (cam != null && cam.enabled)
            {
                cam.enabled = false;
            }
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
        Debug.Log("[InspectManager Debug] Restoring GameManager control scripts and cameras to original states.");
        foreach (var kvp in previousGameManagerScriptStates)
        {
            if (kvp.Key != null)
            {
                kvp.Key.enabled = kvp.Value;
            }
        }
        previousGameManagerScriptStates.Clear();
    }
}