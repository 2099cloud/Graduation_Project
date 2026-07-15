using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Ink.Runtime;
using TMPro;
using UnityEngine.Playables;

public class GameManager : MonoBehaviour
{
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

    [Header("數據與過渡配置")]
    public string csvResourcePath = "SceneConfig";
    public CanvasGroup fadeGroup;
    public float fadeDuration = 0.5f;
    public float bufferTime = 0.5f;

    [Header("動態生成生成點")]
    public Transform environmentHolder;

    // ====================================================================
    // 新增：相機暫停與控制配置 (支援自訂相機腳本)
    // ====================================================================
    [Header("相機暫停配置")]
    [Tooltip("如果你的相機有掛載自訂的移動/旋轉控制腳本，請拖入此欄位。若留空，系統會自動搜尋主相機上的所有行為組件。")]
    public MonoBehaviour[] cameraControlScripts;
    // ====================================================================

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

    void Awake()
    {
        RefreshSceneObjectsDictionary();

        if (taskText != null)
        {
            RectTransform rect = taskText.rectTransform;
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

        // ====================================================================
        // 新增：自動初始化相機腳本尋找
        // ====================================================================
        if (cameraControlScripts == null || cameraControlScripts.Length == 0)
        {
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                // 自動抓取主相機上「非 Unity 內建」的所有自訂控制腳本
                List<MonoBehaviour> foundScripts = new List<MonoBehaviour>();
                foreach (var script in mainCam.GetComponents<MonoBehaviour>())
                {
                    // 排除 AudioListener、Camera 等內建非自訂控制組件
                    if (script != null && script.GetType().Namespace != "UnityEngine")
                    {
                        foundScripts.Add(script);
                    }
                }
                cameraControlScripts = foundScripts.ToArray();
            }
        }
        // ====================================================================
    }

    void Start()
    {
        if (!inkJsonAsset) return;
        story = new Story(inkJsonAsset.text);

        story.ObserveVariable("scene", (varName, newValue) => {
            StartCoroutine(SceneTransition((int)newValue));
        });

        ContinueStory();
    }

    void Update()
    {
        // ====================================================================
        // 新增：即時監測 fadeGroup 顯示狀態，並自動鎖定/解鎖相機
        // ====================================================================
        bool shouldDisableCamera = false;

        if (fadeGroup != null)
        {
            // 只要 fadeGroup 的不透明度大於 0 (正在顯示或正在過渡)，就判定需要鎖定相機
            shouldDisableCamera = fadeGroup.alpha > 0.01f;
        }

        // 根據狀態開關相機控制腳本
        SetCameraControlsActive(!shouldDisableCamera);
        // ====================================================================

        if (isTaskPopupOpen)
        {
            if (Input.GetMouseButtonDown(0))
            {
                OnCloseTaskPopupClicked();
            }
            return;
        }

        if (isWaitingForChoice || isTransitioning) return;
        if (Input.GetMouseButtonDown(0))
        {
            if (DetectObjectClick()) return;
            if (story.canContinue) ContinueStory();
        }
    }

    // ====================================================================
    // 新增：統一開關相機控制腳本的輔助函式
    // ====================================================================
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
    // ====================================================================

    private void ContinueStory()
    {
        string text = "";

        do
        {
            if (!story.canContinue) break;

            text = story.Continue().Trim();
            HandleTags(story.currentTags);

            // 如果被標籤截斷且需要淡出過渡，則終止 do-while 確保文字不會在全黑前直接跳出
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
            if (dialogueText != null)
            {
                dialogueText.text = "";
            }
        }
    }

    IEnumerator OnChoiceSelected(int index)
    {
        story.ChooseChoiceIndex(index);
        ClearChoices();
        yield return new WaitForSeconds(bufferTime);
        isWaitingForChoice = false;
        ContinueStory();
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
        yield return Fade(1); // 1. 先淡入至全黑

        yield return new WaitForSeconds(0.5f); // 全黑緩衝時間
        yield return Fade(0); // 2. 再淡出恢復畫面

        isTransitioning = false;
        isWaitingForChoice = false;

        ContinueStory(); // 3. 恢復正常機制，秀出過渡後的後續台詞內容
    }

    void UpdateEnvironment(int sceneID)
    {
        TextAsset sceneConfigCsv = Resources.Load<TextAsset>(csvResourcePath);

        if (sceneConfigCsv == null)
        {
            Debug.LogError("[GameManager] SceneConfig CSV Null!");
            return;
        }

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

            if (cleanTag == "InteractableObject.non-interactable")
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

    void ParseAndSetObjectActive(string tag)
    {
        string[] split = tag.Split(new string[] { ".SetActive." }, System.StringSplitOptions.None);
        if (split.Length < 2) return;

        string objName = split[0].Trim();
        string stateStr = split[1].Trim().ToLower();
        bool targetState = (stateStr == "true");

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

            if (targetGo == null)
            {
                targetGo = GameObject.Find(objName);
            }
        }

        if (targetGo != null)
        {
            targetGo.SetActive(targetState);

            InteractableObject io = targetGo.GetComponent<InteractableObject>();
            if (io != null)
            {
                io.isInteractable = targetState;
            }
        }
        else
        {
            Debug.LogWarning($"[GameManager] SetActive 找不到目標物件: {objName}");
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

            RectTransform rect = taskText.rectTransform;
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
            RectTransform rect = taskText.rectTransform;
            rect.anchorMin = originalAnchorMin;
            rect.anchorMax = originalAnchorMax;
            rect.pivot = originalPivot;
            rect.anchoredPosition = originalAnchoredPosition;
            rect.localPosition = originalPosition;

            taskText.fontSize = originalFontSize;
            taskText.color = originalColor;
            taskText.alignment = TextAlignmentOptions.Left;

            taskText.text = "目前任務：" + currentTaskTitle;

            Button btn = taskText.gameObject.GetComponent<Button>();
            if (btn == null) btn = taskText.gameObject.AddComponent<Button>();
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(OnTaskTextClicked);
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

            InteractableObject ioComponent = spawnedInstance.GetComponent<InteractableObject>();
            if (ioComponent == null)
            {
                ioComponent = spawnedInstance.AddComponent<InteractableObject>();
            }

            if (!string.IsNullOrEmpty(oldTargetKnot))
            {
                ioComponent.targetKnot = oldTargetKnot;
            }

            RefreshSceneObjectsDictionary();
        }
        else
        {
            Debug.LogError($"[GameManager] Prefab Path Error: Resources/{loadPath}");
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

        if (targetGo == null)
        {
            Debug.LogWarning($"[GameManager] Cannot find target object named: {targetObjectName} in scene.");
            return;
        }

        if (commandContent.StartsWith("position"))
        {
            Vector3 targetPosition = ExtractVector3(commandContent);
            CharacterController cc = targetGo.GetComponent<CharacterController>();

            if (cc != null)
            {
                cc.enabled = false;
                targetGo.transform.position = targetPosition;
                cc.enabled = true;
            }
            else
            {
                targetGo.transform.position = targetPosition;
            }
        }
        else if (commandContent.StartsWith("rotation"))
        {
            Vector3 targetRotation = ExtractVector3(commandContent);
            CharacterController cc = targetGo.GetComponent<CharacterController>();

            if (cc != null)
            {
                cc.enabled = false;
                targetGo.transform.eulerAngles = targetRotation;
                cc.enabled = true;
            }
            else
            {
                targetGo.transform.eulerAngles = targetRotation;
            }
        }
        else if (commandContent.StartsWith("TargetKnot"))
        {
            InteractableObject targetInteract = targetGo.GetComponent<InteractableObject>();
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
                MeshRenderer renderer = targetGo.GetComponentInChildren<MeshRenderer>();
                if (renderer != null) renderer.material = newMat;
            }
        }
    }

    void ParseAndPlayTimeline(string tag)
    {
        if (globalDirector == null) return;
        string[] split = tag.Split(new string[] { ".timeline." }, System.StringSplitOptions.None);
        if (split.Length < 2) return;

        PlayableAsset timelineAsset = Resources.Load<PlayableAsset>("Prefabs/" + split[0].Trim() + "/Timelines/" + split[1].Trim());
        if (timelineAsset != null)
        {
            globalDirector.Stop();
            globalDirector.playableAsset = timelineAsset;
            globalDirector.Play();
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
            btn.GetComponent<Button>().onClick.AddListener(() => StartCoroutine(OnChoiceSelected(choice.index)));
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