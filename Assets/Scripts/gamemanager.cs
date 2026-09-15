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

    [Header("Task System Configuration")]
    public TextMeshProUGUI taskText;
    public string taskCsvPath = "TaskManager";

    [Header("Timeline Configuration")]
    public PlayableDirector globalDirector;

    [Header("Cinemachine Camera Configuration")]
    public MonoBehaviour cinemachineCamera;

    public MonoBehaviour firstPersonCamera => cinemachineCamera;
    public MonoBehaviour thirdPersonCamera => cinemachineCamera;
    public MonoBehaviour timelineCamera => cinemachineCamera;

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
    private bool isSwitchingTimeline = false;

    private List<CharacterController> disabledControllers = new List<CharacterController>();
    private List<MonoBehaviour> disabledCinemachineCameras = new List<MonoBehaviour>();
    private List<MonoBehaviour> disabledPlayerControlScripts = new List<MonoBehaviour>();

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
    }

    private void OnTimelineStopped(PlayableDirector director)
    {
        if (isSwitchingTimeline) return;

        string timelineName = (director != null && director.playableAsset != null) ? director.playableAsset.name : "Unknown";
        Debug.Log("[Timeline Debug] OnTimelineStopped triggered for: " + timelineName);

        if (isTimelineCutsceneActive)
        {
            isTimelineCutsceneActive = false;
            SetTimelineSystemOverride(false);

            if (!isWaitingForTimelineClick)
            {
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
                    else
                    {
                        CheckAndEndCutscene();
                    }
                }
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

        story.ObserveVariable("scene", (varName, newValue) => {
            StartCoroutine(SceneTransition((int)newValue));
        });

        if (!string.IsNullOrEmpty(startingKnot))
        {
            story.ChoosePathString(startingKnot);
        }

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
                if (IsPointerOverUIElement())
                {
                    Debug.Log("[GameManager Debug] Click ignored because mouse is over Canvas UI element.");
                    return;
                }

                if (DetectObjectClick()) return;
            }

            if (story != null && story.canContinue)
            {
                ContinueStory();
            }
            else if (story != null && story.currentChoices.Count > 0 && !isTimelineCutsceneActive)
            {
                StartCoroutine(ToggleChoiceUI(true));
            }
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
        if (taskText != null)
        {
            if (overrideActive)
            {
                taskText.gameObject.SetActive(false);
            }
            else if (!string.IsNullOrEmpty(currentTaskTitle))
            {
                taskText.gameObject.SetActive(true);
            }
        }

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

    public void OnTimelinePauseSignalReached()
    {
        if (globalDirector != null)
        {
            globalDirector.Pause();
        }
        isWaitingForTimelineClick = true;

        SyncCharacterTransform();

        if (story != null && story.canContinue && dialogueText != null && string.IsNullOrWhiteSpace(dialogueText.text))
        {
            ContinueStory();
        }
    }

    private void AdvanceTimelineAndDialogue()
    {
        Debug.Log("[Timeline Debug] Advancing Timeline and Dialogue from pause state.");
        isWaitingForTimelineClick = false;

        if (story != null && story.canContinue)
        {
            ContinueStory();
            if (globalDirector != null && globalDirector.state == PlayState.Paused)
            {
                globalDirector.Play();
            }
        }
        else if (story != null && story.currentChoices.Count > 0)
        {
            StartCoroutine(ToggleChoiceUI(true));
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
        if (story != null && !story.canContinue && story.currentChoices.Count == 0)
        {
            Debug.Log("[Timeline Debug] Cutscene conditions ended. Cleaning up Timeline state.");
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
        if (story == null) return;

        string text = "";
        bool foundText = false;

        while (story.canContinue)
        {
            text = story.Continue().Trim();
            HandleTags(story.currentTags);

            if (!string.IsNullOrWhiteSpace(text) && text != ".")
            {
                foundText = true;
            }

            if (isTransitioning || isTimelineCutsceneActive)
            {
                break;
            }

            if (foundText)
            {
                break;
            }
        }

        if (isTransitioning) return;

        if (foundText)
        {
            if (dialogueText != null)
            {
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

        if (!story.canContinue)
        {
            if (isTimelineCutsceneActive)
            {
                return;
            }

            if (story.currentChoices.Count > 0)
            {
                if (string.IsNullOrWhiteSpace(text) || text == ".")
                {
                    StartCoroutine(ToggleChoiceUI(true));
                }
            }
            else
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

            if (cleanTag.Equals("fade.Out", System.StringComparison.OrdinalIgnoreCase))
            {
                pendingFade = FadeType.Out;
                continue;
            }
            else if (cleanTag.Equals("fade.In", System.StringComparison.OrdinalIgnoreCase))
            {
                pendingFade = FadeType.In;
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
                        SwitchBakedLight.Instance.ApplyLightmapPreset(cleanTag);
                    }
                    else
                    {
                        Debug.LogWarning("[GameManager Warning] SwitchBakedLight instance not found.");
                    }
                    break;
                case string s when s.StartsWith("Emitter", System.StringComparison.OrdinalIgnoreCase):
                    ParseAndJumpToEmitter(cleanTag);
                    break;
                case string s when s.StartsWith("Task.text.", System.StringComparison.OrdinalIgnoreCase):
                    ParseAndSetTask(cleanTag);
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
                StartCoroutine(FadeOutSequence());
                break;
            case FadeType.In:
                StartCoroutine(FadeInSequence());
                break;
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
            if (io != null) io.isInteractable = targetState;
        }
        else
        {
            Debug.LogWarning("[GameManager Warning] Target object '" + objName + "' not found for SetActive.");
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
            taskText.text = "Task: " + currentTaskTitle;
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

                if (taskText != null)
                {
                    taskText.gameObject.SetActive(true);
                    if (!isTaskPopupOpen)
                    {
                        taskText.text = "Task: " + currentTaskTitle;
                    }
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
            Debug.LogError("[Timeline Error] globalDirector is null!");
            return;
        }

        string[] split = tag.Split(new string[] { ".timeline." }, System.StringSplitOptions.None);
        if (split.Length < 2) return;

        string timelineName = split[1].Trim();
        PlayableAsset timelineAsset = Resources.Load<PlayableAsset>("Timelines/" + timelineName);

        if (timelineAsset != null)
        {
            if (globalDirector.playableAsset == timelineAsset && isTimelineCutsceneActive)
            {
                Debug.Log("[Timeline Debug] Already playing Timeline: " + timelineName + ", skipping restart.");
                return;
            }

            Debug.Log("[Timeline Debug] Starting Timeline: " + timelineName);

            isTimelineCutsceneActive = true;
            isWaitingForTimelineClick = false;

            SetTimelineSystemOverride(true);

            isSwitchingTimeline = true;
            globalDirector.Stop();
            isSwitchingTimeline = false;

            globalDirector.playableAsset = timelineAsset;

            globalDirector.Play();
        }
        else
        {
            Debug.LogError("[Timeline Error] Cannot find asset at: Resources/Timelines/" + timelineName);
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

    private enum FadeType
    {
        None,
        Full,
        Out,
        In
    }

    IEnumerator FadeOutSequence()
    {
        isWaitingForChoice = true;
        isTransitioning = true;

        yield return new WaitForSeconds(bufferTime);
        yield return Fade(1f);

        isTransitioning = false;
        isWaitingForChoice = false;

        ContinueStory();
    }

    IEnumerator FadeInSequence()
    {
        isWaitingForChoice = true;
        isTransitioning = true;

        yield return Fade(0f);
        yield return new WaitForSeconds(bufferTime);

        isTransitioning = false;
        isWaitingForChoice = false;

        ContinueStory();
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
                if (!interactable.enabled || !interactable.isInteractable)
                {
                    Debug.LogWarning("[GameManager Debug] Hit [" + interactable.name + "], but component is disabled or isInteractable is FALSE.");
                    return false;
                }

                if (string.IsNullOrEmpty(interactable.targetKnot))
                {
                    Debug.LogWarning("[GameManager Debug] Hit [" + interactable.name + "], but targetKnot is EMPTY.");
                    return false;
                }

                Debug.Log("[GameManager Debug] Interacted with: " + interactable.name + ", jumping to knot: " + interactable.targetKnot);
                story.ChoosePathString(interactable.targetKnot);
                ContinueStory();
                return true;
            }
            else
            {
                Debug.Log("[GameManager Debug] Hit [" + hit.collider.name + "], but no InteractableObject component found.");
            }
        }
        else
        {
            Debug.Log("[GameManager Debug] Raycast missed all 3D colliders.");
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
    void ClearChoices() { if (choiceHolder) foreach (Transform child in choiceHolder) Destroy(child.gameObject); }
}