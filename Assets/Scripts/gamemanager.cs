using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Ink.Runtime;
using TMPro;

public class GameManager : MonoBehaviour
{
    [Header("Ink 配置")]
    public TextAsset inkJsonAsset;
    private Story story;

    [Header("UI 引用")]
    public TextMeshProUGUI dialogueText;
    public GameObject choiceButtonPrefab, choiceHolder; // 合併宣告

    [Header("節奏與過渡")]
    public float bufferTime = 0.5f;
    public CanvasGroup fadeGroup; // 拖入 Fade 遮罩
    public float fadeDuration = 0.5f;

    private bool isWaitingForChoice = false;
    private int currentSceneValue = 0; // 追蹤目前的場景數值

    void Start()
    {
        if (!inkJsonAsset) return;
        story = new Story(inkJsonAsset.text);

        // 【核心優化】：監聽 Ink 變數 "scene"，一旦數值改變就觸發 OnSceneChanged
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
            else if (story.currentChoices.Count == 0) dialogueText.text = "...";
        }
    }

    // --- 核心邏輯區 ---

    private void ContinueStory()
    {
        if (!story.canContinue) return;

        string text = "";
        while (story.canContinue && string.IsNullOrWhiteSpace(text))
            text = story.Continue().Trim();

        if (!string.IsNullOrWhiteSpace(text))
        {
            dialogueText.text = text;
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

    IEnumerator SceneTransition(int nextScene)
    {
        isWaitingForChoice = true;
        // --- 新增：淡出前的緩衝時間 ---
        // 讓玩家有時間看完最後一句對話，不會立刻變黑
        yield return new WaitForSeconds(bufferTime);
        // 1. 淡出 (Fade Out)
        yield return Fade(1);

        // 2. 這裡處理你的 SceneManager.csv 加載邏輯
        Debug.Log($"根據變數變更，載入場景: {nextScene}");
        // YourSceneManager.Load(nextScene); 

        yield return new WaitForSeconds(0.5f);

        // 3. 淡入 (Fade In)
        yield return Fade(0);

        isWaitingForChoice = false;
        // 如果場景切換後有新對話，手動推動
        ContinueStory();
    }

    // --- 工具函式區 ---

    IEnumerator Fade(float targetAlpha)
    {
        float speed = Mathf.Abs(fadeGroup.alpha - targetAlpha) / fadeDuration;
        while (!Mathf.Approximately(fadeGroup.alpha, targetAlpha))
        {
            fadeGroup.alpha = Mathf.MoveTowards(fadeGroup.alpha, targetAlpha, speed * Time.deltaTime);
            yield return null;
        }
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
            GameObject btn = Instantiate(choiceButtonPrefab, choiceHolder.transform);
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

    void ClearChoices() { foreach (Transform child in choiceHolder.transform) Destroy(child.gameObject); }
    void HandleTags(List<string> tags) { /* 處理標籤邏輯 */ }
}