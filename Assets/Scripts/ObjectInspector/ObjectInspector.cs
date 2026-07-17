using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering.Universal;

public class ObjectInspector : MonoBehaviour
{
    public static ObjectInspector Instance { get; private set; }

    [Header("UI References")]
    public CanvasGroup fadeGroup;
    public RectTransform inventoryHotbar;
    public Image[] slotImages;

    [Header("Rotation and Zoom Settings")]
    public float rotateSpeed = 5f;
    public float zoomSpeed = 2f;
    public float minScale = 0.5f;
    public float maxScale = 2.5f;

    [Header("Inspect Position (Isolated)")]
    public Vector3 isolatedSpawnCenter = new Vector3(0f, -1000f, 0f);
    public float inspectDistance = 1.5f;
    public Vector3 positionOffset = Vector3.zero;

    [Header("Inspect Layer and Camera")]
    public LayerMask inspectLayer;

    private GameObject clonedObject;
    private Camera inspectCamera;
    private bool isInspecting = false;
    private Vector3 originalClonedScale;
    private float currentZoomMultiplier = 1f;
    private int currentSlotIndex = 0;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        if (inventoryHotbar != null) inventoryHotbar.gameObject.SetActive(false);
    }

    void Update()
    {
        if (!isInspecting) return;
        HandleInput();
    }

    public void StartInspect(GameObject targetPrefab)
    {
        if (isInspecting || targetPrefab == null) return;
        isInspecting = true;

        Camera mainCam = Camera.main;
        if (mainCam == null)
        {
            Debug.LogError("MainCamera not found!");
            return;
        }

        if (fadeGroup != null)
        {
            fadeGroup.alpha = 0.65f;
            fadeGroup.blocksRaycasts = true;
        }

        if (inventoryHotbar != null) inventoryHotbar.gameObject.SetActive(true);

        CreateInspectCameraURP(mainCam);

        Vector3 targetPosition = inspectCamera.transform.position + (inspectCamera.transform.forward * inspectDistance);
        targetPosition += inspectCamera.transform.right * positionOffset.x;
        targetPosition += inspectCamera.transform.up * positionOffset.y;
        targetPosition += inspectCamera.transform.forward * positionOffset.z;

        Quaternion targetRotation = Quaternion.LookRotation(inspectCamera.transform.forward, inspectCamera.transform.up);

        clonedObject = Instantiate(targetPrefab, targetPosition, targetRotation);
        clonedObject.transform.SetParent(this.transform);

        originalClonedScale = clonedObject.transform.localScale;
        currentZoomMultiplier = 1f;

        int layerIndex = MaskToLayer(inspectLayer);
        SetLayerRecursively(clonedObject, layerIndex);
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
            Ray ray = inspectCamera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 100f, inspectLayer))
            {
                InspectableInfo mainInfo = clonedObject.GetComponent<InspectableInfo>();
                if (mainInfo == null)
                {
                    mainInfo = clonedObject.GetComponentInChildren<InspectableInfo>();
                }

                if (mainInfo != null)
                {
                    if (mainInfo.TryGetSubObjectInfo(hit.collider.gameObject, out InspectableInfo.SubObjectInfo foundSubInfo, out int index))
                    {
                        if (!foundSubInfo.isFound)
                        {
                            mainInfo.MarkAsFound(index);
                            OnInfoFound(foundSubInfo);
                        }
                    }
                }
            }
        }
    }

    private void OnInfoFound(InspectableInfo.SubObjectInfo subInfo)
    {
        if (EvidenceManager.Instance != null)
        {
            EvidenceManager.Instance.CollectEvidence(subInfo.infoID);
        }

        if (slotImages != null && currentSlotIndex < slotImages.Length)
        {
            if (subInfo.atlasTexture != null && slotImages[currentSlotIndex] != null)
            {
                Sprite croppedSprite = Sprite.Create(subInfo.atlasTexture, subInfo.uvRect, new Vector2(0.5f, 0.5f));
                slotImages[currentSlotIndex].sprite = croppedSprite;
                slotImages[currentSlotIndex].enabled = true;
                currentSlotIndex++;
            }
            else
            {
                currentSlotIndex++;
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
}