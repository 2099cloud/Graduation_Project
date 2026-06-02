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

    // ====================================================================
    // 新增：處理單純由 #fade 標籤觸過渡的淡入淡出協程
    // ====================================================================
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
        // 新增：優先攔截並檢查是否有單純的 fade 標籤指令
        foreach (string tag in tags)
        {
            if (tag.Trim() == "fade")
            {
                StartCoroutine(ManualFadeSequence());
                return; // 直接進入淡入淡出協程流程，跳出當前執行
            }
        }

        // ====================================================================
        // 新增：攔截並處理物件 Active 狀態控制標籤
        // 格式：(物件名稱).SetActive.true 或 (物件名稱).SetActive.false
        // ====================================================================
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
            if (cleanTag.Contains(".SetActive.")) continue; // 避免與後續的屬性解析衝突

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

    // ====================================================================
    // 新增：解析 Ink 傳入的 SetActive 指令並尋找物件進行控制
    // ====================================================================
    void ParseAndSetObjectActive(string tag)
    {
        string[] split = tag.Split(new string[] { ".SetActive." }, System.StringSplitOptions.None);
        if (split.Length < 2) return;

        string objName = split[0].Trim();
        string stateStr = split[1].Trim().ToLower();
        bool targetState = (stateStr == "true");

        GameObject targetGo = null;

        // 優先從現有的場景互動字典中尋找物件
        if (allSceneObjects.ContainsKey(objName) && allSceneObjects[objName] != null)
        {
            targetGo = allSceneObjects[objName].gameObject;
        }
        else
        {
            // 如果字典裡找不到（可能是非互動或目前處於隱藏狀態的物件），嘗試在環境節點下尋找
            if (environmentHolder != null)
            {
                Transform foundTx = environmentHolder.Find(objName);
                if (foundTx != null) targetGo = foundTx.gameObject;
            }

            // 最終防線：若還是找不到，嘗試全域搜尋（注意：GameObject.Find 找不到未激活的物件）
            if (targetGo == null)
            {
                targetGo = GameObject.Find(objName);
            }
        }

        if (targetGo != null)
        {
            targetGo.SetActive(targetState);

            // 如果物件本身包含 InteractableObject 元件，同步切換它的可互動狀態
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

        if (taskText != null && fadeGroup != null)
        {
            taskText.transform.SetParent(fadeGroup.transform, true);

            RectTransform rect = taskText.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;

            taskText.fontSize = originalFontSize * 1.5f;
            taskText.color = Color.white;
            taskText.alignment = TextAlignmentOptions.Center;

            taskText.text = "<color=#FFCC00><b>" + currentTaskTitle + "</b></color>\n\n<size=80%>" + currentTaskFullContent + "</size>";
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
            taskText.transform.SetParent(originalParent, true);

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