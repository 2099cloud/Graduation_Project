using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Ink.Runtime;
using TMPro;
using UnityEngine.Playables; // 【新增】Timeline 控制必備命名空間

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
    public string taskCsvPath = "TaskManager"; // 已改為大寫 TaskManager

    [Header("Timeline 配置")]
    [Tooltip("場景中負責播放動畫的 PlayableDirector 組件")]
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

    // 追蹤當前對話中，標籤正在操作的目標物件名稱
    private string currentTargetObjectName = "";

    void Awake()
    {
        RefreshSceneObjectsDictionary();
    }

    void Start()
    {
        if (!inkJsonAsset) return;
        story = new Story(inkJsonAsset.text);

        // 監聽 Ink 變數 scene
        story.ObserveVariable("scene", (varName, newValue) => {
            StartCoroutine(SceneTransition((int)newValue));
        });

        ContinueStory();
    }

    void Update()
    {
        if (isWaitingForChoice) return;
        if (Input.GetMouseButtonDown(0))
        {
            if (DetectObjectClick()) return;
            if (story.canContinue) ContinueStory();
        }
    }

    // --- 故事核心邏輯 ---

    private void ContinueStory()
    {
        if (!story.canContinue) return;
        string text = "";
        while (story.canContinue && string.IsNullOrWhiteSpace(text))
            text = story.Continue().Trim();

        if (!string.IsNullOrWhiteSpace(text))
        {
            // 【關鍵防呆】：如果發現台詞只是我們用來帶標籤的點「.」，就讓它變空字串
            if (text == ".")
            {
                dialogueText.text = "";
            }
            else
            {
                dialogueText.text = text; // 正常的台詞照常顯示
            }

            currentTargetObjectName = "";
            HandleTags(story.currentTags);
        }

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

    // --- 場景更新邏輯 (換幕統一讀一份表) ---

    IEnumerator SceneTransition(int nextScene)
    {
        isWaitingForChoice = true;
        yield return new WaitForSeconds(bufferTime);
        yield return Fade(1);

        currentSceneID = nextScene;
        UpdateEnvironment(nextScene);

        // 換幕時自動解除全場景的互動鎖定，確保新幕能正常互動
        SetAllObjectsInteractable(true);

        yield return new WaitForSeconds(0.5f);
        yield return Fade(0);
        isWaitingForChoice = false;
        ContinueStory();
    }

    void UpdateEnvironment(int sceneID)
    {
        // 統一讀取位於 Resources/ 下的單一表單
        TextAsset sceneConfigCsv = Resources.Load<TextAsset>(csvResourcePath);

        if (sceneConfigCsv == null)
        {
            Debug.LogError($"[GameManager] 在 Resources 下找不到名為 '{csvResourcePath}' 的全域 CSV 檔案！");
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

                if (allSceneObjects.ContainsKey(id))
                {
                    allSceneObjects[id].gameObject.SetActive(active);
                    allSceneObjects[id].isInteractable = active;
                    if (!string.IsNullOrEmpty(knot)) allSceneObjects[id].targetKnot = knot;

                    Debug.Log($"場景 {sceneID}: 已更新靜態物件 {id} 狀態為 {active}");
                }
            }
        }
    }

    // --- 靈星物件動態修改處理器 (Ink 標籤系統) ---

    void HandleTags(List<string> tags)
    {
        // 防呆檢查 1：看看這句台詞到底有沒有帶任何標籤過來
        Debug.Log($"[HandleTags 觸發] 當前台詞偵測到 {tags.Count} 個標籤。");

        // 第一輪：先找出是否有置換 Prefab 的主要指令 (例如: phone.prefab.incomingcall)
        foreach (string tag in tags)
        {
            string cleanTag = tag.Trim();
            Debug.Log($"[收到標籤] 內容為: {cleanTag}"); // 防呆檢查 2：把每個收到的標籤印出來
            if (cleanTag.Contains(".prefab."))
            {
                ParseAndSwapPrefab(cleanTag);
                break; // 找到主導物件即可
            }
        }

        // 第二輪：處理屬性、材質球、鎖定互動、任務與 Timeline
        foreach (string tag in tags)
        {
            string cleanTag = tag.Trim();
            if (cleanTag.Contains(".prefab.")) continue; // 跳過已處理的主指令

            // 1. 全場景互動鎖定：InteractableObject.non-interactable
            if (cleanTag == "InteractableObject.non-interactable")
            {
                SetAllObjectsInteractable(false);
            }
            // 2. 任務更換：Task.text.應門
            else if (cleanTag.StartsWith("Task.text."))
            {
                ParseAndSetTask(cleanTag);
            }
            // 3. 材質球切換：phone.material.M_Phone_Calling
            else if (cleanTag.Contains(".material."))
            {
                ParseAndSwapMaterial(cleanTag);
            }
            // 4. Timeline 動態播放：phone.timeline.incoming_ring
            else if (cleanTag.Contains(".timeline."))
            {
                ParseAndPlayTimeline(cleanTag);
            }
            // 5. 其餘基礎屬性設定 (position, rotation, TargetKnot)
            else
            {
                ParseObjectAttributes(cleanTag);
            }
        }
    }

    // 解析並換掉 Prefab。格式範例: phone.prefab.incomingcall
    void ParseAndSwapPrefab(string tag)
    {
        string[] split = tag.Split(new string[] { ".prefab." }, System.StringSplitOptions.None);
        if (split.Length < 2) return;

        string objName = split[0].Trim();       // "phone"
        string prefabName = split[1].Trim();    // "incomingcall"

        currentTargetObjectName = objName; // 綁定當前要操作的物件

        // 1. 如果場景中已經有舊的同名物件，先把它移除
        if (allSceneObjects.ContainsKey(objName))
        {
            Destroy(allSceneObjects[objName].gameObject);
            allSceneObjects.Remove(objName);
        }

        // 2. 從動態物件路徑載入新 Prefab，路徑：Prefabs/phone/incomingcall
        string loadPath = $"Prefabs/{objName}/{prefabName}";
        GameObject newPrefab = Resources.Load<GameObject>(loadPath);

        if (newPrefab != null)
        {
            GameObject spawnedInstance = Instantiate(newPrefab, environmentHolder);
            spawnedInstance.name = objName; // 強制將生成的物件名稱洗回 "phone"，維持識別一致性

            RefreshSceneObjectsDictionary();
            Debug.Log($"[動態置換] 成功載入 {loadPath} 並替換舊有物件。");
        }
        else
        {
            Debug.LogError($"[動態置換] 找不到 Prefab 路徑: Resources/{loadPath}");
        }
    }

    // 解析其餘屬性標籤。格式範例: position(0,1,0), rotation(0,90,0), TargetKnot="KnotName"
    void ParseObjectAttributes(string tag)
    {
        if (string.IsNullOrEmpty(currentTargetObjectName) || !allSceneObjects.ContainsKey(currentTargetObjectName)) return;

        GameObject targetGo = allSceneObjects[currentTargetObjectName].gameObject;
        InteractableObject targetInteract = allSceneObjects[currentTargetObjectName];

        // 處理位置設定: position(x,y,z)
        if (tag.StartsWith("position"))
        {
            Vector3 pos = ExtractVector3(tag);
            targetGo.transform.localPosition = pos;
            Debug.Log($"[屬性變更] {currentTargetObjectName} 位置修改為: {pos}");
        }
        // 處理角度設定: rotation(x,y,z)
        else if (tag.StartsWith("rotation"))
        {
            Vector3 rot = ExtractVector3(tag);
            targetGo.transform.localEulerAngles = rot;
            Debug.Log($"[屬性變更] {currentTargetObjectName} 角度修改為: {rot}");
        }
        // 處理跳轉節點: TargetKnot="KnotName" 
        else if (tag.StartsWith("TargetKnot"))
        {
            string[] parts = tag.Split('=');
            if (parts.Length >= 2)
            {
                string knot = parts[1].Replace("\"", "").Trim(); // 去除雙引號
                targetInteract.targetKnot = knot;
                Debug.Log($"[屬性變更] {currentTargetObjectName} 互動節點修改為: {knot}");
            }
        }
    }

    // --- 擴充功能核心邏輯 ---

    // 1. 全場景互動控制
    void SetAllObjectsInteractable(bool state)
    {
        InteractableObject[] allObjects = FindObjectsOfType<InteractableObject>(true);
        foreach (var obj in allObjects)
        {
            obj.isInteractable = state;
        }
        Debug.Log($"[系統控制] 已將全場景 {allObjects.Length} 個物件的互動狀態設為: {state}");
    }

    // 2. 任務系統：讀取 TaskManager.csv 並更換 TMP 文字
    void ParseAndSetTask(string tag)
    {
        string taskID = tag.Replace("Task.text.", "").Trim();

        TextAsset taskCsv = Resources.Load<TextAsset>(taskCsvPath);
        if (taskCsv == null)
        {
            Debug.LogError($"[任務系統] 找不到 Resources/{taskCsvPath}.csv 檔案！");
            return;
        }

        string csvText = taskCsv.text.Replace("\r", "");
        string[] lines = csvText.Split('\n');
        bool foundTask = false;

        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;
            string[] data = lines[i].Split(',');

            if (data[0].Trim() == taskID)
            {
                string taskContent = data[1].Trim();
                if (taskText != null)
                {
                    taskText.text = $"目前任務：{taskContent}";
                }
                Debug.Log($"[任務系統] 任務更新成功 -> 【{taskID}】: {taskContent}");
                foundTask = true;
                break;
            }
        }

        if (!foundTask)
        {
            Debug.LogWarning($"[任務系統] 在 CSV 中找不到名為 '{taskID}' 的任務項目。");
        }
    }

    // 3. 材質球動態切換處理器。格式範例: phone.material.M_Phone_Calling
    void ParseAndSwapMaterial(string tag)
    {
        string[] split = tag.Split(new string[] { ".material." }, System.StringSplitOptions.None);
        if (split.Length < 2) return;

        string objName = split[0].Trim();       // "phone"
        string materialName = split[1].Trim();  // "M_Phone_Calling"

        if (allSceneObjects.ContainsKey(objName))
        {
            GameObject targetGo = allSceneObjects[objName].gameObject;
            string matPath = $"Prefabs/{objName}/Materials/{materialName}";
            Material newMat = Resources.Load<Material>(matPath);

            if (newMat != null)
            {
                MeshRenderer renderer = targetGo.GetComponentInChildren<MeshRenderer>();
                if (renderer != null)
                {
                    renderer.material = newMat;
                    Debug.Log($"[動態材質] 成功將 {objName} 的材質更換為: {materialName}");
                }
                else
                {
                    Debug.LogWarning($"[動態材質] 找不到 {objName} 身上的 MeshRenderer 組件。");
                }
            }
            else
            {
                Debug.LogError($"[動態材質] 找不到材質球，請檢查路徑是否為: Resources/{matPath}");
            }
        }
    }

    // 4. Timeline 動態播放處理器。格式範例: phone.timeline.incoming_ring
    void ParseAndPlayTimeline(string tag)
    {
        if (globalDirector == null)
        {
            Debug.LogError("[Timeline 系統] 場景中未綁定 PlayableDirector，無法播放動畫！");
            return;
        }

        string[] split = tag.Split(new string[] { ".timeline." }, System.StringSplitOptions.None);
        if (split.Length < 2) return;

        string objName = split[0].Trim();      // "phone"
        string timelineName = split[1].Trim(); // "incoming_ring"

        string loadPath = $"Prefabs/{objName}/Timelines/{timelineName}";
        PlayableAsset timelineAsset = Resources.Load<PlayableAsset>(loadPath);

        if (timelineAsset != null)
        {
            globalDirector.Stop();
            globalDirector.playableAsset = timelineAsset;
            globalDirector.Play();
            Debug.Log($"[Timeline 系統] 成功載入並播放: {loadPath}");
        }
        else
        {
            Debug.LogError($"[Timeline 系統] 找不到 Timeline 資源，請檢查路徑: Resources/{loadPath}");
        }
    }

    // 輔支工具：將 "position(0,1.5,0)" 這種字串提取為 Vector3 結構
    Vector3 ExtractVector3(string tag)
    {
        try
        {
            int start = tag.IndexOf('(') + 1;
            int end = tag.IndexOf(')');
            string content = tag.Substring(start, end - start);
            string[] values = content.Split(',');

            return new Vector3(
                float.Parse(values[0].Trim()),
                float.Parse(values[1].Trim()),
                float.Parse(values[2].Trim())
            );
        }
        catch
        {
            Debug.LogError($"[解析錯誤] 標籤 '{tag}' 的 Vector3 格式有誤，請確保格式為 (x,y,z) 且使用英文逗號。");
            return Vector3.zero;
        }
    }

    void RefreshSceneObjectsDictionary()
    {
        allSceneObjects.Clear();
        foreach (var obj in FindObjectsOfType<InteractableObject>(true))
        {
            if (!allSceneObjects.ContainsKey(obj.name))
                allSceneObjects[obj.name] = obj;
        }
    }

    // --- 工具與 UI ---

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