using UnityEngine;
using Ink.Runtime;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;

public class DialogueManager : MonoBehaviour
{
    [Header("Ink 配置")]
    public TextAsset inkJsonAsset;
    private Story story;

    [Header("UI 顯示")]
    public TextMeshProUGUI dialogueText;
    public GameObject choiceButtonPrefab;
    public Transform choiceHolder;

    void Start()
    {
        // 初始化故事
        if (inkJsonAsset != null)
        {
            story = new Story(inkJsonAsset.text);

            // --- 加入這一行 ---
            // 強制故事從你定義的那個「結點」開始跑
            story.ChoosePathString("Act1_Home_Night");
            // ------------------

            DisplayNextLine();
        }


    }

    void Update()
    {
        // 偵測滑鼠左鍵點擊
        // 條件：點擊左鍵 且 目前沒有顯示選項 且 故事還可以繼續
        if (Input.GetMouseButtonDown(0))
        {
            // 如果目前有選項，強迫玩家必須點擊按鈕，不能透過點擊空白處跳過
            if (story.currentChoices.Count > 0) return;

            if (story.canContinue)
            {
                DisplayNextLine();
            }
            else if (!story.canContinue && story.currentChoices.Count == 0)
            {
                // 對話徹底結束
                dialogueText.text = "...";
                Debug.Log("對話結束");
            }
        }
    }

    public void DisplayNextLine()
    {
        if (story.canContinue)
        {
            string text = story.Continue();

            // 處理標籤
            HandleTags(story.currentTags);

            dialogueText.text = text;

            // 如果跑完這句剛好出現選項，立刻顯示按鈕
            if (!story.canContinue && story.currentChoices.Count > 0)
            {
                DisplayChoices();
            }
        }
    }

    void DisplayChoices()
    {
        ClearChoices();

        foreach (Choice choice in story.currentChoices)
        {
            GameObject btn = Instantiate(choiceButtonPrefab, choiceHolder);
            btn.GetComponentInChildren<TextMeshProUGUI>().text = choice.text;

            // 點擊按鈕後的邏輯
            btn.GetComponent<Button>().onClick.AddListener(() => {
                story.ChooseChoiceIndex(choice.index);
                ClearChoices();
                DisplayNextLine(); // 選擇後自動顯示下一句
            });
        }
    }

    void HandleTags(List<string> tags)
    {
        foreach (string tag in tags)
        {
            // 支援格式如 # Key: Value
            string[] splitTag = tag.Split(':');
            string key = splitTag[0].Trim();
            string value = splitTag.Length > 1 ? splitTag[1].Trim() : "";

            switch (key)
            {
                case "TriggerAct":
                    // 呼叫你的 GameEventManager (假設你已經定義好 Enum)
                    Debug.Log($"[事件] 切換幕至: {value}");
                    // GameEventManager.TriggerActChanged(...); 
                    break;

                case "PhoneAction":
                    Debug.Log($"[動作] 手機執行: {value}");
                    break;

                case "Audio":
                    Debug.Log($"[音效] 播放: {value}");
                    break;
            }
        }
    }

    void ClearChoices()
    {
        foreach (Transform child in choiceHolder)
        {
            Destroy(child.gameObject);
        }
    }
}