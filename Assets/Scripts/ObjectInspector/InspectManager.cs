using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 全域單例管理器，掛載在場景中的空物件上，管理檢視相機、Clone 生成與 UI
/// </summary>
public class InspectManager : MonoBehaviour
{
    public static InspectManager Instance { get; private set; }

    [Header("UI 引用")]
    public CanvasGroup fadeGroup;
    public RectTransform inventoryHotbar; // 底部線索欄容器

    [Header("動態 UI 格子設定")]
    public Vector2 slotSize = new Vector2(80f, 80f);

    [Header("旋轉與縮放設定")]
    public float rotateSpeed = 5f;
    public float zoomSpeed = 2f;
    public float minScale = 0.5f;
    public float maxScale = 2.5f;

    [Header("檢視位置與 Layer")]
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
    private Dictionary<InspectableObject, Image> activeSlotImages = new Dictionary<InspectableObject, Image>();

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
            inventoryHotbar.gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        if (!isInspecting) return;
        HandleInput();
    }

    private void EnsureHorizontalLayout()
    {
        HorizontalLayoutGroup layout = inventoryHotbar.GetComponent<HorizontalLayoutGroup>();
        if (layout == null) layout = inventoryHotbar.gameObject.AddComponent<HorizontalLayoutGroup>();

        layout.childAlignment = TextAnchor.MiddleCenter; // 垂直與水平皆完全置中
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        layout.spacing = 15f;
    }

    public void StartInspect(InspectableObject target)
    {
        if (isInspecting || target == null) return;

        currentTarget = target;
        isInspecting = true;

        Camera mainCam = Camera.main;
        if (mainCam == null)
        {
            Debug.LogError("[InspectManager] 找不到 Tag 為 MainCamera 的相機！");
            isInspecting = false;
            return;
        }

        if (fadeGroup != null)
        {
            fadeGroup.alpha = 0.65f;
            fadeGroup.blocksRaycasts = true;
        }

        if (inventoryHotbar != null) inventoryHotbar.gameObject.SetActive(true);

        // 1. 初始化/動態生成 UI 格子
        SetupMainObjectSlot(currentTarget);

        // 2. 建立 Overlay 相機
        CreateInspectCameraURP(mainCam);

        // 3. 計算生成位置
        Vector3 targetPosition = inspectCamera.transform.position + (inspectCamera.transform.forward * inspectDistance);
        targetPosition += inspectCamera.transform.right * positionOffset.x;
        targetPosition += inspectCamera.transform.up * positionOffset.y;
        targetPosition += inspectCamera.transform.forward * positionOffset.z;

        Quaternion targetRotation = Quaternion.LookRotation(inspectCamera.transform.forward, inspectCamera.transform.up);

        // 4. 生成 Clone 物件
        clonedObject = Instantiate(target.gameObject, targetPosition, targetRotation);
        clonedObject.name = $"[INSPECT_CLONE] {target.gameObject.name}";

        originalClonedScale = clonedObject.transform.localScale;
        currentZoomMultiplier = 1f;

        // 設置 Layer
        int layerIndex = MaskToLayer(inspectLayer);
        SetLayerRecursively(clonedObject, layerIndex);
    }

    private void SetupMainObjectSlot(InspectableObject target)
    {
        if (inventoryHotbar == null) return;

        if (!activeSlotImages.ContainsKey(target))
        {
            // --- 1. 外層：Slot 容器 (負責在 Hotbar 中定位與排列) ---
            GameObject slotGo = new GameObject($"Slot_{target.mainInfoID}", typeof(RectTransform));
            slotGo.transform.SetParent(inventoryHotbar, false);

            RectTransform slotRect = slotGo.GetComponent<RectTransform>();
            slotRect.sizeDelta = slotSize;
            slotRect.anchorMin = new Vector2(0.5f, 0.5f);
            slotRect.anchorMax = new Vector2(0.5f, 0.5f);
            slotRect.pivot = new Vector2(0.5f, 0.5f);

            // --- 2. 內層：Icon 圖示 (負責顯示圖片、比例縮放與 Fill 進度條) ---
            GameObject iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(slotGo.transform, false);

            RectTransform iconRect = iconGo.GetComponent<RectTransform>();
            iconRect.anchorMin = Vector2.zero;
            iconRect.anchorMax = Vector2.one;
            iconRect.sizeDelta = Vector2.zero; // 完全對齊父容器中心
            iconRect.pivot = new Vector2(0.5f, 0.5f);

            Image slotImg = iconGo.GetComponent<Image>();
            activeSlotImages[target] = slotImg;
        }

        Image slotImage = activeSlotImages[target];

        if (slotImage != null && target.mainSprite != null)
        {
            // 生成黑白縮圖
            Sprite bwSprite = CreateGrayscaleSprite(target.mainSprite, target.useCustomCrop, target.cropRect);
            Sprite finalSprite = (bwSprite != null) ? bwSprite : target.mainSprite;

            slotImage.sprite = finalSprite;

            // 在內層 Icon 上掛載 AspectRatioFitter，使其相對於 Slot 容器保持正確比例並置中
            AspectRatioFitter fitter = slotImage.GetComponent<AspectRatioFitter>();
            if (fitter == null)
            {
                fitter = slotImage.gameObject.AddComponent<AspectRatioFitter>();
            }
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;

            if (finalSprite != null && finalSprite.rect.height > 0)
            {
                fitter.aspectRatio = finalSprite.rect.width / finalSprite.rect.height;
            }

            // 設定 UI Filled 進度條屬性
            slotImage.type = Image.Type.Filled;
            slotImage.fillMethod = Image.FillMethod.Vertical;
            slotImage.fillOrigin = (int)Image.OriginVertical.Top;

            UpdateSlotFillProgress(target);
            slotImage.enabled = true;
        }
    }

    private void UpdateSlotFillProgress(InspectableObject target)
    {
        if (!activeSlotImages.ContainsKey(target)) return;

        Image slotImage = activeSlotImages[target];
        int totalSub = target.subObjectInfos.Count;
        int foundSub = target.GetFoundCount();

        float fillRatio = (totalSub > 0) ? (float)foundSub / totalSub : 1f;
        slotImage.fillAmount = fillRatio;

        if (foundSub >= totalSub && totalSub > 0 && !target.isMainEvidenceUnlocked)
        {
            target.isMainEvidenceUnlocked = true;
            Debug.Log($"<color=green>[證據解鎖] 找齊所有線索，獲得主物件證據：{target.mainInfoID}！</color>");

            if (EvidenceManager.Instance != null)
            {
                EvidenceManager.Instance.CollectEvidence(target.mainInfoID);
            }
        }
    }

    public void StopInspect()
    {
        if (!isInspecting) return;
        isInspecting = false;

        if (fadeGroup != null)
        {
            fadeGroup.alpha = 0f;
            fadeGroup.blocksRaycasts = false;
        }

        if (inventoryHotbar != null) inventoryHotbar.gameObject.SetActive(false);
        if (clonedObject != null) Destroy(clonedObject);

        RemoveInspectCameraURP();
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

        // 旋轉
        if (Input.GetMouseButton(0))
        {
            float rotX = Input.GetAxis("Mouse X") * rotateSpeed;
            float rotY = Input.GetAxis("Mouse Y") * rotateSpeed;

            clonedObject.transform.Rotate(inspectCamera.transform.up, -rotX, Space.World);
            clonedObject.transform.Rotate(inspectCamera.transform.right, rotY, Space.World);
        }

        // 縮放
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.01f)
        {
            currentZoomMultiplier += scroll * zoomSpeed;
            currentZoomMultiplier = Mathf.Clamp(currentZoomMultiplier, minScale, maxScale);
            clonedObject.transform.localScale = originalClonedScale * currentZoomMultiplier;
        }

        // 點擊線索
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = inspectCamera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 100f, inspectLayer))
            {
                InspectableObject clonedInfo = clonedObject.GetComponent<InspectableObject>();
                if (clonedInfo == null) clonedInfo = clonedObject.GetComponentInChildren<InspectableObject>();

                if (clonedInfo != null && currentTarget != null)
                {
                    if (clonedInfo.TryGetSubObjectInfo(hit.collider.gameObject, out SubObjectInfo foundSubInfo, out int index))
                    {
                        if (!foundSubInfo.isFound)
                        {
                            clonedInfo.MarkAsFound(index);
                            currentTarget.MarkAsFound(index);

                            UpdateSlotFillProgress(currentTarget);
                        }
                    }
                }
            }
        }
    }

    private Sprite CreateGrayscaleSprite(Sprite sourceSprite, bool useCustomCrop, Rect customRect)
    {
        if (sourceSprite == null) return null;

        try
        {
            Texture2D sourceTex = sourceSprite.texture;
            Rect sourceRect = sourceSprite.rect;

            Rect finalRect = useCustomCrop ? customRect : sourceRect;

            int x = Mathf.Clamp((int)finalRect.x, 0, sourceTex.width);
            int y = Mathf.Clamp((int)finalRect.y, 0, sourceTex.height);
            int width = Mathf.Clamp((int)finalRect.width, 1, sourceTex.width - x);
            int height = Mathf.Clamp((int)finalRect.height, 1, sourceTex.height - y);

            Texture2D bwTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            Color[] pixels = sourceTex.GetPixels(x, y, width, height);

            for (int i = 0; i < pixels.Length; i++)
            {
                float gray = pixels[i].r * 0.299f + pixels[i].g * 0.587f + pixels[i].b * 0.114f;
                pixels[i] = new Color(gray, gray, gray, pixels[i].a);
            }

            bwTexture.SetPixels(pixels);
            bwTexture.Apply();

            return Sprite.Create(bwTexture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f));
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[InspectManager] 圖片轉灰階失敗！請點擊 Sprite 資源並在 Inspector 勾選 'Read/Write Enabled'。錯誤細節：{e.Message}");
            return sourceSprite;
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
}