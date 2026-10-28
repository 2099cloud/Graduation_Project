using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[RequireComponent(typeof(Collider))]
public class InteractiveDoor : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Choice Button Prefab")]
    public GameObject choiceButtonPrefab;
    [Tooltip("Choice Holder Container")]
    public Transform choiceHolder;

    [Header("UI Text Customization")]
    public string enterButtonText = "Enter";
    public string cancelButtonText = "Cancel";

    [Header("Ink Knot Configuration")]
    [Tooltip("Target Ink knot to trigger on enter")]
    public string targetKnot = "";

    [Header("Door Teleport Positions")]
    [Tooltip("Side A position")]
    public Vector3 targetPositionA;
    [Tooltip("Side B position")]
    public Vector3 targetPositionB;

    [Header("Player Tag")]
    public string playerTag = "Player";

    private GameObject currentPlayer = null;

    // 只記錄「這扇門自己」建立的按鈕，避免誤刪 Ink 選項按鈕（兩者共用同一個 choiceHolder）
    private readonly List<GameObject> spawnedButtons = new List<GameObject>();

    private void Reset()
    {
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

            // 過場播放中（例如 CharacterController 被重新啟用而再次進入觸發區）不要跳出門的選項
            GameManager gm = FindObjectOfType<GameManager>();
            if (gm != null && gm.IsTimelineCutsceneActive) return;

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

    private void ShowDoorChoices()
    {
        ClearChoices();

        if (choiceButtonPrefab == null || choiceHolder == null)
        {
            Debug.LogWarning("[InteractiveDoor] Choice Button Prefab or Choice Holder is missing!");
            return;
        }

        CreateChoiceButton(enterButtonText, OnEnterClicked);
        CreateChoiceButton(cancelButtonText, OnCancelClicked);
    }

    private void CreateChoiceButton(string buttonText, UnityEngine.Events.UnityAction onClickAction)
    {
        GameObject btnObj = Instantiate(choiceButtonPrefab, choiceHolder);
        spawnedButtons.Add(btnObj);

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

        Button btn = btnObj.GetComponent<Button>();
        if (btn != null)
        {
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(onClickAction);
        }
    }

    private void OnEnterClicked()
    {
        string knotToRun = GetEffectiveTargetKnot();

        if (!string.IsNullOrEmpty(knotToRun))
        {
            TriggerInkKnot(knotToRun);
        }
        else
        {
            if (currentPlayer != null)
            {
                TeleportPlayerToOppositeSide();
            }
        }

        ClearChoices();
    }

    private void OnCancelClicked()
    {
        ClearChoices();
    }

    private void TriggerInkKnot(string knotName)
    {
        GameManager gm = FindObjectOfType<GameManager>();
        if (gm != null)
        {
            gm.ExecuteKnot(knotName);
        }
        else
        {
            Debug.LogWarning("[InteractiveDoor] GameManager not found in scene!");
        }
    }

    private string GetEffectiveTargetKnot()
    {
        InteractableObject io = GetComponent<InteractableObject>();
        if (io != null && !string.IsNullOrEmpty(io.targetKnot))
        {
            return io.targetKnot;
        }
        return targetKnot;
    }

    private void TeleportPlayerToOppositeSide()
    {
        float distA = Vector3.Distance(currentPlayer.transform.position, targetPositionA);
        float distB = Vector3.Distance(currentPlayer.transform.position, targetPositionB);

        Vector3 destination = (distA < distB) ? targetPositionB : targetPositionA;

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
        // 只清除這扇門自己建立的按鈕，不動 Ink 的選項
        foreach (GameObject btn in spawnedButtons)
        {
            if (btn != null) Destroy(btn);
        }
        spawnedButtons.Clear();
    }

    private void OnDisable()
    {
        ClearChoices();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(targetPositionA, 0.4f);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(targetPositionB, 0.4f);
    }
}