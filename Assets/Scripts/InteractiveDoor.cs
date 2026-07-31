using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[RequireComponent(typeof(Collider))]
public class InteractiveDoor : MonoBehaviour
{
    [Header("UI 引用 (複用 Ink 對話 UI)")]
    [Tooltip("請拖入與 Ink 對話同款的 Choice Button Prefab")]
    public GameObject choiceButtonPrefab;
    [Tooltip("請拖入與 Ink 對話同款的 Choice Holder 容器")]
    public Transform choiceHolder;

    [Header("門的兩側傳送座標 (可以在 Inspector 手動輸入 Vector3)")]
    [Tooltip("門 A 側傳送點座標")]
    public Vector3 targetPositionA;
    [Tooltip("門 B 側傳送點座標")]
    public Vector3 targetPositionB;

    [Header("玩家標籤名稱")]
    public string playerTag = "Player";

    private GameObject currentPlayer = null;

    private void Reset()
    {
        // 自動勾選 Collider 的 Is Trigger
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(playerTag))
        {
            currentPlayer = other.gameObject;
            ShowDoorChoices();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(playerTag))
        {
            ClearChoices();
            currentPlayer = null;
        }
    }

    /// <summary>
    /// 動態生成「進入」與「不進入」按鈕
    /// </summary>
    private void ShowDoorChoices()
    {
        ClearChoices();

        if (choiceButtonPrefab == null || choiceHolder == null)
        {
            Debug.LogWarning("[DoorInteraction] 未拖入 Choice Button Prefab 或 Choice Holder！");
            return;
        }

        // 1. 生成 "進入" 按鈕
        CreateChoiceButton("進入", OnEnterClicked);

        // 2. 生成 "不進入" 按鈕
        CreateChoiceButton("不進入", OnCancelClicked);
    }

    private void CreateChoiceButton(string buttonText, UnityEngine.Events.UnityAction onClickAction)
    {
        GameObject btnObj = Instantiate(choiceButtonPrefab, choiceHolder);

        // 自動判斷文字組件為 TextMeshPro 或 一般 Text
        TextMeshProUGUI tmpText = btnObj.GetComponentInChildren<TextMeshProUGUI>();
        if (tmpText != null)
        {
            tmpText.text = buttonText;
        }
        else
        {
            Text uiText = btnObj.GetComponentInChildren<Text>();
            if (uiText != null) uiText.text = buttonText;
        }

        // 綁定點擊事件
        Button btn = btnObj.GetComponent<Button>();
        if (btn != null)
        {
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(onClickAction);
        }
    }

    private void OnEnterClicked()
    {
        if (currentPlayer != null)
        {
            TeleportPlayerToOppositeSide();
        }
        ClearChoices();
    }

    private void OnCancelClicked()
    {
        ClearChoices();
    }

    /// <summary>
    /// 計算距離並將玩家傳送到門對面的指定座標
    /// </summary>
    private void TeleportPlayerToOppositeSide()
    {
        // 判斷玩家目前離 A 點近還是 B 點近，自動傳送到較遠的「對面」點
        float distA = Vector3.Distance(currentPlayer.transform.position, targetPositionA);
        float distB = Vector3.Distance(currentPlayer.transform.position, targetPositionB);

        Vector3 destination = (distA < distB) ? targetPositionB : targetPositionA;

        // 暫時關閉 CharacterController 以確保瞬移成功 (避免物理/撞擊判定抵銷位移)
        CharacterController cc = currentPlayer.GetComponent<CharacterController>();
        if (cc != null)
        {
            cc.enabled = false;
            currentPlayer.transform.position = destination;
            cc.enabled = true;
        }
        else
        {
            currentPlayer.transform.position = destination;
        }
    }

    private void ClearChoices()
    {
        if (choiceHolder == null) return;

        foreach (Transform child in choiceHolder)
        {
            Destroy(child.gameObject);
        }
    }

    private void OnDisable()
    {
        ClearChoices();
    }

    // 在 Scene 視窗繪製提示球體，方便手動調整 Vector3 座標
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(targetPositionA, 0.4f);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(targetPositionB, 0.4f);
    }
}