using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class VirtualBrowserController : MonoBehaviour
{
    public static VirtualBrowserController Instance { get; private set; }

    [System.Serializable]
    public class PageButtonInfo
    {
        [Tooltip("Button sprite image - Optional")]
        public Sprite buttonSprite;

        [Tooltip("Target Page ID to switch when clicked")]
        public string targetPageID;

        [Tooltip("Anchored Position relative to container center")]
        public Vector2 position = Vector2.zero;

        [Tooltip("Width and Height of the button")]
        public Vector2 size = new Vector2(100f, 100f);
    }

    [System.Serializable]
    public class PageSpriteInfo
    {
        [Tooltip("Sprite image to display")]
        public Sprite sprite;

        [Tooltip("Anchored Position relative to container center")]
        public Vector2 position = Vector2.zero;

        [Tooltip("Width and Height of the sprite image")]
        public Vector2 size = new Vector2(100f, 100f);
    }

    [System.Serializable]
    public class WebPage
    {
        [Tooltip("Unique page identifier")]
        public string pageID;

        [Tooltip("Background Image (Sprite) - Optional")]
        public Sprite backgroundSprite;

        [Tooltip("List of custom sprites with positions and sizes")]
        public List<PageSpriteInfo> pageSprites = new List<PageSpriteInfo>();

        [Tooltip("List of page interactive buttons")]
        public List<PageButtonInfo> pageButtons = new List<PageButtonInfo>();

        [Tooltip("Article ScriptableObject")]
        public FraudArticleSO articleData;

        [Tooltip("TextMeshPro text component for article content")]
        public TMP_Text articleContentText;
    }

    [Header("UI Containers")]
    public GameObject browserUICanvas;
    public Image sharedBackgroundImage;

    [Tooltip("Container Transform where custom page sprites will be placed")]
    public RectTransform spriteContainer;

    [Tooltip("Container Transform where custom page buttons will be placed")]
    public RectTransform buttonContainer;

    [Header("Page Settings")]
    public string defaultPageID = "Home";
    public List<WebPage> webPages = new List<WebPage>();

    private Dictionary<string, WebPage> pageDict = new Dictionary<string, WebPage>();
    private Stack<string> pageHistory = new Stack<string>();
    private WebPage currentPage;
    private TMPLinkHandler currentLinkHandler;
    private bool isBrowserOpen = false;

    // Object pool for UI Image components
    private List<Image> activeSpritePool = new List<Image>();
    private List<Image> inactiveSpritePool = new List<Image>();

    // Object pool for UI Button components
    private List<Button> activeButtonPool = new List<Button>();
    private List<Button> inactiveButtonPool = new List<Button>();

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

        foreach (var page in webPages)
        {
            if (page != null && !string.IsNullOrEmpty(page.pageID))
            {
                pageDict[page.pageID] = page;

                if (page.articleContentText != null)
                {
                    page.articleContentText.gameObject.SetActive(false);
                }
            }
        }

        if (sharedBackgroundImage != null)
        {
            sharedBackgroundImage.gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        if (!isBrowserOpen) return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            CloseBrowser();
            return;
        }
    }

    private void OnMouseDown()
    {
        OpenBrowser();
    }

    public void OpenBrowser()
    {
        if (sharedBackgroundImage == null) return;

        sharedBackgroundImage.gameObject.SetActive(true);
        isBrowserOpen = true;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        pageHistory.Clear();
        SwitchToPage(defaultPageID, false);
    }

    public void CloseBrowser()
    {
        UnbindCurrentLinkHandler();

        if (currentPage != null)
        {
            if (currentPage.articleContentText != null)
            {
                currentPage.articleContentText.gameObject.SetActive(false);
            }
        }

        RecycleSpriteImages();
        RecycleButtonObjects();

        if (sharedBackgroundImage != null)
        {
            sharedBackgroundImage.gameObject.SetActive(false);
        }

        isBrowserOpen = false;
        currentPage = null;
    }

    public void SwitchToPage(string pageID)
    {
        SwitchToPage(pageID, true);
    }

    private void SwitchToPage(string pageID, bool recordHistory)
    {
        if (!pageDict.ContainsKey(pageID))
        {
            Debug.LogWarning("[VirtualBrowser] Page ID not found: " + pageID);
            return;
        }

        UnbindCurrentLinkHandler();

        if (currentPage != null)
        {
            if (currentPage.articleContentText != null)
            {
                currentPage.articleContentText.gameObject.SetActive(false);
            }
        }

        RecycleSpriteImages();
        RecycleButtonObjects();

        if (recordHistory && currentPage != null)
        {
            pageHistory.Push(currentPage.pageID);
        }

        currentPage = pageDict[pageID];

        if (sharedBackgroundImage != null && currentPage.backgroundSprite != null)
        {
            sharedBackgroundImage.sprite = currentPage.backgroundSprite;
        }

        RenderPageSprites(currentPage);
        RenderPageButtons(currentPage);
        LoadArticleAndBindLinkHandler(currentPage);

        Debug.Log("[VirtualBrowser] Switched to page: " + pageID);
    }

    private void RenderPageSprites(WebPage page)
    {
        if (page == null || page.pageSprites == null) return;

        RectTransform targetParent = spriteContainer != null ? spriteContainer : (sharedBackgroundImage != null ? sharedBackgroundImage.rectTransform : null);
        if (targetParent == null) return;

        foreach (var spriteInfo in page.pageSprites)
        {
            if (spriteInfo == null || spriteInfo.sprite == null) continue;

            Image img = GetPooledImage(targetParent);
            img.sprite = spriteInfo.sprite;
            img.rectTransform.anchoredPosition = spriteInfo.position;
            img.rectTransform.sizeDelta = spriteInfo.size;

            // Force rect transform order to match inspector list
            img.transform.SetAsLastSibling();

            img.gameObject.SetActive(true);
            activeSpritePool.Add(img);
        }
    }

    private Image GetPooledImage(RectTransform parent)
    {
        Image img;
        if (inactiveSpritePool.Count > 0)
        {
            int lastIdx = inactiveSpritePool.Count - 1;
            img = inactiveSpritePool[lastIdx];
            inactiveSpritePool.RemoveAt(lastIdx);
            img.transform.SetParent(parent, false);
        }
        else
        {
            GameObject go = new GameObject("PageSprite", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            img = go.GetComponent<Image>();
            img.raycastTarget = false;
        }
        return img;
    }

    private void RecycleSpriteImages()
    {
        for (int i = 0; i < activeSpritePool.Count; i++)
        {
            if (activeSpritePool[i] != null)
            {
                activeSpritePool[i].gameObject.SetActive(false);
                inactiveSpritePool.Add(activeSpritePool[i]);
            }
        }
        activeSpritePool.Clear();
    }

    private void RenderPageButtons(WebPage page)
    {
        if (page == null || page.pageButtons == null) return;

        RectTransform targetParent = buttonContainer != null ? buttonContainer : (spriteContainer != null ? spriteContainer : (sharedBackgroundImage != null ? sharedBackgroundImage.rectTransform : null));
        if (targetParent == null) return;

        foreach (var btnInfo in page.pageButtons)
        {
            if (btnInfo == null) continue;

            Button btn = GetPooledButton(targetParent);

            Image img = btn.GetComponent<Image>();
            if (img != null)
            {
                img.sprite = btnInfo.buttonSprite;
                img.raycastTarget = true;
                img.color = btnInfo.buttonSprite != null ? Color.white : new Color(1f, 1f, 1f, 0f);
            }

            RectTransform rectTransform = btn.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                rectTransform.anchoredPosition = btnInfo.position;
                rectTransform.sizeDelta = btnInfo.size;
            }

            btn.onClick.RemoveAllListeners();
            string targetID = btnInfo.targetPageID;
            btn.onClick.AddListener(() => SwitchToPage(targetID));

            // Force rect transform order to match inspector list
            btn.transform.SetAsLastSibling();

            btn.gameObject.SetActive(true);
            activeButtonPool.Add(btn);
        }
    }

    private Button GetPooledButton(RectTransform parent)
    {
        Button btn;
        if (inactiveButtonPool.Count > 0)
        {
            int lastIdx = inactiveButtonPool.Count - 1;
            btn = inactiveButtonPool[lastIdx];
            inactiveButtonPool.RemoveAt(lastIdx);
            btn.transform.SetParent(parent, false);
        }
        else
        {
            GameObject go = new GameObject("PageButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            btn = go.GetComponent<Button>();
        }
        return btn;
    }

    private void RecycleButtonObjects()
    {
        for (int i = 0; i < activeButtonPool.Count; i++)
        {
            if (activeButtonPool[i] != null)
            {
                activeButtonPool[i].onClick.RemoveAllListeners();
                activeButtonPool[i].gameObject.SetActive(false);
                inactiveButtonPool.Add(activeButtonPool[i]);
            }
        }
        activeButtonPool.Clear();
    }

    private void LoadArticleAndBindLinkHandler(WebPage page)
    {
        if (page == null || page.articleContentText == null) return;

        if (page.articleData != null && !string.IsNullOrEmpty(page.articleData.articleBody))
        {
            page.articleContentText.text = page.articleData.articleBody;
            page.articleContentText.gameObject.SetActive(true);
        }
        else
        {
            page.articleContentText.gameObject.SetActive(false);
            return;
        }

        currentLinkHandler = page.articleContentText.GetComponent<TMPLinkHandler>();
        if (currentLinkHandler != null)
        {
            currentLinkHandler.OnLinkClicked += ProcessVerification;
        }
        else
        {
            Debug.LogWarning($"[VirtualBrowser] Page {page.pageID} TMP_Text missing TMPLinkHandler!");
        }
    }

    private void UnbindCurrentLinkHandler()
    {
        if (currentLinkHandler != null)
        {
            currentLinkHandler.OnLinkClicked -= ProcessVerification;
            currentLinkHandler = null;
        }
    }

    public void GoBack()
    {
        if (pageHistory.Count > 0)
        {
            string previousPageID = pageHistory.Pop();
            SwitchToPage(previousPageID, false);
        }
        else
        {
            Debug.Log("[VirtualBrowser] No previous page in history.");
        }
    }

    public void GoHome()
    {
        SwitchToPage(defaultPageID);
    }

    private void ProcessVerification(string snippetID)
    {
        if (InspectManager.Instance == null) return;
        InspectableObject currentTarget = InspectManager.Instance.GetCurrentTarget();

        if (currentTarget == null)
        {
            Debug.LogWarning("[VirtualBrowser] No target object selected!");
            return;
        }

        if (!currentTarget.IsAllSubObjectsFound())
        {
            Debug.Log("[VirtualBrowser] Target sub objects not fully collected: " + currentTarget.mainInfoID);
            return;
        }

        bool isMatch = currentTarget.TryVerifySubInfo(snippetID);

        if (isMatch)
        {
            Debug.Log("[Verification Success] Snippet matched: " + snippetID);
            InspectManager.Instance.UpdateCurrentSlotProgress(currentTarget);
        }
        else
        {
            Debug.Log("[Verification Failed] Mismatch or already verified: " + snippetID);
        }
    }
}