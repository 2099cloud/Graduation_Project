using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TaskSystemManager : MonoBehaviour
{
    public static TaskSystemManager Instance { get; private set; }

    [Header("UI References")]
    public TextMeshProUGUI taskText;
    public CanvasGroup fadeGroup;

    [Header("CSV Configuration")]
    public string taskCsvPath = "TaskManager";

    [Header("Popup Area Bounds Settings")]
    public Vector2 popupAnchorMin = new Vector2(0.5f, 0.5f);
    public Vector2 popupAnchorMax = new Vector2(0.5f, 0.5f);
    public Vector2 popupPivot = new Vector2(0.5f, 0.5f);
    public Vector2 popupAnchoredPosition = Vector2.zero;
    public Vector2 popupSizeDelta = new Vector2(600f, 400f);

    [Header("Collapsed Text Style")]
    public float collapsedFontSize = 24f;
    public Color collapsedColor = Color.white;
    public TextAlignmentOptions collapsedAlignment = TextAlignmentOptions.Left;
    public string collapsedPrefix = "Task: ";

    [Header("Popup Text Style")]
    public float popupTitleFontSize = 36f;
    public Color popupTitleColor = Color.black;
    public float popupContentFontSize = 24f;
    public Color popupContentColor = Color.white;
    public TextAlignmentOptions popupAlignment = TextAlignmentOptions.Center;

    public bool IsTaskPopupOpen { get; private set; } = false;

    private string currentTaskTitle = "";
    private string currentTaskFullContent = "";

    private Vector3 originalPosition;
    private Vector2 originalPivot;
    private Vector2 originalAnchorMin;
    private Vector2 originalAnchorMax;
    private Vector2 originalAnchoredPosition;
    private Vector2 originalSizeDelta;
    private float originalFontSize;
    private Color originalColor;
    private TextAlignmentOptions originalAlignment;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        InitOriginalTaskTextSettings();
    }

    private void InitOriginalTaskTextSettings()
    {
        if (taskText != null)
        {
            RectTransform rect = taskText.rectTransform;
            originalPosition = rect.localPosition;
            originalPivot = rect.pivot;
            originalAnchorMin = rect.anchorMin;
            originalAnchorMax = rect.anchorMax;
            originalAnchoredPosition = rect.anchoredPosition;
            originalSizeDelta = rect.sizeDelta;
            originalFontSize = taskText.fontSize;
            originalColor = taskText.color;
            originalAlignment = taskText.alignment;

            Button btn = taskText.gameObject.GetComponent<Button>();
            if (btn == null) btn = taskText.gameObject.AddComponent<Button>();
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(OnTaskTextClicked);
        }
    }

    private void Update()
    {
        if (IsTaskPopupOpen)
        {
            if (Input.GetMouseButtonDown(0))
            {
                OnCloseTaskPopupClicked();
            }
        }
    }

    public void ParseAndSetTask(string tagOrId)
    {
        string taskID = tagOrId.Replace("Task.text.", "").Trim();

        // 自動清理路徑：截掉 Resources/ 之前的前綴與 .csv 副檔名
        string cleanPath = taskCsvPath;
        int resIndex = cleanPath.IndexOf("Resources/", System.StringComparison.OrdinalIgnoreCase);
        if (resIndex != -1)
        {
            cleanPath = cleanPath.Substring(resIndex + "Resources/".Length);
        }
        if (cleanPath.EndsWith(".csv", System.StringComparison.OrdinalIgnoreCase))
        {
            cleanPath = cleanPath.Substring(0, cleanPath.Length - 4);
        }

        TextAsset taskCsv = Resources.Load<TextAsset>(cleanPath);
        if (taskCsv == null)
        {
            Debug.LogWarning("[TaskSystemManager] Task CSV not found at cleaned path: " + cleanPath);
            return;
        }

        string csvText = taskCsv.text.Replace("\r", "");
        string[] lines = csvText.Split('\n');

        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;
            string[] data = lines[i].Split(',');

            if (data[0].Trim() == taskID)
            {
                currentTaskTitle = taskID;
                currentTaskFullContent = data.Length > 1 ? data[1].Trim() : "";

                if (taskText != null)
                {
                    taskText.gameObject.SetActive(true);
                    if (!IsTaskPopupOpen)
                    {
                        ApplyCollapsedStyle();
                    }
                }
                break;
            }
        }
    }

    public void OnTaskTextClicked()
    {
        if (string.IsNullOrEmpty(currentTaskTitle) || IsTaskPopupOpen) return;
        if (fadeGroup != null && fadeGroup.alpha > 0.9f) return;

        IsTaskPopupOpen = true;

        if (fadeGroup != null)
        {
            fadeGroup.alpha = 0.65f;
            fadeGroup.blocksRaycasts = true;
        }

        if (taskText != null)
        {
            taskText.transform.SetAsLastSibling();
            RectTransform rect = taskText.rectTransform;
            rect.anchorMin = popupAnchorMin;
            rect.anchorMax = popupAnchorMax;
            rect.pivot = popupPivot;
            rect.anchoredPosition = popupAnchoredPosition;
            rect.sizeDelta = popupSizeDelta;

            taskText.alignment = popupAlignment;

            string titleHex = ColorUtility.ToHtmlStringRGB(popupTitleColor);
            string contentHex = ColorUtility.ToHtmlStringRGB(popupContentColor);

            taskText.text = "<color=#" + titleHex + "><size=" + popupTitleFontSize + "><b>" + currentTaskTitle + "</b></size></color>\n\n<color=#" + contentHex + "><size=" + popupContentFontSize + ">" + currentTaskFullContent + "</size></color>";
        }
    }

    public void OnCloseTaskPopupClicked()
    {
        if (!IsTaskPopupOpen) return;

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
            rect.sizeDelta = originalSizeDelta;

            ApplyCollapsedStyle();
        }
        StartCoroutine(ResetPopupFlag());
    }

    private void ApplyCollapsedStyle()
    {
        if (taskText == null) return;
        taskText.fontSize = collapsedFontSize;
        taskText.color = collapsedColor;
        taskText.alignment = collapsedAlignment;
        taskText.text = collapsedPrefix + currentTaskTitle;
    }

    private IEnumerator ResetPopupFlag()
    {
        yield return null;
        IsTaskPopupOpen = false;
    }

    public void SetTaskVisibility(bool visible)
    {
        if (taskText != null)
        {
            if (!visible)
            {
                taskText.gameObject.SetActive(false);
            }
            else if (!string.IsNullOrEmpty(currentTaskTitle))
            {
                taskText.gameObject.SetActive(true);
            }
        }
    }
}