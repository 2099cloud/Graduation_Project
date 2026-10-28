using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TutorialManager : MonoBehaviour
{
    public static TutorialManager Instance { get; private set; }

    [Header("UI References")]
    public Image tutorialImageDisplay;
    public GameObject tutorialPanel;

    [Header("Button References")]
    public Button nextButton;
    public Button prevButton;
    public Button closeButton;

    [Header("Tutorial Sprites")]
    public Sprite[] tutorialSprites;

    private List<int> activePages = new List<int>();
    private int currentListIndex = 0;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            Debug.Log("[TutorialManager] Instance initialized successfully.");
        }
        else
        {
            Debug.LogWarning("[TutorialManager] Duplicate instance detected and destroyed.");
            Destroy(gameObject);
            return;
        }

        if (nextButton != null) nextButton.onClick.AddListener(OnClickNext);
        if (prevButton != null) prevButton.onClick.AddListener(OnClickPrev);
        if (closeButton != null) closeButton.onClick.AddListener(CloseTutorial);
    }

    /// <summary>
    /// Open tutorial with specific page indices list.
    /// </summary>
    public void OpenTutorialPages(List<int> pages)
    {
        Debug.Log($"[TutorialManager] OpenTutorialPages called with count: {(pages != null ? pages.Count : 0)}");

        if (tutorialSprites == null || tutorialSprites.Length == 0)
        {
            Debug.LogError("[TutorialManager Error] No tutorial sprites assigned in the inspector!");
            return;
        }

        if (pages == null || pages.Count == 0)
        {
            Debug.LogError("[TutorialManager Error] Pages list is null or empty!");
            return;
        }

        activePages.Clear();
        foreach (int p in pages)
        {
            if (p >= 0 && p < tutorialSprites.Length)
            {
                activePages.Add(p);
            }
            else
            {
                Debug.LogWarning($"[TutorialManager Warning] Page index {p} is out of bounds for tutorialSprites (Length: {tutorialSprites.Length}). Ignored.");
            }
        }

        if (activePages.Count == 0)
        {
            Debug.LogError("[TutorialManager Error] No valid pages could be added to activePages!");
            return;
        }

        currentListIndex = 0;

        if (tutorialPanel != null)
        {
            tutorialPanel.SetActive(true);
            Debug.Log("[TutorialManager] tutorialPanel activated.");
        }
        else
        {
            Debug.LogError("[TutorialManager Error] tutorialPanel reference is missing!");
        }

        if (closeButton != null)
        {
            closeButton.gameObject.SetActive(true);
        }

        UpdateTutorialUI();
    }

    /// <summary>
    /// Open tutorial from index 0 up to maxIndex inclusive.
    /// </summary>
    public void OpenTutorialUpTo(int maxIndex)
    {
        Debug.Log($"[TutorialManager] OpenTutorialUpTo called with maxIndex: {maxIndex}");
        List<int> pages = new List<int>();
        int targetMax = Mathf.Clamp(maxIndex, 0, tutorialSprites.Length - 1);
        for (int i = 0; i <= targetMax; i++)
        {
            pages.Add(i);
        }
        OpenTutorialPages(pages);
    }

    /// <summary>
    /// Close tutorial panel and disable all navigation buttons.
    /// </summary>
    public void CloseTutorial()
    {
        Debug.Log("[TutorialManager] CloseTutorial called.");

        if (tutorialPanel != null) tutorialPanel.SetActive(false);

        if (nextButton != null) nextButton.gameObject.SetActive(false);
        if (prevButton != null) prevButton.gameObject.SetActive(false);
        if (closeButton != null) closeButton.gameObject.SetActive(false);
    }

    public void OnClickNext()
    {
        Debug.Log($"[TutorialManager] OnClickNext clicked. Current index before: {currentListIndex}, Total pages: {activePages.Count}");

        if (activePages == null || activePages.Count == 0) return;

        if (currentListIndex < activePages.Count - 1)
        {
            currentListIndex++;
            UpdateTutorialUI();
        }
        else
        {
            Debug.Log("[TutorialManager] Reached the end of tutorial pages via Next button. Closing.");
            CloseTutorial();
        }
    }

    public void OnClickPrev()
    {
        Debug.Log($"[TutorialManager] OnClickPrev clicked. Current index before: {currentListIndex}");

        if (activePages == null || activePages.Count == 0) return;

        if (currentListIndex > 0)
        {
            currentListIndex--;
            UpdateTutorialUI();
        }
    }

    private void UpdateTutorialUI()
    {
        if (activePages.Count == 0) return;

        int pageIndex = activePages[currentListIndex];
        Debug.Log($"[TutorialManager] UpdateTutorialUI -> Displaying list index: {currentListIndex}, mapping to sprite index: {pageIndex}");

        if (tutorialImageDisplay != null && tutorialSprites != null && pageIndex >= 0 && pageIndex < tutorialSprites.Length)
        {
            tutorialImageDisplay.sprite = tutorialSprites[pageIndex];
        }
        else
        {
            Debug.LogError("[TutorialManager Error] tutorialImageDisplay is missing or sprite index is out of bounds!");
        }

        if (prevButton != null)
        {
            bool showPrev = currentListIndex > 0;
            prevButton.gameObject.SetActive(showPrev);
        }

        if (nextButton != null)
        {
            bool showNext = currentListIndex < activePages.Count - 1;
            nextButton.gameObject.SetActive(showNext);
        }
    }
}