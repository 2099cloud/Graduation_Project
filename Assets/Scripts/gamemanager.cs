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
    [Tooltip("拖入畫面上負責顯示目前任務的 TMP 組件")]
    public TextMeshProUGUI taskText;
    [Tooltip("任務 CSV 配置文件(放於 Resources 正下方，不含副檔名)")]
    public string taskCsvPath = "TaskManager";

    [Header("Timeline 配置")]
    [Tooltip("場景中負責播放動畫的 Playable Director 組件")]
    public PlayableDirector globalDirector;

    [Header("數據與過渡配置")]
    [Tooltip("全域 CSV 配置文件(放於 Resources 正下方，不含副檔名)")]
    public string csvResourcePath = "SceneConfig";
    public CanvasGroup fadeGroup;
    public float fadeDuration = 0.5f;
    public float bufferTime = 0.5f;

    [Header("動態生成生成點")]
    [Tooltip("新物件生成時的父節點")]
    public Transform environmentHolder;

    private bool isWaitingForChoice = false;
    private Dictionary<string, InteractableObject> allSceneObjects = new Dictionary<string, InteractableObject>();
    private int currentSceneID = 0;

    private string currentTargetObjectName = "";

    // 紀錄任務原始狀態與彈出狀態的變數
    private string currentTaskTitle = "";
    private string currentTaskFullContent = "";
    private bool isTaskPopupOpen = false;

    // 過渡狀態鎖與標籤暫存器
    private bool isTransitioning = false;
    private List<string> deferredTags = new List<string>();

    // 用於記錄 taskText 還原用的原始變數
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

        if (isWaitingForChoice || isTransitioning) return; // 換幕時不允許點擊互動
        if (Input.GetMouseButtonDown(0))
        {
            if (DetectObjectClick()) return;
            if (story.canContinue) ContinueStory();
        }
    }

    // --- 故事核心邏輯 ---

    private void ContinueStory()
    {
        string text = "";

        // 核心改動：使用 do-while 機制，如果當前行沒有實質文字，且故事還能繼續，就自動往下讀取，不卡滑鼠點擊
        do
        {
            if (!story.canContinue) break;

            text = story.Continue().Trim();
            HandleTags(story.currentTags);

        } while ((string.IsNullOrWhiteSpace(text) || text == ".") && story.canContinue);

        // 處理最終要呈現的文字
        if (!string.IsNullOrWhiteSpace(text))
        {
            if (text == ".") dialogueText.text = "";
            else dialogueText.text = text;

            currentTargetObjectName = "";
        }
        else
        {
            // 如果跳出迴圈後 text 依然是空的或點號（通常是到了故事盡頭），則清空文字
            if (text == "" || text == ".") dialogueText.text = "";
        }

        // 檢查是否到了需要顯示選項的階段
        if (!story.canContinue && story.currentChoices.Count > 0)
            StartCoroutine(ToggleChoiceUI(true));
    }

    IEnumerator OnChoiceSelected(int index)
    {
        story.ChooseChoiceIndex(index);
        ClearChoices();
        yield return new WaitForSeconds(bufferTime);
        isWaitingForChoice = false;
        ContinueStory();
    }

    // --- 場景更新邏輯 ---

    IEnumerator SceneTransition(int nextScene)
    {
        isWaitingForChoice = true;
        isTransitioning = true; // 鎖定狀態，開始過渡
        deferredTags.Clear();   // 清空舊的延遲標籤緩存

        yield return new WaitForSeconds(bufferTime);
        yield return Fade(1); // 直到畫面完全變黑

        // 黑幕已蓋滿，這時候更新環境與生成 Prefab
        currentSceneID = nextScene;
        UpdateEnvironment(nextScene);

        // 在全黑狀態下，補跑剛才被攔截下來的 Ink 標籤
        if (deferredTags.Count > 0)
        {
            ExecuteTagsReal(deferredTags);
            deferredTags.Clear();
        }

        SetAllObjectsInteractable(true);

        yield return new WaitForSeconds(0.5f);
        yield return Fade(0); // 淡出黑幕

        isTransitioning = false; // 解除鎖定
        isWaitingForChoice = false;

        ContinueStory();
    }

    void UpdateEnvironment(int sceneID)
    {
        TextAsset sceneConfigCsv = Resources.Load<TextAsset>(csvResourcePath);

        if (sceneConfigCsv == null)
        {
            Debug.LogError("[GameManager] 在 Resources 下找不到全域 CSV 檔案！");
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
                    Debug.Log("場景 " + sceneID + ": 已更新靜態物件 " + id + " 狀態為 " + active);
                }
            }
        }
    }

    // --- 靈星物件動態修改處理器 (Ink 標籤系統) ---

    void HandleTags(List<string> tags)
    {
        if (tags == null || tags.Count == 0) return;

        // 如果目前處於變黑的過渡期，先把標籤存起來，稍後再執行
        if (isTransitioning)
        {
            foreach (var tag in tags)
            {
                deferredTags.Add(tag);
            }
            return;
        }

        // 如果不是在換幕期間，維持原樣直接執行
        ExecuteTagsReal(tags);
    }

    // 將原本 HandleTags 的核心邏輯抽離出來，方便在全黑時呼叫
    void ExecuteTagsReal(List<string> tags)
    {
        // 優先處理所有具有生成性質的 Prefab 標籤
        foreach (string tag in tags)
        {
            string cleanTag = tag.Trim();
            if (cleanTag.Contains(".prefab."))
            {
                ParseAndSwapPrefab(cleanTag);
            }
        }

        // 處理其他屬性控制標籤
        foreach (string tag in tags)
        {
            string cleanTag = tag.Trim();
            if (cleanTag.Contains(".prefab.")) continue;

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

    // --- 任務介面核心切換邏輯 ---

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

    // --- 其餘擴充功能 ---

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

        if (allSceneObjects.ContainsKey(objName))
        {
            Destroy(allSceneObjects[objName].gameObject);
            allSceneObjects.Remove(objName);
        }

        string loadPath = "Prefabs/" + objName + "/" + prefabName;
        GameObject newPrefab = Resources.Load<GameObject>(loadPath);

        if (newPrefab != null)
        {
            GameObject spawnedInstance = Instantiate(newPrefab, environmentHolder);
            spawnedInstance.name = objName;
            RefreshSceneObjectsDictionary();
        }
    }

    void ParseObjectAttributes(string tag)
    {
        string targetObjectName = "";
        string commandContent = "";

        // 檢查指令是否包含物件名稱前綴 (例如 phone.position)
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
            // 回退機制：若不含點號前綴，則使用上一次記錄的 currentTargetObjectName
            targetObjectName = currentTargetObjectName;
            commandContent = tag;
        }

        // 安全檢查：若找不對對應的場景物件則直接跳出
        if (string.IsNullOrEmpty(targetObjectName) || !allSceneObjects.ContainsKey(targetObjectName)) return;

        GameObject targetGo = allSceneObjects[targetObjectName].gameObject;
        InteractableObject targetInteract = allSceneObjects[targetObjectName];

        if (commandContent.StartsWith("position"))
        {
            targetGo.transform.localPosition = ExtractVector3(commandContent);
        }
        else if (commandContent.StartsWith("rotation"))
        {
            targetGo.transform.localEulerAngles = ExtractVector3(commandContent);
        }
        else if (commandContent.StartsWith("TargetKnot"))
        {
            string[] parts = commandContent.Split('=');
            if (parts.Length >= 2)
            {
                targetInteract.targetKnot = parts[1].Replace("\"", "").Trim();
            }
        }
    }

    void ParseAndSwapMaterial(string tag)
    {
        string[] split = tag.Split(new string[] { ".material." }, System.StringSplitOptions.None);
        if (split.Length < 2) return;

        string objName = split[0].Trim();
        string materialName = split[1].Trim();

        if (allSceneObjects.ContainsKey(objName))
        {
            GameObject targetGo = allSceneObjects[objName].gameObject;
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
        catch { return Vector3.zero; }
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