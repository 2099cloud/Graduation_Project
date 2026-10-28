using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Ink.Runtime;
using TMPro;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using System.Linq;

public class GameManager : MonoBehaviour
{
    [Header("Ink Configuration")]
    public TextAsset inkJsonAsset;
    [Tooltip("Starting knot name when game begins.")]
    public string startingKnot = "";
    private Story story;

    [Header("UI References")]
    public TextMeshProUGUI dialogueText;
    public GameObject choiceButtonPrefab;
    public Transform choiceHolder;
    [Tooltip("TextMeshProUGUI used to display current scores in real time")]
    public TextMeshProUGUI scoreDisplayTMP;

    [Header("Scoring Weights (Adjustable)")]
    [Range(0f, 1f)] public float searchWeight = 0.2f;
    [Range(0f, 1f)] public float verifyWeight = 0.5f;
    [Range(0f, 1f)] public float dialogueWeight = 0.3f;

    [Header("Testing & Debug Scoring Override")]
    [Tooltip("勾選後將略過自動計算，直接使用下方指定的分數來進行遊戲測試與劇情走向判定")]
    public bool enableScoreOverride = false;
    [Range(0f, 100f)]
    [Tooltip("手動指定的總得分（可用於測試不同的劇情結局）")]
    public float overrideTotalScore = 0f;

    [Header("Timeline Configuration")]
    public PlayableDirector globalDirector;

    [Header("Timeline Player Control")]
    [Tooltip("勾選後，播放 Timeline 時會暫時關閉 CharacterController（預設不關閉）")]
    public bool disableCharacterControllerDuringTimeline = false;
    [Tooltip("勾選後，播放 Timeline 時會暫時關閉 PlayerInput / ThirdPersonController / StarterAssetsInputs（預設不關閉）")]
    public bool disablePlayerInputDuringTimeline = false;

    [Header("Cinemachine Camera Configuration")]
    public MonoBehaviour cinemachineCamera;

    public MonoBehaviour firstPersonCamera => cinemachineCamera;
    public MonoBehaviour thirdPersonCamera => cinemachineCamera;
    public MonoBehaviour timelineCamera => cinemachineCamera;

    [Header("Player Input Control")]
    [Tooltip("Please drag player input component here")]
    public Behaviour playerInputScript;

    [Header("Data and Transition Configuration")]
    public string csvResourcePath = "SceneConfig";
    public CanvasGroup fadeGroup;
    public float fadeDuration = 0.5f;
    public float bufferTime = 0.5f;

    [Header("Dynamic Spawn Holder")]
    public Transform environmentHolder;

    [Header("Camera and Controls Configuration")]
    public MonoBehaviour[] cameraControlScripts;

    private bool isWaitingForChoice = false;
    private bool isInputBlocked = false;
    private Dictionary<string, InteractableObject> allSceneObjects = new Dictionary<string, InteractableObject>();
    private int currentSceneID = 0;

    private string currentTargetObjectName = "";
    private bool isTransitioning = false;
    private List<string> deferredTags = new List<string>();

    private bool isWaitingForTimelineClick = false;
    private bool isTimelineCutsceneActive = false;
    private bool isSwitchingTimeline = false;
    private bool hasReachedFirstSignal = false; // 追蹤是否已碰到第一個 Signal
    private int signalHitCount = 0; // 已經「通過」的 Signal 數量（0 = 還沒碰到任何 Signal）
    private int timelineSession = 0; // 每次 Timeline 被切換 / 被 Tag 跳轉就 +1，用來偵測 ContinueStory 期間 Timeline 是否被接管
    private int lastSignalJumpFrame = -100; // 最近一次「程式跳轉到 Signal」的 frame，用來忽略補觸發的 Signal
    private const double SignalTimeTolerance = 0.02; // Signal 時間比對容許誤差（秒）

    // Scoring runtime tracking variables
    private int dialogueScoreAccumulated = 0;
    private int dialogueGoodCount = 0;
    private int dialogueNormalCount = 0;
    private int dialogueBadCount = 0;
    private int dialogueTotalChoices = 0; // 新增：已選選項總數

    private int verifyFailCount = 0;
    private int verifySuccessCount = 0;

    // 追蹤被開啟檢視過的物件 ID (第一關用)
    private HashSet<string> inspectedObjectIDs = new HashSet<string>();

    private List<CharacterController> disabledControllers = new List<CharacterController>();
    private List<MonoBehaviour> disabledCinemachineCameras = new List<MonoBehaviour>();
    private List<MonoBehaviour> disabledPlayerControlScripts = new List<MonoBehaviour>();

    // 暫存 Cinemachine 攝影機調整前的速度數值，用於恢復
    private float originalXSpeed = -1f;
    private float originalYSpeed = -1f;

    /// <summary>其他腳本（例如 InteractiveDoor）可用來判斷目前是否正在播放過場</summary>
    public bool IsTimelineCutsceneActive => isTimelineCutsceneActive;

    private void OnEnable()
    {
        if (globalDirector != null)
        {
            globalDirector.stopped += OnTimelineStopped;
        }
    }

    private void OnDisable()
    {
        if (globalDirector != null)
        {
            globalDirector.stopped -= OnTimelineStopped;
        }

        if (story != null)
        {
            story.UnbindExternalFunction("IsVerified");
            story.UnbindExternalFunction("GetTotalScore");
        }
    }

    private void OnTimelineStopped(PlayableDirector director)
    {
        if (isSwitchingTimeline) return;

        string timelineName = (director != null && director.playableAsset != null) ? director.playableAsset.name : "Unknown";
        Debug.Log("[Timeline Debug] OnTimelineStopped triggered for: " + timelineName);

        // 【核心修正】強制重置所有過場與等待狀態
        isTimelineCutsceneActive = false;
        isWaitingForTimelineClick = false;
        hasReachedFirstSignal = false;
        signalHitCount = 0;

        SetTimelineSystemOverride(false);

        ProceduralBreathing[] breathings = FindObjectsOfType<ProceduralBreathing>(true);
        foreach (var b in breathings)
        {
            if (b != null) b.SetBreathingActive(false);
        }

        // 【修正】Timeline 結束時若只剩選項、且畫面上有一句話：保留該句，等玩家點擊後再顯示選項
        bool waitForChoiceClick = story != null && !story.canContinue && story.currentChoices.Count > 0
                                  && dialogueText != null && !string.IsNullOrWhiteSpace(dialogueText.text);

        if (dialogueText != null && !waitForChoiceClick)
        {
            dialogueText.text = "";
        }

        if (story != null)
        {
            Debug.Log($"<color=cyan>[Timeline Debug] 動畫 [{timelineName}] 播放完畢，檢查 Ink 是否能繼續</color>");
            if (story.canContinue)
            {
                ContinueStory();
            }
            else if (story.currentChoices.Count > 0)
            {
                if (!waitForChoiceClick)
                {
                    StartCoroutine(ToggleChoiceUI(true));
                }
                // waitForChoiceClick == true：交給自由模式的點擊（Update）顯示選項
            }
            else
            {
                CheckAndEndCutscene();
            }
        }
    }

    void Awake()
    {
        if (EventSystem.current == null)
        {
            Debug.LogError("[GameManager Debug] No EventSystem found in scene! UI and EventTrigger will NOT work!");
        }

        Camera mainCam = Camera.main;
        if (mainCam != null && mainCam.GetComponent<PhysicsRaycaster>() == null)
        {
            Debug.LogWarning("[GameManager Debug] MainCamera is missing PhysicsRaycaster component! EventTrigger on 3D objects will not fire!");
        }

        RefreshSceneObjectsDictionary();

        if (cameraControlScripts == null || cameraControlScripts.Length == 0)
        {
            if (mainCam != null)
            {
                List<MonoBehaviour> foundScripts = new List<MonoBehaviour>();
                foreach (var script in mainCam.GetComponents<MonoBehaviour>())
                {
                    if (script != null)
                    {
                        string scriptName = script.GetType().Name;
                        string scriptNamespace = script.GetType().Namespace ?? "";

                        if (scriptNamespace != "UnityEngine" &&
                            scriptName != "CinemachineBrain" &&
                            !scriptName.Contains("Cinemachine"))
                        {
                            foundScripts.Add(script);
                        }
                    }
                }
                cameraControlScripts = foundScripts.ToArray();
            }
        }
    }

    void Start()
    {
        if (!inkJsonAsset) return;
        story = new Story(inkJsonAsset.text);

        story.BindExternalFunction("IsVerified", (string objectID) => {
            bool verified = IsObjectFullyVerified(objectID);
            if (isTimelineCutsceneActive)
            {
                StartCoroutine(CheckAndShowChoicesRoutine());
            }
            return verified;
        });

        // 綁定外部函數讓 Ink 劇本可以隨時取得總得分
        story.BindExternalFunction("GetTotalScore", () => {
            return GetFinalTotalScore();
        });

        story.ObserveVariable("scene", (varName, newValue) => {
            StartCoroutine(SceneTransition((int)newValue));
        });

        if (!string.IsNullOrEmpty(startingKnot))
        {
            story.ChoosePathString(startingKnot);
        }

        ContinueStory();
    }

    private IEnumerator CheckAndShowChoicesRoutine()
    {
        yield return null;
        if (story != null && !story.canContinue && story.currentChoices.Count > 0)
        {
            bool hasLineOnScreen = (dialogueText != null && !string.IsNullOrWhiteSpace(dialogueText.text));
            if (!hasLineOnScreen && !isWaitingForChoice && (choiceHolder == null || choiceHolder.childCount == 0))
            {
                StartCoroutine(ToggleChoiceUI(true));
            }
        }
    }

    /// <summary>
    /// 檢查指定的物件（或證據）是否已經完全驗證成功（所有 subObjectInfos 皆為 isVerified）
    /// </summary>
    public bool IsObjectFullyVerified(string mainInfoID)
    {
        InspectableObject[] allInspectables = FindObjectsOfType<InspectableObject>(true);
        foreach (var obj in allInspectables)
        {
            if (obj != null && (obj.mainInfoID == mainInfoID || obj.gameObject.name == mainInfoID))
            {
                if (obj.subObjectInfos != null && obj.subObjectInfos.Count > 0)
                {
                    foreach (var sub in obj.subObjectInfos)
                    {
                        if (sub != null && !sub.isVerified) return false;
                    }
                    return true;
                }
            }
        }
        return false;
    }

    void Update()
    {
        CheckAndHandleCameraSpeedForUI();

        if (isInputBlocked) return;

        UpdateScoreDisplayUI();

        bool shouldDisableCamera = false;

        if (fadeGroup != null)
        {
            shouldDisableCamera = fadeGroup.alpha > 0.01f;
        }

        if (isTimelineCutsceneActive)
        {
            shouldDisableCamera = true;
        }

        SetCameraControlsActive(!shouldDisableCamera);

        if (TaskSystemManager.Instance != null && TaskSystemManager.Instance.IsTaskPopupOpen)
        {
            return;
        }

        if (isWaitingForChoice || isTransitioning) return;

        // 【防呆保護】如果當前正在播放 Timeline 過場，絕對不允許透過空白鍵或隨意點擊去呼叫 ContinueStory 亂跳！
        if (isTimelineCutsceneActive)
        {
            if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space))
            {
                if (!hasReachedFirstSignal)
                {
                    return;
                }
                AdvanceTimelineAndDialogue();
            }
            return; // 只要還在過場中，直接攔截後續的點擊，防止觸發自由模式的點擊邏輯！
        }

        if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space))
        {
            if (Input.GetMouseButtonDown(0))
            {
                if (IsPointerOverUIElement())
                {
                    return;
                }

                if (DetectObjectClick()) return;
            }

            if (story != null && story.canContinue)
            {
                // 【修正】自由模式不需要處理 Timeline 推進。
                // 若這句 Ink 剛好帶了 timeline Tag，原本的 CheckAndAdvance 會誤對「剛啟動的新 Timeline」做跳轉。
                ContinueStory();
            }
            else if (story != null && story.currentChoices.Count > 0)
            {
                StartCoroutine(ToggleChoiceUI(true));
            }
        }
    }

    /// <summary>
    /// 檢查虛擬瀏覽器或 InspectManager 是否開啟，動態將 Cinemachine 攝影機軸向速度歸零及控制 Player Input，關閉後恢復
    /// </summary>
    private void CheckAndHandleCameraSpeedForUI()
    {
        bool isAnyUIOpen = false;

        // 1. 檢查虛擬瀏覽器是否開啟
        if (VirtualBrowserController.Instance != null)
        {
            if (VirtualBrowserController.Instance.backgroundPanel != null && VirtualBrowserController.Instance.backgroundPanel.activeInHierarchy)
            {
                isAnyUIOpen = true;
            }
        }

        // 2. 檢查 InspectManager 是否開啟
        InspectManager inspectInst = InspectManager.Instance != null ? InspectManager.Instance : FindObjectOfType<InspectManager>(true);
        if (inspectInst != null)
        {
            if (inspectInst.closeButton != null && inspectInst.closeButton.activeInHierarchy)
            {
                isAnyUIOpen = true;
            }
        }

        // 【防呆自動尋找】如果 cinemachineCamera 為空，自動在場景中尋找 FreeLook 或對應攝影機元件
        if (cinemachineCamera == null)
        {
            var freeLookComp = FindObjectsOfType<MonoBehaviour>(true)
                .FirstOrDefault(b => b != null && b.GetType().Name.Contains("FreeLook"));

            if (freeLookComp != null)
            {
                cinemachineCamera = freeLookComp;
            }
        }

        // 3. 根據 UI 是否開啟來同步控制 攝影機速度 與 Player Input 控制腳本
        if (isAnyUIOpen)
        {
            if (cinemachineCamera != null)
            {
                SetAxisSpeedZero(cinemachineCamera);
            }

            if (playerInputScript != null && playerInputScript.enabled)
            {
                playerInputScript.enabled = false;
            }
        }
        else
        {
            if (cinemachineCamera != null)
            {
                RestoreAxisSpeed(cinemachineCamera);
            }

            if (playerInputScript != null && !playerInputScript.enabled)
            {
                playerInputScript.enabled = true;
            }
        }
    }

    private void SetAxisSpeedZero(MonoBehaviour camComponent)
    {
        if (camComponent == null) return;
        var t = camComponent.GetType();
        //Debug.Log("[CameraSpeed Debug] 正在對攝影機元件進行反射歸零，類型: " + t.Name);

        string[] xAxisNames = { "m_XAxis", "xAxis", "XAxis" };
        string[] yAxisNames = { "m_YAxis", "yAxis", "YAxis" };

        bool foundX = false;
        bool foundY = false;

        foreach (var name in xAxisNames)
        {
            var field = t.GetField(name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null)
            {
                object xVal = field.GetValue(camComponent);
                if (xVal != null)
                {
                    var axisType = xVal.GetType();
                    var speedField = axisType.GetField("m_MaxSpeed", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                                  ?? axisType.GetField("MaxSpeed", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (speedField != null)
                    {
                        float currentSpeed = (float)speedField.GetValue(xVal);
                        //Debug.Log("[CameraSpeed Debug] 成功找到 X 軸，目前速度: " + currentSpeed);
                        if (currentSpeed > 0.001f && originalXSpeed < 0f) originalXSpeed = currentSpeed;
                        if (currentSpeed > 0.001f) speedField.SetValue(xVal, 0f);
                        foundX = true;
                    }
                }
                break;
            }
        }
        if (!foundX) Debug.LogWarning("[CameraSpeed Warning] 找不到 X 軸欄位！");

        foreach (var name in yAxisNames)
        {
            var field = t.GetField(name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null)
            {
                object yVal = field.GetValue(camComponent);
                if (yVal != null)
                {
                    var axisType = yVal.GetType();
                    var speedField = axisType.GetField("m_MaxSpeed", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                                  ?? axisType.GetField("MaxSpeed", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (speedField != null)
                    {
                        float currentSpeed = (float)speedField.GetValue(yVal);
                        //Debug.Log("[CameraSpeed Debug] 成功找到 Y 軸，目前速度: " + currentSpeed);
                        if (currentSpeed > 0.001f && originalYSpeed < 0f) originalYSpeed = currentSpeed;
                        if (currentSpeed > 0.001f) speedField.SetValue(yVal, 0f);
                        foundY = true;
                    }
                }
                break;
            }
        }
        if (!foundY) Debug.LogWarning("[CameraSpeed Warning] 找不到 Y 軸欄位！");
    }

    private void RestoreAxisSpeed(MonoBehaviour camComponent)
    {
        if (camComponent == null) return;
        var t = camComponent.GetType();

        string[] xAxisNames = { "m_XAxis", "xAxis", "XAxis" };
        string[] yAxisNames = { "m_YAxis", "yAxis", "YAxis" };

        if (originalXSpeed > 0f)
        {
            foreach (var name in xAxisNames)
            {
                var field = t.GetField(name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field != null)
                {
                    object xVal = field.GetValue(camComponent);
                    if (xVal != null)
                    {
                        var axisType = xVal.GetType();
                        var speedField = axisType.GetField("m_MaxSpeed", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                                      ?? axisType.GetField("MaxSpeed", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        if (speedField != null)
                        {
                            speedField.SetValue(xVal, originalXSpeed);
                            Debug.Log("[CameraSpeed Debug] 恢復 X 軸速度為: " + originalXSpeed);
                        }
                    }
                    break;
                }
            }
            originalXSpeed = -1f;
        }

        if (originalYSpeed > 0f)
        {
            foreach (var name in yAxisNames)
            {
                var field = t.GetField(name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field != null)
                {
                    object yVal = field.GetValue(camComponent);
                    if (yVal != null)
                    {
                        var axisType = yVal.GetType();
                        var speedField = axisType.GetField("m_MaxSpeed", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                                      ?? axisType.GetField("MaxSpeed", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        if (speedField != null)
                        {
                            speedField.SetValue(yVal, originalYSpeed);
                            Debug.Log("[CameraSpeed Debug] 恢復 Y 軸速度為: " + originalYSpeed);
                        }
                    }
                    break;
                }
            }
            originalYSpeed = -1f;
        }
    }

    public void SetPlayerInputActive(bool isActive)
    {
        if (playerInputScript != null)
        {
            playerInputScript.enabled = isActive;
        }
    }

    private bool IsPointerOverUIElement()
    {
        if (EventSystem.current == null) return false;

        PointerEventData pointerData = new PointerEventData(EventSystem.current)
        {
            position = Input.mousePosition
        };
        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerData, results);

        foreach (var result in results)
        {
            if (result.module is GraphicRaycaster)
            {
                if (fadeGroup != null && (result.gameObject == fadeGroup.gameObject || result.gameObject.transform.IsChildOf(fadeGroup.transform)))
                {
                    continue;
                }
                return true;
            }
        }
        return false;
    }

    private void SetCameraControlsActive(bool isActive)
    {
        if (cameraControlScripts == null) return;
        foreach (var script in cameraControlScripts)
        {
            if (script != null)
            {
                if (script.GetType().Name == "CinemachineBrain") continue;

                if (script.enabled != isActive)
                {
                    script.enabled = isActive;
                }
            }
        }
    }

    private GameObject FindGameObject(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;

        if (allSceneObjects.ContainsKey(name) && allSceneObjects[name] != null)
        {
            return allSceneObjects[name].gameObject;
        }

        if (environmentHolder != null)
        {
            Transform[] childTransforms = environmentHolder.GetComponentsInChildren<Transform>(true);
            foreach (var t in childTransforms)
            {
                if (t != null && t.name == name) return t.gameObject;
            }
        }

        Scene activeScene = SceneManager.GetActiveScene();
        GameObject[] rootObjects = activeScene.GetRootGameObjects();
        foreach (var root in rootObjects)
        {
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            foreach (var t in transforms)
            {
                if (t != null && t.name == name) return t.gameObject;
            }
        }

        return null;
    }

    private void SetTimelineSystemOverride(bool overrideActive)
    {
        if (TaskSystemManager.Instance != null)
        {
            TaskSystemManager.Instance.SetTaskVisibility(!overrideActive);
        }

        if (overrideActive)
        {
            disabledControllers.Clear();
            disabledPlayerControlScripts.Clear();

            // 【修改】預設播放 Timeline 時不再關閉 CharacterController 與 PlayerInput 相關腳本，
            // 需要時可在 Inspector 勾選 disableCharacterControllerDuringTimeline / disablePlayerInputDuringTimeline。
            if (disableCharacterControllerDuringTimeline)
            {
                CharacterController[] controllers = FindObjectsOfType<CharacterController>();
                foreach (var cc in controllers)
                {
                    if (cc != null && cc.enabled)
                    {
                        cc.enabled = false;
                        disabledControllers.Add(cc);
                    }
                }
            }

            if (disablePlayerInputDuringTimeline)
            {
                MonoBehaviour[] allScripts = FindObjectsOfType<MonoBehaviour>();
                foreach (var script in allScripts)
                {
                    if (script != null && script.enabled)
                    {
                        string typeName = script.GetType().Name;
                        if (typeName.Contains("ThirdPersonController") || typeName.Contains("PlayerInput") || typeName.Contains("StarterAssetsInputs"))
                        {
                            script.enabled = false;
                            disabledPlayerControlScripts.Add(script);
                        }
                    }
                }
            }
        }
        else
        {
            Physics.SyncTransforms();

            foreach (var cc in disabledControllers)
            {
                if (cc != null) cc.enabled = true;
            }
            disabledControllers.Clear();

            foreach (var cam in disabledCinemachineCameras)
            {
                if (cam != null) cam.enabled = true;
            }
            disabledCinemachineCameras.Clear();

            foreach (var script in disabledPlayerControlScripts)
            {
                if (script != null) script.enabled = true;
            }
            disabledPlayerControlScripts.Clear();
        }

        if (InspectManager.Instance != null)
        {
            InspectManager.Instance.SetTimelinePlaying(overrideActive);
        }
    }

    /// <summary>
    /// 取得目前 Timeline 上所有 SignalEmitter 的時間（由小到大排序）
    /// </summary>
    private List<double> GetSortedSignalTimes()
    {
        List<double> times = new List<double>();
        if (globalDirector == null) return times;

        TimelineAsset timelineAsset = globalDirector.playableAsset as TimelineAsset;
        if (timelineAsset == null) return times;

        List<SignalEmitter> allEmitters = new List<SignalEmitter>();

        if (timelineAsset.markerTrack != null)
        {
            foreach (var marker in timelineAsset.markerTrack.GetMarkers())
            {
                if (marker is SignalEmitter emitter) allEmitters.Add(emitter);
            }
        }

        foreach (var track in timelineAsset.GetOutputTracks())
        {
            foreach (var marker in track.GetMarkers())
            {
                if (marker is SignalEmitter emitter && !allEmitters.Contains(emitter))
                {
                    allEmitters.Add(emitter);
                }
            }
        }

        foreach (var e in allEmitters) times.Add(e.time);
        times.Sort();
        return times;
    }

    /// <summary>
    /// Timeline Signal 觸發時呼叫。
    /// 規則：第一個 Signal 不暫停；最後一個 Signal 不暫停（讓 Timeline 自然播完）；
    ///       中間的 Signal 暫停並等待玩家點擊。
    /// 這裡「不」呼叫 ContinueStory()，Ink 只由玩家點擊推進。
    /// </summary>
    public void OnTimelinePauseSignalReached()
    {
        List<double> times = GetSortedSignalTimes();
        int total = times.Count;

        // 不在過場模式、或找不到 Signal 資料：只做位置同步
        if (!isTimelineCutsceneActive || total == 0)
        {
            SyncCharacterTransform();
            return;
        }

        // 剛被程式跳轉過（Evaluate 可能補觸發被跳過的 Signal），忽略，避免重複暫停 / 重複計數
        if (Time.frameCount - lastSignalJumpFrame <= 2) return;

        // 所有 Signal 都處理過了
        if (signalHitCount >= total) return;

        // 依目前播放時間判斷「實際已經通過幾個 Signal」，避免計數與實際位置脫節
        double now = (globalDirector != null) ? globalDirector.time : 0.0;
        int idxByTime = -1;
        for (int i = 0; i < total; i++)
        {
            if (times[i] <= now + SignalTimeTolerance) idxByTime = i;
        }

        int prevCount = signalHitCount;
        signalHitCount = Mathf.Min(total, Mathf.Max(prevCount + 1, idxByTime + 1));
        hasReachedFirstSignal = true;

        bool isFirstSignal = (prevCount == 0);
        bool isLastSignal = (signalHitCount >= total);

        Debug.Log($"[Timeline Debug] Signal reached. passed={signalHitCount}/{total}, first={isFirstSignal}, last={isLastSignal}, time={now:F3}");

        SyncCharacterTransform();

        if (isFirstSignal)
        {
            // 第一個 Signal：不暫停。只有在畫面上完全沒有文字時才補讀一句。
            if (story != null && story.canContinue && dialogueText != null && string.IsNullOrWhiteSpace(dialogueText.text))
            {
                ContinueStory();
            }
            return;
        }

        if (isLastSignal)
        {
            // 最後一個 Signal：不暫停、不等待點擊，讓 Timeline 正常播完（播完後由 OnTimelineStopped 接手）
            isWaitingForTimelineClick = false;
            return;
        }

        // 中間的 Signal：暫停並等待玩家點擊
        if (globalDirector != null)
        {
            globalDirector.Pause();
        }
        isWaitingForTimelineClick = true;

        ProceduralBreathing[] breathings = FindObjectsOfType<ProceduralBreathing>(true);
        foreach (var b in breathings)
        {
            if (b != null) b.SetBreathingActive(true);
        }
    }

    /// <summary>
    /// 對話推進（ContinueStory）之後，決定 Timeline 要怎麼走：
    /// 1) 已停在最近的 Signal（等待點擊中）→ 從目前位置繼續播放
    /// 2) 還沒碰到最近的 Signal（動畫播放中）→ 跳到該 Signal 之後並繼續播放
    /// 3) 已經碰過最後一個 Signal → 不處理，讓 Timeline 自然播完
    /// sessionBefore：呼叫 ContinueStory 之前記錄的 timelineSession，若不同代表 Ink Tag 已經接管 Timeline，這裡就不要再動。
    /// </summary>
    private void CheckAndAdvanceTimelineForNextDialogue(int sessionBefore)
    {
        if (!isTimelineCutsceneActive) return;
        if (globalDirector == null) return;

        if (sessionBefore != timelineSession)
        {
            Debug.Log("[Timeline Debug] Timeline 已被 Ink Tag 切換或跳轉，略過本次推進。");
            return;
        }

        List<double> times = GetSortedSignalTimes();
        int total = times.Count;

        // 沒有 Signal 的 Timeline：只確保它繼續播放
        if (total == 0)
        {
            isWaitingForTimelineClick = false;
            if (globalDirector.state == PlayState.Paused) globalDirector.Play();
            return;
        }

        // 已碰過最後一個 Signal：讓 Timeline 正常播完，不跳轉
        if (signalHitCount >= total)
        {
            isWaitingForTimelineClick = false;
            return;
        }

        // 情況 1：已經停在最近的 Signal → 從目前位置繼續播放
        if (isWaitingForTimelineClick)
        {
            Debug.Log("[Timeline Debug] 已在 Signal 暫停點，從目前位置繼續播放。");
            isWaitingForTimelineClick = false;

            ProceduralBreathing[] breathings = FindObjectsOfType<ProceduralBreathing>(true);
            foreach (var b in breathings)
            {
                if (b != null) b.SetBreathingActive(false);
            }

            globalDirector.Play();
            return;
        }

        // 情況 2：還沒碰到最近的 Signal → 跳到該 Signal 之後繼續播放
        int idx = signalHitCount;
        double target = System.Math.Max(times[idx] + 0.001, globalDirector.time);

        Debug.Log($"[Timeline Debug] 動畫播放中點擊，跳到第 {idx + 1} 個 Signal ({times[idx]:F3}) 之後繼續播放。");

        signalHitCount = idx + 1;
        hasReachedFirstSignal = true;
        isWaitingForTimelineClick = false;
        lastSignalJumpFrame = Time.frameCount;

        ProceduralBreathing[] breathings2 = FindObjectsOfType<ProceduralBreathing>(true);
        foreach (var b in breathings2)
        {
            if (b != null) b.SetBreathingActive(false);
        }

        globalDirector.time = target;
        globalDirector.Evaluate();
        SyncCharacterTransform();
        globalDirector.Play();

        StartCoroutine(EnsureTimelinePlaying());
    }

    private IEnumerator EnsureTimelinePlaying()
    {
        yield return null;
        if (globalDirector != null && isTimelineCutsceneActive && !isWaitingForTimelineClick)
        {
            globalDirector.Play();
        }
    }

    /// <summary>
    /// 過場中玩家點擊（滑鼠 / 空白鍵）時呼叫。
    /// </summary>
    private void AdvanceTimelineAndDialogue()
    {
        List<double> times = GetSortedSignalTimes();
        int total = times.Count;

        // 沒有 Signal 的 Timeline：維持原本行為（推進 Ink，必要時結束過場）
        if (total == 0)
        {
            isWaitingForTimelineClick = false;

            if (story != null && story.canContinue)
            {
                ContinueStory();
            }
            else if (story != null && story.currentChoices.Count > 0)
            {
                StartCoroutine(ToggleChoiceUI(true));
            }
            else
            {
                CheckAndEndCutscene();
            }

            if (isTimelineCutsceneActive && globalDirector != null && globalDirector.state == PlayState.Paused)
            {
                globalDirector.Play();
            }
            return;
        }

        // 已碰過最後一個 Signal：不進行 Timeline 跳轉，讓 Timeline 正常播完。
        // 但如果 Ink 下一步是選項，點擊仍然要能把選項顯示出來（不影響 Timeline）。
        if (signalHitCount >= total)
        {
            if (story != null && !story.canContinue && story.currentChoices.Count > 0)
            {
                StartCoroutine(ToggleChoiceUI(true));
            }
            return;
        }

        // 推進一句 Ink，再決定 Timeline 要「從目前位置繼續」還是「跳到最近的 Signal」
        int sessionBefore = timelineSession;

        if (story != null)
        {
            if (story.canContinue)
            {
                ContinueStory();
            }
            else if (story.currentChoices.Count > 0)
            {
                StartCoroutine(ToggleChoiceUI(true));
            }
        }

        CheckAndAdvanceTimelineForNextDialogue(sessionBefore);
    }

    private void CheckAndEndCutscene()
    {
        if (story != null && !story.canContinue && story.currentChoices.Count == 0)
        {
            Debug.Log("[Timeline Debug] Cutscene conditions ended. Cleaning up Timeline state.");

            LogDialogueCommunicationMetrics("過場動畫結束");

            if (isTimelineCutsceneActive)
            {
                isTimelineCutsceneActive = false;
                SetTimelineSystemOverride(false);
            }

            if (globalDirector != null)
            {
                globalDirector.Stop();
            }

            ProceduralBreathing[] breathings = FindObjectsOfType<ProceduralBreathing>(true);
            foreach (var b in breathings)
            {
                if (b != null) b.SetBreathingActive(false);
            }

            if (dialogueText != null)
            {
                dialogueText.text = "";
            }
        }
    }

    private void ContinueStory()
    {
        if (story == null) return;

        isWaitingForChoice = false;
        if (!isTimelineCutsceneActive)
        {
            isTransitioning = false;
        }

        if (dialogueText == null)
        {
            dialogueText = FindObjectsOfType<TextMeshProUGUI>(true)
                .FirstOrDefault(t => t != null && t.name.Equals("Dialog", System.StringComparison.OrdinalIgnoreCase));
        }

        string text = "";
        bool foundText = false;

        while (story.canContinue)
        {
            text = story.Continue().Trim();
            List<string> currentTags = story.currentTags;

            bool hasTimelineTag = false; // 用來追蹤這次讀取是否有觸發 Timeline 動畫

            if (currentTags != null && currentTags.Count > 0)
            {
                foreach (var tag in currentTags)
                {
                    Debug.Log($"[Debug-Continue] 讀取到 Tag: {tag}");
                    if (SoundManager.Instance != null)
                    {
                        SoundManager.Instance.HandleInkTag(tag);
                    }

                    // 檢查這個 Tag 是不是切換 Timeline 動畫
                    if (tag.Contains(".timeline.") || tag.Contains("timeline."))
                    {
                        hasTimelineTag = true;
                    }
                }
                HandleTags(currentTags);
            }

            if (!string.IsNullOrWhiteSpace(text) && text != ".")
            {
                foundText = true;
            }

            if (isTimelineCutsceneActive && isWaitingForChoice)
            {
                break;
            }

            // 【核心修正】如果在讀取 Tag 時碰到了 Timeline 動畫，必須立刻強制跳出 while 迴圈！
            // 絕不讓它一口氣連鎖讀取到下一個結點或下一個動畫，確保當前動畫能安穩播放。
            if (hasTimelineTag)
            {
                Debug.Log("[Debug-Continue] 偵測到 Timeline 播放 Tag，強制中斷故事繼續往下讀，等待動畫播放完畢。");
                break;
            }

            if (foundText)
            {
                break;
            }
        }

        if (foundText)
        {
            Debug.Log($"[Debug-Continue] 顯示對話: {text} (dialogueText 是否存在: {dialogueText != null})");
            if (dialogueText != null)
            {
                dialogueText.gameObject.SetActive(true);
                if (text == ".") dialogueText.text = "";
                else dialogueText.text = text;
            }
            currentTargetObjectName = "";
        }
        else
        {
            if (text == "" || text == ".")
            {
                if (dialogueText != null) dialogueText.text = "";
            }
        }

        if (story.currentChoices.Count > 0)
        {
            // 【修正】選項要跟一般對話一樣「點擊後才切換」：
            // 這次有讀到文字 → 先顯示文字，選項等玩家下一次點擊再出現。
            // 這次沒有任何文字可讀（例如選完選項直接又是選項）→ 才直接顯示選項。
            if (!foundText)
            {
                StartCoroutine(ToggleChoiceUI(true));
            }
        }
        else if (!story.canContinue)
        {
            if (isTimelineCutsceneActive) return;
            CheckAndEndCutscene();
        }
    }

    private void LogDialogueCommunicationMetrics(string actionEventName, int selectedChoiceIndex = -1)
    {
        if (AnalyticsManager.Instance == null) return;

        int totalChoicesMade = dialogueGoodCount + dialogueNormalCount + dialogueBadCount;
        float goodRatio = (totalChoicesMade > 0) ? (float)dialogueGoodCount / totalChoicesMade : 0f;
        float normalRatio = (totalChoicesMade > 0) ? (float)dialogueNormalCount / totalChoicesMade : 0f;
        float badRatio = (totalChoicesMade > 0) ? (float)dialogueBadCount / totalChoicesMade : 0f;

        float dialogueEfficiency = (totalChoicesMade > 0) ? (float)dialogueScoreAccumulated / totalChoicesMade : 0f;

        string currentTimelineName = (globalDirector != null && globalDirector.playableAsset != null)
                                     ? globalDirector.playableAsset.name
                                     : "None_FreeRoam";

        Dictionary<string, object> metrics = new Dictionary<string, object>
        {
            { "動作事件", actionEventName },
            { "時間軸名稱", currentTimelineName },
            { "選取選項索引", selectedChoiceIndex },
            { "好對話選項數", dialogueGoodCount },
            { "普通對話選項數", dialogueNormalCount },
            { "差對話選項數", dialogueBadCount },
            { "總選取選項數", totalChoicesMade },
            { "累積對話分數", dialogueScoreAccumulated },
            { "好選項比例", goodRatio },
            { "普通選項比例", normalRatio },
            { "差選項比例", badRatio },
            { "對話效率", dialogueEfficiency }
        };

        AnalyticsManager.Instance.TrackEvent("溝通對話檔案", metrics);
    }

    IEnumerator OnChoiceSelected(int index)
    {
        Debug.Log($"<color=yellow>[Debug-Choice] 玩家點擊選擇了選項 Index: {index}</color>");
        isInputBlocked = true;

        if (isTimelineCutsceneActive)
        {
            string currentPath = (story != null && story.state != null) ? story.state.currentPathString : "";
            bool isIgnoredNode = !string.IsNullOrEmpty(currentPath) && (currentPath.Contains("Story5_evidence1") || currentPath.Contains("Story9_evidence1"));

            if (!isIgnoredNode)
            {
                int scoreAdd = 0;
                if (index == 0)
                {
                    scoreAdd = 10;
                    dialogueGoodCount++;
                }
                else if (index == 1)
                {
                    scoreAdd = 5;
                    dialogueNormalCount++;
                }
                else if (index >= 2)
                {
                    scoreAdd = 0;
                    dialogueBadCount++;
                }

                dialogueScoreAccumulated += scoreAdd;
                dialogueTotalChoices++;
            }

            LogDialogueCommunicationMetrics("choice_selected", index);
        }

        story.ChooseChoiceIndex(index);
        Debug.Log("[Debug-Choice] 已執行 story.ChooseChoiceIndex(index)");
        ClearChoices();

        int sessionBefore = timelineSession;
        ContinueStory();

        if (story.currentChoices.Count > 0)
        {
            // 【修正】是否立刻顯示由 ContinueStory 決定（有文字則等點擊）
            Debug.Log($"[Debug-Choice] 選擇後發現新的選項數量: {story.currentChoices.Count}");
        }
        else
        {
            Debug.Log("[Debug-Choice] 選擇後沒有新選項，目前 story.canContinue = " + story.canContinue);
        }

        CheckAndAdvanceTimelineForNextDialogue(sessionBefore);

        yield return new WaitForSeconds(bufferTime);
        isWaitingForChoice = false;
        isInputBlocked = false;
        Debug.Log("[Debug-Choice] OnChoiceSelected 結束，解鎖輸入");
    }

    IEnumerator SceneTransition(int nextScene)
    {
        isWaitingForChoice = true;
        isTransitioning = true;
        deferredTags.Clear();

        yield return new WaitForSeconds(bufferTime);
        yield return Fade(1);

        currentSceneID = nextScene;
        UpdateEnvironment(nextScene);

        if (deferredTags.Count > 0)
        {
            ExecuteTagsReal(deferredTags);
            deferredTags.Clear();
        }

        SetAllObjectsInteractable(true);

        yield return new WaitForSeconds(0.5f);
        yield return Fade(0);

        isTransitioning = false;
        isWaitingForChoice = false;

        ContinueStory();
    }

    IEnumerator ManualFadeSequence()
    {
        isWaitingForChoice = true;
        isTransitioning = true;
        deferredTags.Clear();

        yield return new WaitForSeconds(bufferTime);
        yield return Fade(1f);

        if (deferredTags.Count > 0)
        {
            ExecuteTagsReal(deferredTags);
            deferredTags.Clear();
        }

        yield return new WaitForSeconds(0.5f);
        yield return Fade(0f);

        if (deferredTags.Count > 0)
        {
            ExecuteTagsReal(deferredTags);
            deferredTags.Clear();
        }

        isTransitioning = false;
        isWaitingForChoice = false;

        ContinueStory();
    }

    IEnumerator FadeOutSequence(float duration = -1f)
    {
        isWaitingForChoice = true;
        isTransitioning = true;
        deferredTags.Clear();

        yield return new WaitForSeconds(bufferTime);
        yield return Fade(1f, duration);

        if (deferredTags.Count > 0)
        {
            ExecuteTagsReal(deferredTags);
            deferredTags.Clear();
        }

        isTransitioning = false;
        isWaitingForChoice = false;

        ContinueStory();
    }

    IEnumerator FadeInSequence(float duration = -1f)
    {
        isWaitingForChoice = true;
        isTransitioning = true;
        deferredTags.Clear();

        yield return new WaitForSeconds(bufferTime);
        yield return Fade(0f, duration);

        if (deferredTags.Count > 0)
        {
            ExecuteTagsReal(deferredTags);
            deferredTags.Clear();
        }

        isTransitioning = false;
        isWaitingForChoice = false;

        ContinueStory();
    }

    void UpdateEnvironment(int sceneID)
    {
        TextAsset sceneConfigCsv = Resources.Load<TextAsset>(csvResourcePath);
        if (sceneConfigCsv == null) return;

        string csvText = sceneConfigCsv.text.Replace("\r", "");
        string[] lines = csvText.Split('\n');

        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;
            string[] data = lines[i].Split(',');

            if (int.TryParse(data[0].Trim(), out int rowSceneID) && rowSceneID == sceneID)
            {
                string id = data[1].Trim();
                bool active = (data[2].Trim().ToLower() == "true");
                string knot = data[3].Trim();

                if (id.StartsWith("Task."))
                {
                    string taskID = id.Replace("Task.", "").Trim();
                    if (TaskSystemManager.Instance != null)
                    {
                        TaskSystemManager.Instance.ParseAndSetTask("Task.text." + taskID);
                    }
                    continue;
                }

                GameObject targetGo = FindGameObject(id);
                if (targetGo != null)
                {
                    targetGo.SetActive(active);
                    var io = targetGo.GetComponent<InteractableObject>();
                    if (io != null)
                    {
                        io.isInteractable = active;
                        if (!string.IsNullOrEmpty(knot)) io.targetKnot = knot;
                    }
                }
            }
        }
    }

    void HandleTags(List<string> tags)
    {
        if (tags == null || tags.Count == 0) return;

        if (isTransitioning)
        {
            foreach (var tag in tags)
            {
                deferredTags.Add(tag);
            }
            return;
        }

        ExecuteTagsReal(tags);
    }

    void ExecuteTagsReal(List<string> tags)
    {
        if (tags == null || tags.Count == 0) return;

        FadeType pendingFade = FadeType.None;
        float pendingFadeDuration = -1f;

        foreach (string tag in tags)
        {
            string cleanTag = tag.Trim();
            if (cleanTag.StartsWith("#")) cleanTag = cleanTag.Substring(1).Trim();
            if (cleanTag.Contains(".SetActive."))
            {
                ParseAndSetObjectActive(cleanTag);
            }
        }

        foreach (string tag in tags)
        {
            string cleanTag = tag.Trim();
            if (cleanTag.StartsWith("#")) cleanTag = cleanTag.Substring(1).Trim();
            if (cleanTag.Contains(".prefab."))
            {
                ParseAndSwapPrefab(cleanTag);
            }
        }

        foreach (string tag in tags)
        {
            string cleanTag = tag.Trim();
            if (cleanTag.StartsWith("#")) cleanTag = cleanTag.Substring(1).Trim();

            if (cleanTag.StartsWith("OpenTutorial", System.StringComparison.OrdinalIgnoreCase))
            {
                int startParen = cleanTag.IndexOf('(');
                int endParen = cleanTag.IndexOf(')');

                if (startParen != -1 && endParen != -1 && endParen > startParen)
                {
                    string paramStr = cleanTag.Substring(startParen + 1, endParen - startParen - 1).Trim();

                    if (paramStr.Contains(","))
                    {
                        string[] split = paramStr.Split(',');
                        List<int> pages = new List<int>();
                        foreach (string s in split)
                        {
                            if (int.TryParse(s.Trim(), out int pageNum))
                            {
                                pages.Add(pageNum);
                            }
                        }
                        if (TutorialManager.Instance != null)
                        {
                            TutorialManager.Instance.OpenTutorialPages(pages);
                        }
                        else
                        {
                            Debug.LogWarning("[GameManager Warning] TutorialManager instance not found.");
                        }
                    }
                    else
                    {
                        if (int.TryParse(paramStr, out int maxPage))
                        {
                            if (TutorialManager.Instance != null)
                            {
                                TutorialManager.Instance.OpenTutorialUpTo(maxPage);
                            }
                            else
                            {
                                Debug.LogWarning("[GameManager Warning] TutorialManager instance not found.");
                            }
                        }
                    }
                }
                continue;
            }

            if (cleanTag.StartsWith("delay.", System.StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (cleanTag.Contains(".isInspectable."))
            {
                ParseAndSetInspectable(cleanTag);
                continue;
            }

            if (cleanTag.Contains(".isInteractable."))
            {
                ParseAndSetInteractable(cleanTag);
                continue;
            }

            if (cleanTag.StartsWith("fade.Out", System.StringComparison.OrdinalIgnoreCase))
            {
                pendingFade = FadeType.Out;
                pendingFadeDuration = ExtractDurationFromFadeTag(cleanTag);
                continue;
            }
            else if (cleanTag.StartsWith("fade.In", System.StringComparison.OrdinalIgnoreCase))
            {
                pendingFade = FadeType.In;
                pendingFadeDuration = ExtractDurationFromFadeTag(cleanTag);
                continue;
            }
            else if (cleanTag.Equals("fade", System.StringComparison.OrdinalIgnoreCase))
            {
                pendingFade = FadeType.Full;
                continue;
            }

            if (cleanTag.Contains(".prefab.") || cleanTag.Contains(".SetActive."))
            {
                continue;
            }

            if (cleanTag.StartsWith("timer.", System.StringComparison.OrdinalIgnoreCase))
            {
                string[] parts = cleanTag.Split('.');
                if (parts.Length >= 3)
                {
                    if (float.TryParse(parts[1], out float delaySeconds))
                    {
                        string targetKnot = parts[2].Trim();

                        if (TimerManager.Instance != null)
                        {
                            TimerManager.Instance.StartTimer(delaySeconds, () => {
                                if (story != null)
                                {
                                    story.ChoosePathString(targetKnot);
                                    ContinueStory();
                                }
                            });
                        }
                    }
                }
                continue;
            }

            switch (cleanTag)
            {
                case string s when s.Equals("InteractableObject.non-interactable", System.StringComparison.OrdinalIgnoreCase):
                    SetAllObjectsInteractable(false);
                    break;
                case string s when s.StartsWith("camera.", System.StringComparison.OrdinalIgnoreCase):
                    ParseAndSwitchCamera(cleanTag);
                    break;
                case string s when s.StartsWith("LightingData_", System.StringComparison.OrdinalIgnoreCase) || s.StartsWith("LightingData.", System.StringComparison.OrdinalIgnoreCase):
                    if (SwitchBakedLight.Instance != null)
                    {
                        string presetName = "";
                        if (cleanTag.StartsWith("LightingData_", System.StringComparison.OrdinalIgnoreCase))
                        {
                            presetName = cleanTag.Substring("LightingData_".Length).Trim();
                        }
                        else if (cleanTag.StartsWith("LightingData.", System.StringComparison.OrdinalIgnoreCase))
                        {
                            presetName = cleanTag.Substring("LightingData.".Length).Trim();
                        }

                        bool foundDirectMatch = false;
                        if (!string.IsNullOrEmpty(presetName) && SwitchBakedLight.Instance.presets != null)
                        {
                            foreach (var p in SwitchBakedLight.Instance.presets)
                            {
                                if (p.configName.Equals(presetName, System.StringComparison.OrdinalIgnoreCase))
                                {
                                    foundDirectMatch = true;
                                    break;
                                }
                            }
                        }

                        if (foundDirectMatch)
                        {
                            SwitchBakedLight.Instance.ApplyLightmapPreset(presetName);
                        }
                        else
                        {
                            SwitchBakedLight.Instance.ApplyLightmapPreset(cleanTag);
                        }
                    }
                    else
                    {
                        Debug.LogWarning("[GameManager Warning] SwitchBakedLight instance not found.");
                    }
                    break;
                case string s when s.StartsWith("Emitter", System.StringComparison.OrdinalIgnoreCase):
                    ParseAndJumpToEmitter(cleanTag);
                    break;
                case string s when s.StartsWith("TimelineSignal", System.StringComparison.OrdinalIgnoreCase) || s.StartsWith("timeline.signal", System.StringComparison.OrdinalIgnoreCase):
                    ParseAndJumpTimelineSignal(cleanTag);
                    break;
                case string s when s.StartsWith("Task.text.", System.StringComparison.OrdinalIgnoreCase):
                    if (TaskSystemManager.Instance != null)
                    {
                        TaskSystemManager.Instance.ParseAndSetTask(cleanTag);
                    }
                    break;
                case string s when s.Contains(".mode."):
                    ParseAndSetObjectMode(cleanTag);
                    break;
                case string s when s.Contains(".material."):
                    ParseAndSwapMaterial(cleanTag);
                    break;
                case string s when s.Contains(".timeline."):
                    ParseAndPlayTimeline(cleanTag);
                    break;
                default:
                    ParseObjectAttributes(cleanTag);
                    break;
            }
        }

        switch (pendingFade)
        {
            case FadeType.Full:
                StartCoroutine(ManualFadeSequence());
                break;
            case FadeType.Out:
                StartCoroutine(FadeOutSequence(pendingFadeDuration));
                break;
            case FadeType.In:
                StartCoroutine(FadeInSequence(pendingFadeDuration));
                break;
        }
    }

    private float ExtractDurationFromFadeTag(string tag)
    {
        try
        {
            if (tag.Contains("(") && tag.Contains(")"))
            {
                int start = tag.IndexOf('(') + 1;
                int end = tag.IndexOf(')');
                string numStr = tag.Substring(start, end - start).Trim();
                if (float.TryParse(numStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float val)) return val;
            }
            else
            {
                string[] parts = tag.Split('.');
                if (parts.Length >= 3)
                {
                    if (float.TryParse(parts[parts.Length - 1].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float val)) return val;
                }
            }
        }
        catch { }
        return -1f;
    }

    void ParseAndSetInspectable(string tag)
    {
        string[] split = tag.Split(new string[] { ".isInspectable." }, System.StringSplitOptions.None);
        if (split.Length < 2) return;

        string objName = split[0].Trim();
        bool targetState = (split[1].Trim().ToLower() == "true");

        GameObject targetGo = FindGameObject(objName);
        if (targetGo != null)
        {
            var inspectable = targetGo.GetComponent<InspectableObject>() ?? targetGo.GetComponentInChildren<InspectableObject>();
            if (inspectable != null)
            {
                inspectable.enabled = targetState;
            }

            Debug.Log($"[GameManager] Object '{objName}' InspectableObject set to {targetState}");
        }
        else
        {
            Debug.LogWarning("[GameManager Warning] Target object '" + objName + "' not found for isInspectable update.");
        }
    }

    void ParseAndSetInteractable(string tag)
    {
        string[] split = tag.Split(new string[] { ".isInteractable." }, System.StringSplitOptions.None);
        if (split.Length < 2) return;

        string objName = split[0].Trim();
        bool targetState = (split[1].Trim().ToLower() == "true");

        GameObject targetGo = FindGameObject(objName);
        if (targetGo != null)
        {
            var interactable = targetGo.GetComponent<InteractableObject>() ?? targetGo.GetComponentInChildren<InteractableObject>();
            if (interactable != null)
            {
                interactable.enabled = targetState;
            }

            Debug.Log($"[GameManager] Object '{objName}' InteractableObject set to {targetState}");
        }
        else
        {
            Debug.LogWarning("[GameManager Warning] Target object '" + objName + "' not found for isInteractable update.");
        }
    }

    void ParseAndSetObjectMode(string tag)
    {
        string[] split = tag.Split(new string[] { ".mode." }, System.StringSplitOptions.None);
        if (split.Length < 2) return;

        string objName = split[0].Trim();
        string modeStr = split[1].Trim();

        GameObject targetGo = FindGameObject(objName);

        if (targetGo == null)
        {
            Debug.LogWarning("[GameManager Warning] Target object '" + objName + "' not found for mode switch.");
            return;
        }

        var interactor = targetGo.GetComponent<InteractableObject>();
        var inspector = targetGo.GetComponent("InspectableObject") as MonoBehaviour;

        if (modeStr.Equals("Inspector", System.StringComparison.OrdinalIgnoreCase))
        {
            if (interactor != null) interactor.enabled = false;
            if (inspector != null) inspector.enabled = true;
            Debug.Log("[GameManager Debug] Switched '" + objName + "' to Inspector mode.");
        }
        else if (modeStr.Equals("Interactor", System.StringComparison.OrdinalIgnoreCase))
        {
            if (interactor != null) interactor.enabled = true;
            if (inspector != null) inspector.enabled = false;
            Debug.Log("[GameManager Debug] Switched '" + objName + "' to Interactor mode.");
        }
    }

    void ParseAndSwitchCamera(string tag)
    {
        if (cinemachineCamera != null)
        {
            cinemachineCamera.enabled = true;
        }
    }

    void ParseAndJumpToEmitter(string tag)
    {
        int targetIndex = -1;

        if (tag.Contains("(") && tag.Contains(")"))
        {
            int start = tag.IndexOf('(') + 1;
            int end = tag.IndexOf(')');
            string numStr = tag.Substring(start, end - start).Trim();
            int.TryParse(numStr, out targetIndex);
        }
        else if (tag.Contains("."))
        {
            string[] parts = tag.Split('.');
            if (parts.Length >= 2)
            {
                int.TryParse(parts[1].Trim(), out targetIndex);
            }
        }

        if (targetIndex > 0)
        {
            JumpToNthEmitter(targetIndex);
        }
    }

    void ParseAndJumpTimelineSignal(string tag)
    {
        int targetIndex = -1;

        if (tag.Contains("(") && tag.Contains(")"))
        {
            int start = tag.IndexOf('(') + 1;
            int end = tag.IndexOf(')');
            string numStr = tag.Substring(start, end - start).Trim();
            int.TryParse(numStr, out targetIndex);
        }
        else if (tag.Contains("."))
        {
            string[] parts = tag.Split('.');
            if (parts.Length >= 2)
            {
                int.TryParse(parts[parts.Length - 1].Trim(), out targetIndex);
            }
        }

        if (targetIndex > 0)
        {
            JumpTimelineSignalAndPlay(targetIndex);
        }
    }

    public void JumpToNthEmitter(int targetEmitterIndex)
    {
        if (globalDirector == null || globalDirector.playableAsset == null) return;

        TimelineAsset timelineAsset = globalDirector.playableAsset as TimelineAsset;
        if (timelineAsset == null) return;

        List<SignalEmitter> allEmitters = new List<SignalEmitter>();

        if (timelineAsset.markerTrack != null)
        {
            foreach (var marker in timelineAsset.markerTrack.GetMarkers())
            {
                if (marker is SignalEmitter emitter)
                {
                    allEmitters.Add(emitter);
                }
            }
        }

        foreach (var track in timelineAsset.GetOutputTracks())
        {
            foreach (var marker in track.GetMarkers())
            {
                if (marker is SignalEmitter emitter && !allEmitters.Contains(emitter))
                {
                    allEmitters.Add(emitter);
                }
            }
        }

        allEmitters.Sort((a, b) => a.time.CompareTo(b.time));

        int arrayIndex = targetEmitterIndex - 1;
        if (arrayIndex >= 0 && arrayIndex < allEmitters.Count)
        {
            double targetTime = allEmitters[arrayIndex].time;

            Debug.Log("[Timeline Debug] Jumping to Emitter index: " + targetEmitterIndex + " at time: " + targetTime);

            if (!isTimelineCutsceneActive)
            {
                isTimelineCutsceneActive = true;
                SetTimelineSystemOverride(true);
            }

            // 讓計數與跳轉位置一致：跳到第 N 個 Emitter 前，已通過 N-1 個
            timelineSession++;
            signalHitCount = Mathf.Max(0, targetEmitterIndex - 1);
            if (targetEmitterIndex > 1) hasReachedFirstSignal = true;

            globalDirector.Pause();
            globalDirector.time = targetTime;

            globalDirector.Evaluate();
            globalDirector.Play();

            isWaitingForTimelineClick = false;
        }
        else
        {
            Debug.LogWarning("[Timeline Warning] Emitter index " + targetEmitterIndex + " not found!");
        }
    }

    public void JumpTimelineSignalAndPlay(int targetEmitterIndex)
    {
        if (globalDirector == null || globalDirector.playableAsset == null) return;

        TimelineAsset timelineAsset = globalDirector.playableAsset as TimelineAsset;
        if (timelineAsset == null) return;

        List<SignalEmitter> allEmitters = new List<SignalEmitter>();

        if (timelineAsset.markerTrack != null)
        {
            foreach (var marker in timelineAsset.markerTrack.GetMarkers())
            {
                if (marker is SignalEmitter emitter)
                {
                    allEmitters.Add(emitter);
                }
            }
        }

        foreach (var track in timelineAsset.GetOutputTracks())
        {
            foreach (var marker in track.GetMarkers())
            {
                if (marker is SignalEmitter emitter && !allEmitters.Contains(emitter))
                {
                    allEmitters.Add(emitter);
                }
            }
        }

        allEmitters.Sort((a, b) => a.time.CompareTo(b.time));

        int arrayIndex = targetEmitterIndex - 1;
        if (arrayIndex >= 0 && arrayIndex < allEmitters.Count)
        {
            double targetTime = allEmitters[arrayIndex].time;

            Debug.Log("[Timeline Debug] JumpTimelineSignalAndPlay index: " + targetEmitterIndex + " at time: " + targetTime);

            if (!isTimelineCutsceneActive)
            {
                isTimelineCutsceneActive = true;
                SetTimelineSystemOverride(true);
            }

            timelineSession++;
            lastSignalJumpFrame = Time.frameCount;
            signalHitCount = targetEmitterIndex;
            hasReachedFirstSignal = true;
            isWaitingForTimelineClick = false;

            globalDirector.Pause();
            globalDirector.time = targetTime + 0.001;
            globalDirector.Evaluate();
            globalDirector.Play();
        }
        else
        {
            Debug.LogWarning("[Timeline Warning] TimelineSignal index " + targetEmitterIndex + " not found!");
        }
    }

    void ParseAndSetObjectActive(string tag)
    {
        string[] split = tag.Split(new string[] { ".SetActive." }, System.StringSplitOptions.None);
        if (split.Length < 2) return;

        string objName = split[0].Trim();
        bool targetState = (split[1].Trim().ToLower() == "true");

        GameObject targetGo = FindGameObject(objName);

        if (targetGo != null)
        {
            targetGo.SetActive(targetState);
            var io = targetGo.GetComponent<InteractableObject>();
            if (io != null) io.enabled = targetState;
        }
        else
        {
            Debug.LogWarning("[GameManager Warning] Target object '" + objName + "' not found for SetActive.");
        }
    }

    void SetAllObjectsInteractable(bool state)
    {
        InteractableObject[] allObjects = FindObjectsOfType<InteractableObject>(true);
        foreach (var obj in allObjects)
        {
            obj.enabled = state;
        }
    }

    void ParseAndSwapPrefab(string tag)
    {
        string[] split = tag.Split(new string[] { ".prefab." }, System.StringSplitOptions.None);
        if (split.Length < 2) return;

        string objName = split[0].Trim();
        string prefabName = split[1].Trim();

        currentTargetObjectName = objName;

        Vector3 oldLocalPosition = Vector3.zero;
        Quaternion oldLocalRotation = Quaternion.identity;
        Vector3 oldLocalScale = Vector3.one;
        int oldLayer = 0;
        string oldTag = "Untagged";
        string oldTargetKnot = "";
        Transform oldParentNode = environmentHolder;

        GameObject oldGo = FindGameObject(objName);

        if (oldGo != null)
        {
            oldLocalPosition = oldGo.transform.localPosition;
            oldLocalRotation = oldGo.transform.localRotation;
            oldLocalScale = oldGo.transform.localScale;
            oldLayer = oldGo.layer;
            oldTag = oldGo.tag;
            oldParentNode = oldGo.transform.parent;

            var io = oldGo.GetComponent<InteractableObject>();
            if (io != null) oldTargetKnot = io.targetKnot;

            Destroy(oldGo);
            allSceneObjects.Remove(objName);
        }

        string loadPath = "Prefabs/" + objName + "/" + prefabName;
        GameObject newPrefab = Resources.Load<GameObject>(loadPath);

        if (newPrefab != null)
        {
            GameObject spawnedInstance = Instantiate(newPrefab, oldParentNode);
            spawnedInstance.name = objName;
            spawnedInstance.transform.localPosition = oldLocalPosition;
            spawnedInstance.transform.localRotation = oldLocalRotation;
            spawnedInstance.transform.localScale = oldLocalScale;
            spawnedInstance.gameObject.layer = oldLayer;
            spawnedInstance.gameObject.tag = oldTag;

            foreach (Transform child in spawnedInstance.GetComponentsInChildren<Transform>(true))
            {
                child.gameObject.layer = oldLayer;
            }

            var ioComponent = spawnedInstance.GetComponent<InteractableObject>();
            if (ioComponent == null) spawnedInstance.AddComponent<InteractableObject>();
            if (!string.IsNullOrEmpty(oldTargetKnot)) ioComponent.targetKnot = oldTargetKnot;

            RefreshSceneObjectsDictionary();
        }
    }

    void ParseObjectAttributes(string tag)
    {
        string targetObjectName = "";
        string commandContent = "";

        if (tag.Contains(".position") || tag.Contains(".rotation") || tag.Contains(".TargetKnot"))
        {
            int firstDotIndex = tag.IndexOf('.');
            if (firstDotIndex > 0)
            {
                targetObjectName = tag.Substring(0, firstDotIndex).Trim();
                commandContent = tag.Substring(firstDotIndex + 1).Trim();
            }
        }
        else
        {
            targetObjectName = currentTargetObjectName;
            commandContent = tag;
        }

        if (string.IsNullOrEmpty(targetObjectName)) return;

        GameObject targetGo = FindGameObject(targetObjectName);

        if (targetGo == null) return;

        if (commandContent.StartsWith("position"))
        {
            Vector3 targetPosition = ExtractVector3(commandContent);
            var cc = targetGo.GetComponent<CharacterController>();
            if (cc != null)
            {
                cc.enabled = false;
                targetGo.transform.position = targetPosition;
                cc.enabled = true;
            }
            else targetGo.transform.position = targetPosition;
        }
        else if (commandContent.StartsWith("rotation"))
        {
            Vector3 targetRotation = ExtractVector3(commandContent);
            var cc = targetGo.GetComponent<CharacterController>();
            if (cc != null)
            {
                cc.enabled = false;
                targetGo.transform.eulerAngles = targetRotation;
                cc.enabled = true;
            }
            else targetGo.transform.eulerAngles = targetRotation;
        }
        else if (commandContent.StartsWith("TargetKnot"))
        {
            var targetInteract = targetGo.GetComponent<InteractableObject>();
            if (targetInteract != null)
            {
                string[] parts = commandContent.Split('=');
                if (parts.Length >= 2)
                {
                    targetInteract.targetKnot = parts[1].Replace("\"", "").Trim();
                }
            }
        }
        var targetDoor = targetGo.GetComponent<InteractiveDoor>();
        if (targetDoor != null)
        {
            string[] parts = commandContent.Split('=');
            if (parts.Length >= 2)
            {
                targetDoor.targetKnot = parts[1].Replace("\"", "").Trim();
            }
        }
    }

    void ParseAndSwapMaterial(string tag)
    {
        string[] split = tag.Split(new string[] { ".material." }, System.StringSplitOptions.None);
        if (split.Length < 2) return;

        string objName = split[0].Trim();
        string materialName = split[1].Trim();

        GameObject targetGo = FindGameObject(objName);

        if (targetGo != null)
        {
            Material newMat = Resources.Load<Material>("Prefabs/" + objName + "/Materials/" + materialName);
            if (newMat != null)
            {
                var renderer = targetGo.GetComponentInChildren<MeshRenderer>();
                if (renderer != null) renderer.material = newMat;
            }
        }
    }

    void ParseAndPlayTimeline(string tag)
    {
        if (globalDirector == null)
        {
            globalDirector = FindObjectOfType<PlayableDirector>();
        }

        if (globalDirector == null)
        {
            Debug.LogError("[Timeline Error] globalDirector is null!");
            return;
        }

        string timelineName = "";
        if (tag.Contains(".timeline."))
        {
            string[] split = tag.Split(new string[] { ".timeline." }, System.StringSplitOptions.None);
            if (split.Length >= 2) timelineName = split[split.Length - 1].Trim();
        }
        else if (tag.Contains("timeline."))
        {
            string[] split = tag.Split(new string[] { "timeline." }, System.StringSplitOptions.None);
            if (split.Length >= 2) timelineName = split[split.Length - 1].Trim();
        }

        if (string.IsNullOrEmpty(timelineName))
        {
            Debug.LogError("[Timeline Error] 無法從 Tag 中解析出 Timeline 名稱：" + tag);
            return;
        }

        Debug.Log($"<color=magenta>[Timeline Debug] 準備解析並播放 Timeline: [{timelineName}] (原始 Tag: {tag})</color>");

        PlayableAsset timelineAsset = Resources.Load<PlayableAsset>("Timelines/" + timelineName);

        if (timelineAsset != null)
        {
            timelineSession++;
            bool hasAnySignal = false;
            if (timelineAsset is TimelineAsset tAsset)
            {
                List<SignalEmitter> allEmitters = new List<SignalEmitter>();
                if (tAsset.markerTrack != null)
                {
                    foreach (var marker in tAsset.markerTrack.GetMarkers())
                    {
                        if (marker is SignalEmitter emitter) allEmitters.Add(emitter);
                    }
                }
                foreach (var track in tAsset.GetOutputTracks())
                {
                    foreach (var marker in track.GetMarkers())
                    {
                        if (marker is SignalEmitter emitter && !allEmitters.Contains(emitter))
                        {
                            allEmitters.Add(emitter);
                        }
                    }
                }
                hasAnySignal = (allEmitters.Count > 0);
            }

            if (!hasAnySignal)
            {
                Debug.Log($"<color=yellow>[Timeline Debug] Timeline [{timelineName}] 沒有 Signal，將直接正常播放，播放完畢後推進 Ink。</color>");
                isTimelineCutsceneActive = true;
                isWaitingForTimelineClick = false;
                hasReachedFirstSignal = true;
                signalHitCount = 0;

                SetTimelineSystemOverride(true);
            }
            else
            {
                Debug.Log($"<color=green>[Timeline Debug] Timeline [{timelineName}] 包含 Signal，啟用互動過場點擊機制。</color>");
                isTimelineCutsceneActive = true;
                isWaitingForTimelineClick = false;
                hasReachedFirstSignal = false;
                signalHitCount = 0;

                SetTimelineSystemOverride(true);
            }

            StartCoroutine(PlayTimelineRoutine(timelineAsset));
        }
        else
        {
            Debug.LogError("[Timeline Error] Cannot find asset at: Resources/Timelines/" + timelineName);
        }
    }

    private IEnumerator PlayTimelineRoutine(PlayableAsset asset)
    {
        isSwitchingTimeline = true;
        globalDirector.Stop();
        globalDirector.playableAsset = asset;

        // 給 Unity 一幀的時間來初始化 Asset
        yield return null;

        globalDirector.time = 0;
        globalDirector.Play();
        isSwitchingTimeline = false;
    }

    Vector3 ExtractVector3(string tag)
    {
        try
        {
            int start = tag.IndexOf('(') + 1;
            int end = tag.IndexOf(')');
            string content = tag.Substring(start, end - start);
            string[] values = content.Split(',');
            return new Vector3(float.Parse(values[0].Trim()), float.Parse(values[1].Trim()), float.Parse(values[2].Trim()));
        }
        catch
        {
            return Vector3.zero;
        }
    }

    void RefreshSceneObjectsDictionary()
    {
        allSceneObjects.Clear();
        foreach (var obj in FindObjectsOfType<InteractableObject>(true))
        {
            if (!allSceneObjects.ContainsKey(obj.name)) allSceneObjects[obj.name] = obj;
        }
    }

    private enum FadeType
    {
        None,
        Full,
        Out,
        In
    }

    IEnumerator Fade(float targetAlpha, float customDuration = -1f)
    {
        float duration = customDuration > 0f ? customDuration : fadeDuration;
        fadeGroup.blocksRaycasts = (targetAlpha > 0.5f);
        float speed = Mathf.Abs(fadeGroup.alpha - targetAlpha) / duration;
        while (!Mathf.Approximately(fadeGroup.alpha, targetAlpha))
        {
            fadeGroup.alpha = Mathf.MoveTowards(fadeGroup.alpha, targetAlpha, speed * Time.deltaTime);
            yield return null;
        }
        fadeGroup.alpha = targetAlpha;
    }

    IEnumerator ToggleChoiceUI(bool show)
    {
        isWaitingForChoice = show;
        yield return new WaitForSeconds(bufferTime);
        if (show) { dialogueText.text = ""; DisplayChoices(); }
    }

    void DisplayChoices()
    {
        ClearChoices();

        List<Choice> choices = story.currentChoices;

        if (isTimelineCutsceneActive)
        {
            choices = choices.OrderBy(x => Random.value).ToList();
        }

        foreach (Choice choice in choices)
        {
            GameObject btn = Instantiate(choiceButtonPrefab, choiceHolder);
            btn.GetComponentInChildren<TextMeshProUGUI>().text = choice.text;

            int originalIndex = choice.index;
            btn.GetComponent<Button>().onClick.AddListener(() => StartCoroutine(OnChoiceSelected(originalIndex)));
        }
    }

    bool DetectObjectClick()
    {
        if (IsPointerOverUIElement())
        {
            return false;
        }

        Camera mainCam = Camera.main;
        if (mainCam == null)
        {
            Debug.LogWarning("[GameManager Debug] MainCamera not found!");
            return false;
        }

        Ray ray = mainCam.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 100f))
        {
            InteractableObject interactable = hit.collider.GetComponent<InteractableObject>();
            if (interactable == null) interactable = hit.collider.GetComponentInChildren<InteractableObject>();
            if (interactable == null) interactable = hit.collider.GetComponentInParent<InteractableObject>();

            if (interactable != null)
            {
                if (!interactable.enabled)
                {
                    Debug.LogWarning("[GameManager Debug] Hit [" + interactable.name + "], but component is disabled.");
                    return false;
                }

                if (string.IsNullOrEmpty(interactable.targetKnot))
                {
                    Debug.LogWarning("[GameManager Debug] Hit [" + interactable.name + "], but targetKnot is EMPTY.");
                    return false;
                }

                Debug.Log("[GameManager Debug] Interacted with: " + interactable.name + ", jumping to knot: " + interactable.targetKnot);

                // 強制重置阻擋狀態，確保點擊物件後能立刻推進
                isTransitioning = false;
                isWaitingForChoice = false;

                story.ChoosePathString(interactable.targetKnot);
                ContinueStory(); // 選項是否立刻顯示由 ContinueStory 決定（有文字則等點擊）

                return true;
            }
            else
            {
                Debug.LogWarning("[GameManager Debug] Hit [" + hit.collider.name + "], but no InteractableObject component found.");
            }
        }
        else
        {
            Debug.LogWarning("[GameManager Debug] Raycast missed all 3D colliders.");
        }

        return false;
    }

    private void SyncCharacterTransform()
    {
        GameObject player = GameObject.FindWithTag("Player");
        if (player == null) return;

        Animator anim = player.GetComponent<Animator>();
        if (anim != null)
        {
            Vector3 currentWorldPos = anim.rootPosition;
            if (currentWorldPos != Vector3.zero)
            {
                player.transform.position = currentWorldPos;
                Physics.SyncTransforms();
            }
        }
    }

    public void RecordVerificationResult(bool isSuccess)
    {
        if (isSuccess)
        {
            verifySuccessCount++;
            Debug.Log("[GameManager] Verify Success! Current Success Count: " + verifySuccessCount);
        }
        else
        {
            verifyFailCount++;
            Debug.Log("[GameManager] Verify Fail! Current Fail Count: " + verifyFailCount);
        }
    }

    public void RecordObjectInspected(InspectableObject target)
    {
        if (target != null)
        {
            string objID = target.mainInfoID;
            if (string.IsNullOrEmpty(objID)) objID = target.gameObject.name;
            if (!inspectedObjectIDs.Contains(objID))
            {
                inspectedObjectIDs.Add(objID);
                Debug.Log("[GameManager] Object Inspected Recorded: " + objID + " | Total Inspected: " + inspectedObjectIDs.Count);
            }
        }
    }

    public float CalculateSearchScore()
    {
        InspectableObject[] allInspectables = FindObjectsOfType<InspectableObject>(true);
        var validInspectables = allInspectables.Where(obj => obj != null && !obj.name.Contains("[INSPECT_CLONE]")).ToArray();

        int totalObjects = validInspectables.Length;
        int foundObjects = Mathf.Min(inspectedObjectIDs.Count, totalObjects);

        int totalSubInfos = 0;
        int foundSubInfos = 0;

        foreach (var obj in validInspectables)
        {
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

        float objectRatio = (totalObjects > 0) ? ((float)foundObjects / totalObjects) : 1f;
        float subRatio = (totalSubInfos > 0) ? ((float)foundSubInfos / totalSubInfos) : 1f;

        float score1 = (subRatio * 70f) + (objectRatio * 30f);
        return Mathf.Clamp(score1, 0f, 100f);
    }

    public float CalculateVerifyScore()
    {
        int targetSuccess = 2;
        InspectableObject[] allInspectables = FindObjectsOfType<InspectableObject>(true);
        var validInspectables = allInspectables.Where(obj => obj != null && !obj.name.Contains("[INSPECT_CLONE]")).ToArray();

        int verifiedSubTotal = 0;
        foreach (var obj in validInspectables)
        {
            if (obj.subObjectInfos != null)
            {
                foreach (var sub in obj.subObjectInfos)
                {
                    if (sub != null && sub.isVerified) verifiedSubTotal++;
                }
            }
        }

        int successfulMatches = Mathf.Max(verifySuccessCount, VirtualBrowserController.persistentVerifySuccessCount, verifiedSubTotal);
        float successRatio = (targetSuccess > 0) ? Mathf.Clamp01((float)successfulMatches / targetSuccess) : 1f;

        float score2 = successRatio * 80f;
        return Mathf.Clamp(score2, 0f, 100f);
    }

    public float CalculateDialogueScore()
    {
        if (dialogueTotalChoices == 0) return 0f;

        float rawRatio = (float)(dialogueGoodCount * 3 + dialogueNormalCount * 2 + dialogueBadCount * 1) / (3f * dialogueTotalChoices);
        return Mathf.Clamp01(rawRatio) * 100f;
    }

    public float GetFinalTotalScore()
    {
        if (enableScoreOverride)
        {
            return Mathf.Clamp(overrideTotalScore, 0f, 100f);
        }

        float s1 = CalculateSearchScore();
        float s2 = CalculateVerifyScore();
        float s3 = CalculateDialogueScore();
        return (s1 * searchWeight) + (s2 * verifyWeight) + (s3 * dialogueWeight);
    }

    private void UpdateScoreDisplayUI()
    {
        if (scoreDisplayTMP != null)
        {
            InspectableObject[] allInspectables = FindObjectsOfType<InspectableObject>(true);
            var validInspectables = allInspectables.Where(obj => obj != null && !obj.name.Contains("[INSPECT_CLONE]")).ToArray();

            int totalObjects = validInspectables.Length;
            int foundObjects = Mathf.Min(inspectedObjectIDs.Count, totalObjects);

            int totalSubInfos = 0;
            int foundSubInfos = 0;
            int verifiedSubInfos = 0;
            int currentFailCount = Mathf.Max(verifyFailCount, VirtualBrowserController.persistentVerifyFailCount);

            foreach (var obj in validInspectables)
            {
                if (obj.subObjectInfos != null)
                {
                    foreach (var sub in obj.subObjectInfos)
                    {
                        if (sub != null)
                        {
                            totalSubInfos++;
                            if (sub.isFound || sub.isVerified) foundSubInfos++;
                            if (sub.isVerified) verifiedSubInfos++;
                        }
                    }
                }
            }

            int finalSuccessCount = Mathf.Max(verifySuccessCount, verifiedSubInfos);

            float searchRaw = CalculateSearchScore();
            float searchWeighted = searchRaw * searchWeight;

            float verifyRaw = CalculateVerifyScore();
            float verifyWeighted = verifyRaw * verifyWeight;

            float dialogueRaw = CalculateDialogueScore();
            float dialogueWeighted = dialogueRaw * dialogueWeight;

            float totalScore = GetFinalTotalScore();

            string overrideNotice = enableScoreOverride ? " <color=yellow>[測試覆寫中]</color>" : "";

            scoreDisplayTMP.text =
                $"【第一關：線索蒐集】\n" +
                $"已調查物件: {foundObjects} / {totalObjects} | 線索解鎖: {foundSubInfos} / {totalSubInfos}\n" +
                $"原始得分: {searchRaw:F1}分 (線索70% + 物件30%)\n" +
                $"加權得分: {searchRaw:F1} * {searchWeight:F2} = {searchWeighted:F2}分\n\n" +
                $"【第二關：資訊查證】\n" +
                $"成功比對: {finalSuccessCount} / 2 | 失敗比對次數: {currentFailCount}\n" +
                $"原始得分: {verifyRaw:F1}分 (成功*80)\n" +
                $"加權得分: {verifyRaw:F1} * {verifyWeight:F2} = {verifyWeighted:F2}分\n\n" +
                $"【第三關：角色對話互動】\n" +
                $"已選選項總數: {dialogueTotalChoices} | 好: {dialogueGoodCount} | 普通: {dialogueNormalCount} | 差: {dialogueBadCount}\n" +
                $"累積對話分數: {dialogueScoreAccumulated}分\n" +
                $"原始得分: {dialogueRaw:F1}分\n" +
                $"加權得分: {dialogueRaw:F1} * {dialogueWeight:F2} = {dialogueWeighted:F2}分\n\n" +
                $"==================\n" +
                $"總得分 (TotalScore): {totalScore:F1}分{overrideNotice}";
        }
    }

    void ClearChoices() { if (choiceHolder) foreach (Transform child in choiceHolder) Destroy(child.gameObject); }

    public void ExecuteKnot(string knotName)
    {
        if (story != null && !string.IsNullOrEmpty(knotName))
        {
            Debug.Log($"[Debug-Knot] ExecuteKnot 被呼叫，目標 Knot: {knotName} | cutsceneActive={isTimelineCutsceneActive}, isTransitioning={isTransitioning}, isWaitingForChoice={isWaitingForChoice}");

            // 強制解除可能卡住的過場或等待狀態，確保門的對話能順利讀取
            isTransitioning = false;
            isWaitingForChoice = false;

            story.ChoosePathString(knotName);

            ContinueStory();

            Debug.Log($"[Debug-Knot] ExecuteKnot 執行完畢，目前 story.canContinue = {story.canContinue}, currentChoices 數量 = {story.currentChoices.Count}");
            // 選項是否立刻顯示由 ContinueStory 決定（有文字則等點擊）
        }
        else
        {
            Debug.LogWarning("[Debug-Knot] ExecuteKnot 失敗：story 為 null 或 knotName 是空的！");
        }
    }
}