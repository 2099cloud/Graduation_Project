using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Ink.Runtime;
using TMPro;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using UnityEngine.SceneManagement;
using System.Linq;

public class GameManager : MonoBehaviour
{
    // 使用 GameLightingPreset 避開所有 Unity C++ 原生內部類別名稱
    [System.Serializable]
    public struct GameLightingPreset
    {
        public string configName;       // Ink 標籤名稱，例如 "LightingData_Night" 或 "LightingData_Day"
        public Color lightColor;        // 太陽光顏色
        public float lightIntensity;    // 太陽光強度
        public Vector3 lightRotation;   // 太陽光角度 (Euler Angles)
        public Color ambientLightColor; // 環境光顏色 (RenderSettings.ambientLight)
        public string skyboxImageName;  // Skybox 圖片名稱

        [Header("Area Light 配置")]
        public Color areaLightColor;    // Area Light 顏色
        public float areaLightIntensity;// Area Light 強度
    }

    [Header("Ink 配置")]
    public TextAsset inkJsonAsset;
    private Story story;

    [Header("UI 引用")]
    public TextMeshProUGUI dialogueText;
    public GameObject choiceButtonPrefab;
    public Transform choiceHolder;

    [Header("任務系統配置")]
    public TextMeshProUGUI taskText;
    public string taskCsvPath = "TaskManager";

    [Header("Timeline 配置")]
    public PlayableDirector globalDirector;

    [Header("混合光照 (Mixed Lighting) 配置")]
    [Tooltip("請拖入主要的平行光 (Directional Light)")]
    public Light mainDirectionalLight;
    [Tooltip("請拖入區域光 (Area Light)")]
    public Light mainAreaLight;
    [Tooltip("遊戲啟動時預設載入的光照配置名稱 (例如：LightingData_Night)")]
    public string defaultLightingConfigName = "LightingData_Night";

    [Header("光照 Preset 列表")]
    public List<GameLightingPreset> lightingPresets = new List<GameLightingPreset>
    {
        new GameLightingPreset
        {
            configName = "LightingData_Day",
            lightColor = new Color(1.0f, 0.95f, 0.84f),
            lightIntensity = 1.2f,
            lightRotation = new Vector3(50f, -30f, 0f),
            ambientLightColor = new Color(0.6f, 0.6f, 0.65f),
            skyboxImageName = "Day",
            areaLightColor = new Color(1.0f, 0.95f, 0.85f),
            areaLightIntensity = 1.0f
        },
        new GameLightingPreset
        {
            configName = "LightingData_Night",
            lightColor = new Color(0.2f, 0.3f, 0.5f),
            lightIntensity = 0.2f,
            lightRotation = new Vector3(20f, -150f, 0f),
            ambientLightColor = new Color(0.05f, 0.08f, 0.15f),
            skyboxImageName = "Night",
            areaLightColor = new Color(0.1f, 0.15f, 0.3f),
            areaLightIntensity = 0.2f
        }
    };

    [Header("Skybox 配置")]
    [Tooltip("請拖入主要的 Skybox 材質球")]
    public Material skyboxMaterial;
    [Tooltip("圖片在 Resources 資料夾中的相對路徑，例如：Skyboxes/")]
    public string skyboxResourcesPath = "Skyboxes/";

    [Header("數據與過渡配置")]
    public string csvResourcePath = "SceneConfig";
    public CanvasGroup fadeGroup;
    public float fadeDuration = 0.5f;
    public float bufferTime = 0.5f;

    [Header("動態生成生成點")]
    public Transform environmentHolder;

    [Header("相機與物理控制配置")]
    [Tooltip("如果你的相機有掛載自訂的移動/旋轉控制腳本，請拖入此欄位。")]
    public MonoBehaviour[] cameraControlScripts;

    private bool isWaitingForChoice = false;
    private Dictionary<string, InteractableObject> allSceneObjects = new Dictionary<string, InteractableObject>();
    private int currentSceneID = 0;

    private string currentTargetObjectName = "";
    private string currentTaskTitle = "";
    private string currentTaskFullContent = "";
    private bool isTaskPopupOpen = false;
    private bool isTransitioning = false;
    private List<string> deferredTags = new List<string>();

    private Vector3 originalPosition;
    private Vector2 originalPivot;
    private Vector2 originalAnchorMin;
    private Vector2 originalAnchorMax;
    private Vector2 originalAnchoredPosition;
    private float originalFontSize;
    private Color originalColor;
    private Transform originalParent;

    private bool isWaitingForTimelineClick = false;
    private bool isTimelineCutsceneActive = false;

    // 用於在劇情中暫時關閉組件，結束後復原
    private List<CharacterController> disabledControllers = new List<CharacterController>();
    private List<MonoBehaviour> disabledCinemachineCameras = new List<MonoBehaviour>();
    private List<MonoBehaviour> disabledPlayerControlScripts = new List<MonoBehaviour>();

    // 用於儲存並復原 Animator 的 Root Motion 狀態
    private Dictionary<Animator, bool> originalRootMotionStates = new Dictionary<Animator, bool>();

    void Awake()
    {
        RefreshSceneObjectsDictionary();

        if (taskText != null)
        {
            var rect = taskText.rectTransform;
            originalPosition = rect.localPosition;
            originalPivot = rect.pivot;
            originalAnchorMin = rect.anchorMin;
            originalAnchorMax = rect.anchorMax;
            originalAnchoredPosition = rect.anchoredPosition;
            originalFontSize = taskText.fontSize;
            originalColor = taskText.color;
            originalParent = taskText.transform.parent;

            Button btn = taskText.gameObject.GetComponent<Button>();
            if (btn == null) btn = taskText.gameObject.AddComponent<Button>();
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(OnTaskTextClicked);
        }

        if (cameraControlScripts == null || cameraControlScripts.Length == 0)
        {
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                List<MonoBehaviour> foundScripts = new List<MonoBehaviour>();
                foreach (var script in mainCam.GetComponents<MonoBehaviour>())
                {
                    if (script != null && script.GetType().Namespace != "UnityEngine")
                    {
                        foundScripts.Add(script);
                    }
                }
                cameraControlScripts = foundScripts.ToArray();
            }
        }
    }

    void Start()
    {
        // 1. 遊戲啟動時直接套用預設光照配置
        if (!string.IsNullOrEmpty(defaultLightingConfigName))
        {
            ParseAndSwapLightingData(defaultLightingConfigName);
        }

        if (!inkJsonAsset) return;
        story = new Story(inkJsonAsset.text);

        story.ObserveVariable("scene", (varName, newValue) => {
            StartCoroutine(SceneTransition((int)newValue));
        });

        ContinueStory();
    }

    void Update()
    {
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

        if (isTaskPopupOpen)
        {
            if (Input.GetMouseButtonDown(0))
            {
                OnCloseTaskPopupClicked();
            }
            return;
        }

        if (isWaitingForChoice || isTransitioning) return;

        if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space))
        {
            if (isTimelineCutsceneActive)
            {
                if (isWaitingForTimelineClick)
                {
                    AdvanceTimelineAndDialogue();
                }
                return;
            }

            if (Input.GetMouseButtonDown(0))
            {
                if (DetectObjectClick()) return;
            }

            if (story.canContinue)
            {
                ContinueStory();
            }
        }
    }

    private void SetCameraControlsActive(bool isActive)
    {
        if (cameraControlScripts == null) return;
        foreach (var script in cameraControlScripts)
        {
            if (script != null && script.enabled != isActive)
            {
                script.enabled = isActive;
            }
        }
    }

    private void SetTimelineSystemOverride(bool overrideActive)
    {
        if (overrideActive)
        {
            disabledControllers.Clear();
            CharacterController[] controllers = FindObjectsOfType<CharacterController>();
            foreach (var cc in controllers)
            {
                if (cc != null && cc.enabled)
                {
                    cc.enabled = false;
                    disabledControllers.Add(cc);
                }
            }

            disabledPlayerControlScripts.Clear();

            originalRootMotionStates.Clear();
            Animator[] animators = FindObjectsOfType<Animator>();
            foreach (var anim in animators)
            {
                if (anim != null)
                {
                    originalRootMotionStates[anim] = anim.applyRootMotion;
                    anim.applyRootMotion = true;
                }
            }

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

                    if (typeName.Contains("CinemachineFreeLook") || typeName.Contains("CinemachineVirtualCamera"))
                    {
                        if (globalDirector != null && script.transform.root != globalDirector.transform.root)
                        {
                            script.enabled = false;
                            disabledCinemachineCameras.Add(script);
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

            foreach (var kvp in originalRootMotionStates)
            {
                if (kvp.Key != null)
                {
                    kvp.Key.applyRootMotion = kvp.Value;
                }
            }
            originalRootMotionStates.Clear();
        }
    }

    public void OnTimelinePauseSignalReached()
    {
        if (globalDirector != null)
        {
            globalDirector.Pause();
        }
        isWaitingForTimelineClick = true;
    }

    private void AdvanceTimelineAndDialogue()
    {
        isWaitingForTimelineClick = false;

        if (story.canContinue)
        {
            ContinueStory();
            if (globalDirector != null && globalDirector.state == PlayState.Paused)
            {
                globalDirector.Play();
            }
        }
        else
        {
            CheckAndEndCutscene();
        }
    }

    private void CheckAndEndCutscene()
    {
        if (!story.canContinue && story.currentChoices.Count == 0)
        {
            if (isTimelineCutsceneActive)
            {
                isTimelineCutsceneActive = false;
                SetTimelineSystemOverride(false);
            }

            if (globalDirector != null)
            {
                globalDirector.Stop();
            }
            if (dialogueText != null)
            {
                dialogueText.text = "";
            }
        }
    }

    private void ContinueStory()
    {
        string text = "";
        do
        {
            if (!story.canContinue) break;

            text = story.Continue().Trim();
            HandleTags(story.currentTags);

            if (isTransitioning) break;

        } while ((string.IsNullOrWhiteSpace(text) || text == ".") && story.canContinue);

        if (isTransitioning) return;

        if (!string.IsNullOrWhiteSpace(text))
        {
            if (text == ".") dialogueText.text = "";
            else dialogueText.text = text;

            currentTargetObjectName = "";
        }
        else
        {
            if (text == "" || text == ".") dialogueText.text = "";
        }

        if (!story.canContinue && story.currentChoices.Count > 0)
        {
            StartCoroutine(ToggleChoiceUI(true));
        }
        else if (!story.canContinue && story.currentChoices.Count == 0)
        {
            if (!isTimelineCutsceneActive)
            {
                CheckAndEndCutscene();
            }
        }
    }

    IEnumerator OnChoiceSelected(int index)
    {
        story.ChooseChoiceIndex(index);
        ClearChoices();
        yield return new WaitForSeconds(bufferTime);
        isWaitingForChoice = false;

        if (isWaitingForTimelineClick)
        {
            AdvanceTimelineAndDialogue();
        }
        else
        {
            ContinueStory();
        }
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

        yield return new WaitForSeconds(bufferTime);
        yield return Fade(1);

        yield return new WaitForSeconds(0.5f);
        yield return Fade(0);

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
                    ParseAndSetTask($"Task.text.{taskID}");
                    continue;
                }

                if (allSceneObjects.ContainsKey(id))
                {
                    allSceneObjects[id].gameObject.SetActive(active);
                    allSceneObjects[id].isInteractable = active;
                    if (!string.IsNullOrEmpty(knot)) allSceneObjects[id].targetKnot = knot;
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
        foreach (string tag in tags)
        {
            if (tag.Trim() == "fade")
            {
                StartCoroutine(ManualFadeSequence());
                return;
            }
        }

        foreach (string tag in tags)
        {
            string cleanTag = tag.Trim();
            if (cleanTag.Contains(".SetActive."))
            {
                ParseAndSetObjectActive(cleanTag);
            }
        }

        foreach (string tag in tags)
        {
            string cleanTag = tag.Trim();
            if (cleanTag.Contains(".prefab."))
            {
                ParseAndSwapPrefab(cleanTag);
            }
        }

        foreach (string tag in tags)
        {
            string cleanTag = tag.Trim();
            if (cleanTag.Contains(".prefab.")) continue;
            if (cleanTag.Contains(".SetActive.")) continue;

            if (cleanTag.StartsWith("Skybox."))
            {
                ParseAndSwapSkybox(cleanTag);
            }
            else if (cleanTag.StartsWith("LightingData_") || cleanTag.StartsWith("LightingData."))
            {
                ParseAndSwapLightingData(cleanTag);
            }
            else if (cleanTag.StartsWith("Emitter"))
            {
                ParseAndJumpToEmitter(cleanTag);
            }
            else if (cleanTag == "InteractableObject.non-interactable")
            {
                SetAllObjectsInteractable(false);
            }
            else if (cleanTag.StartsWith("Task.text."))
            {
                ParseAndSetTask(cleanTag);
            }
            else if (cleanTag.Contains(".material."))
            {
                ParseAndSwapMaterial(cleanTag);
            }
            else if (cleanTag.Contains(".timeline."))
            {
                ParseAndPlayTimeline(cleanTag);
            }
            else
            {
                ParseObjectAttributes(cleanTag);
            }
        }
    }

    void ParseAndSwapSkybox(string tag)
    {
        string imageName = tag.Replace("Skybox.", "").Trim();

        Texture2D newTex = Resources.Load<Texture2D>(skyboxResourcesPath + imageName);
        if (newTex == null)
        {
            newTex = Resources.Load<Texture2D>(imageName);
        }

        if (newTex != null)
        {
            Material targetMat = skyboxMaterial != null ? skyboxMaterial : RenderSettings.skybox;

            if (targetMat != null)
            {
                if (targetMat.HasProperty("_MainTex"))
                    targetMat.SetTexture("_MainTex", newTex);
                else if (targetMat.HasProperty("_Tex"))
                    targetMat.SetTexture("_Tex", newTex);

                RenderSettings.skybox = targetMat;
                DynamicGI.UpdateEnvironment();
            }
        }
        else
        {
            Debug.LogWarning($"[Skybox 警告] 找不到圖片資源！請確認圖片已放置於：Assets/Resources/{skyboxResourcesPath}{imageName}");
        }
    }

    /// <summary>
    /// 解析 Ink 標籤並比對預設好的 Preset 光照配置
    /// </summary>
    void ParseAndSwapLightingData(string tag)
    {
        string targetConfigName = tag.Trim();

        if (targetConfigName.StartsWith("LightingData."))
        {
            targetConfigName = targetConfigName.Replace("LightingData.", "LightingData_");
        }

        GameLightingPreset? selectedPreset = null;
        foreach (var preset in lightingPresets)
        {
            if (preset.configName.Equals(targetConfigName, System.StringComparison.OrdinalIgnoreCase))
            {
                selectedPreset = preset;
                break;
            }
        }

        if (selectedPreset.HasValue)
        {
            ApplyLightingConfig(selectedPreset.Value);
        }
        else
        {
            Debug.LogWarning($"[LightingPreset 警告] 找不到與 '{targetConfigName}' 對應的光照配置！");
        }
    }

    /// <summary>
    /// 在 Runtime 即時套用指定的太陽光、Area Light、環境光與 Skybox 屬性
    /// </summary>
    private void ApplyLightingConfig(GameLightingPreset config)
    {
        // 1. 調整主太陽光屬性 (顏色、強度、旋轉角度)
        if (mainDirectionalLight != null)
        {
            mainDirectionalLight.color = config.lightColor;
            mainDirectionalLight.intensity = config.lightIntensity;
            mainDirectionalLight.transform.eulerAngles = config.lightRotation;
        }

        //  1.5 調整 Area Light 屬性 (顏色與強度)
        if (mainAreaLight != null)
        {
            mainAreaLight.color = config.areaLightColor;
            mainAreaLight.intensity = config.areaLightIntensity;
        }

        // 2. 調整全域環境光
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = config.ambientLightColor;

        // 3. 切換 Skybox 貼圖
        if (!string.IsNullOrEmpty(config.skyboxImageName))
        {
            ParseAndSwapSkybox("Skybox." + config.skyboxImageName);
        }

        // 4. 強制刷新全域 GI 環境
        DynamicGI.UpdateEnvironment();
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

            if (!isTimelineCutsceneActive)
            {
                isTimelineCutsceneActive = true;
                SetTimelineSystemOverride(true);
            }

            globalDirector.Pause();
            globalDirector.time = targetTime;

            globalDirector.Evaluate();
            globalDirector.Play();

            isWaitingForTimelineClick = false;
        }
        else
        {
            Debug.LogWarning($"[Timeline 警告] 找不到第 {targetEmitterIndex} 個 Emitter！目前 Timeline 共有 {allEmitters.Count} 個 Emitter。");
        }
    }

    void ParseAndSetObjectActive(string tag)
    {
        string[] split = tag.Split(new string[] { ".SetActive." }, System.StringSplitOptions.None);
        if (split.Length < 2) return;

        string objName = split[0].Trim();
        bool targetState = (split[1].Trim().ToLower() == "true");

        GameObject targetGo = null;

        if (allSceneObjects.ContainsKey(objName) && allSceneObjects[objName] != null)
        {
            targetGo = allSceneObjects[objName].gameObject;
        }
        else
        {
            if (environmentHolder != null)
            {
                Transform foundTx = environmentHolder.Find(objName);
                if (foundTx != null) targetGo = foundTx.gameObject;
            }
            if (targetGo == null) targetGo = GameObject.Find(objName);
        }

        if (targetGo != null)
        {
            targetGo.SetActive(targetState);
            var io = targetGo.GetComponent<InteractableObject>();
            if (io != null) io.isInteractable = targetState;
        }
    }

    public void OnTaskTextClicked()
    {
        if (string.IsNullOrEmpty(currentTaskTitle) || isTaskPopupOpen) return;
        if (fadeGroup != null && fadeGroup.alpha > 0.9f) return;

        isTaskPopupOpen = true;

        if (fadeGroup != null)
        {
            fadeGroup.alpha = 0.65f;
            fadeGroup.blocksRaycasts = true;
        }

        if (taskText != null)
        {
            taskText.transform.SetAsLastSibling();
            var rect = taskText.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            taskText.fontSize = originalFontSize * 1.5f;
            taskText.color = Color.white;
            taskText.alignment = TextAlignmentOptions.Center;
            taskText.text = "<color=#000000><b>" + currentTaskTitle + "</b></color>\n\n<color=#FFFFFF><size=80%>" + currentTaskFullContent + "</size></color>";
        }
    }

    public void OnCloseTaskPopupClicked()
    {
        if (!isTaskPopupOpen) return;

        if (fadeGroup != null)
        {
            fadeGroup.alpha = 0f;
            fadeGroup.blocksRaycasts = false;
        }

        if (taskText != null)
        {
            var rect = taskText.rectTransform;
            rect.anchorMin = originalAnchorMin;
            rect.anchorMax = originalAnchorMax;
            rect.pivot = originalPivot;
            rect.anchoredPosition = originalAnchoredPosition;
            rect.localPosition = originalPosition;
            taskText.fontSize = originalFontSize;
            taskText.color = originalColor;
            taskText.alignment = TextAlignmentOptions.Left;
            taskText.text = "目前任務：" + currentTaskTitle;
        }
        StartCoroutine(ResetPopupFlag());
    }

    private IEnumerator ResetPopupFlag()
    {
        yield return null;
        isTaskPopupOpen = false;
    }

    void SetAllObjectsInteractable(bool state)
    {
        InteractableObject[] allObjects = FindObjectsOfType<InteractableObject>(true);
        foreach (var obj in allObjects)
        {
            obj.isInteractable = state;
        }
    }

    void ParseAndSetTask(string tag)
    {
        string taskID = tag.Replace("Task.text.", "").Trim();
        TextAsset taskCsv = Resources.Load<TextAsset>(taskCsvPath);
        if (taskCsv == null) return;

        string csvText = taskCsv.text.Replace("\r", "");
        string[] lines = csvText.Split('\n');

        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;
            string[] data = lines[i].Split(',');

            if (data[0].Trim() == taskID)
            {
                currentTaskTitle = taskID;
                currentTaskFullContent = data[1].Trim();

                if (taskText != null && !isTaskPopupOpen)
                {
                    taskText.text = "目前任務：" + currentTaskTitle;
                }
                break;
            }
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

        GameObject oldGo = null;

        if (allSceneObjects.ContainsKey(objName) && allSceneObjects[objName] != null)
        {
            oldGo = allSceneObjects[objName].gameObject;
            oldTargetKnot = allSceneObjects[objName].targetKnot;
        }
        else
        {
            Transform fallbackTx = environmentHolder.Find(objName);
            if (fallbackTx != null)
            {
                oldGo = fallbackTx.gameObject;
                var io = fallbackTx.GetComponent<InteractableObject>();
                if (io != null) oldTargetKnot = io.targetKnot;
            }
        }

        if (oldGo != null)
        {
            oldLocalPosition = oldGo.transform.localPosition;
            oldLocalRotation = oldGo.transform.localRotation;
            oldLocalScale = oldGo.transform.localScale;
            oldLayer = oldGo.layer;
            oldTag = oldGo.tag;
            oldParentNode = oldGo.transform.parent;

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

        GameObject targetGo = null;
        if (allSceneObjects.ContainsKey(targetObjectName) && allSceneObjects[targetObjectName] != null)
        {
            targetGo = allSceneObjects[targetObjectName].gameObject;
        }
        else
        {
            targetGo = GameObject.Find(targetObjectName);
        }

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
    }

    void ParseAndSwapMaterial(string tag)
    {
        string[] split = tag.Split(new string[] { ".material." }, System.StringSplitOptions.None);
        if (split.Length < 2) return;

        string objName = split[0].Trim();
        string materialName = split[1].Trim();

        GameObject targetGo = null;
        if (allSceneObjects.ContainsKey(objName)) targetGo = allSceneObjects[objName].gameObject;
        else targetGo = GameObject.Find(objName);

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
        if (globalDirector == null) return;
        string[] split = tag.Split(new string[] { ".timeline." }, System.StringSplitOptions.None);
        if (split.Length < 2) return;

        string timelineName = split[1].Trim();
        PlayableAsset timelineAsset = Resources.Load<PlayableAsset>("Timelines/" + timelineName);

        if (timelineAsset != null)
        {
            if (globalDirector.playableAsset == timelineAsset && isTimelineCutsceneActive)
            {
                return;
            }

            isTimelineCutsceneActive = true;
            isWaitingForTimelineClick = false;

            SetTimelineSystemOverride(true);

            globalDirector.Stop();
            globalDirector.playableAsset = timelineAsset;

            TimelineAsset timeline = timelineAsset as TimelineAsset;
            if (timeline != null)
            {
                foreach (var track in timeline.GetOutputTracks())
                {
                    AnimationTrack animTrack = track as AnimationTrack;
                    if (animTrack != null)
                    {
                        animTrack.trackOffset = TrackOffset.ApplyTransformOffsets;
                    }
                }
            }

            globalDirector.Play();
        }
        else
        {
            Debug.LogError($"[Timeline 錯誤] 找不到 Timeline 資源！請確認檔案是否存在於：Assets/Resources/Timelines/{timelineName}");
        }
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

    IEnumerator Fade(float targetAlpha)
    {
        fadeGroup.blocksRaycasts = (targetAlpha > 0.5f);
        float speed = Mathf.Abs(fadeGroup.alpha - targetAlpha) / fadeDuration;
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
        foreach (Choice choice in story.currentChoices)
        {
            GameObject btn = Instantiate(choiceButtonPrefab, choiceHolder);
            btn.GetComponentInChildren<TextMeshProUGUI>().text = choice.text;
            btn.GetComponent<Button>().onClick.AddListener(() => StartCoroutine(OnChoiceSelected(index: choice.index)));
        }
    }

    bool DetectObjectClick()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            var interactable = hit.collider.GetComponent<InteractableObject>();
            if (interactable && interactable.isInteractable)
            {
                story.ChoosePathString(interactable.targetKnot);
                ContinueStory();
                return true;
            }
        }
        return false;
    }

    void ClearChoices() { if (choiceHolder) foreach (Transform child in choiceHolder) Destroy(child.gameObject); }
}