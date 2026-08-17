using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 掛載於 3D 電腦物件上。
/// 負責：開啟電腦 UI、單一 Panel 切換背景圖片與按鈕群組、自動綁定按鈕頁面跳轉、歷史紀錄，
/// 整合 FraudArticleSO 自動載入文章內容與控制 TMP 顯示/隱藏，以及監聽 TMPLinkHandler 超連結點擊事件。
/// </summary>
public class VirtualBrowserController : MonoBehaviour
{
    public static VirtualBrowserController Instance { get; private set; }

    [System.Serializable]
    public class PageButtonInfo
    {
        [Tooltip("按鈕的 UI Button 物件")]
        public Button buttonComponent;

        [Tooltip("按下此按鈕後要切換到的 Target Page ID")]
        public string targetPageID;
    }

    [System.Serializable]
    public class WebPage
    {
        [Tooltip("頁面唯一識別碼，例如: Home, AntiScamHome, Scam_Package")]
        public string pageID;

        [Tooltip("該頁面的背景圖片 (Sprite)")]
        public Sprite backgroundSprite;

        [Tooltip("該頁面的互動按鈕清單 (可設定每個按鈕按下後開啟哪個頁面)")]
        public List<PageButtonInfo> pageButtons = new List<PageButtonInfo>();

        [Tooltip("該頁面對應的文章 ScriptableObject (包含套用 <link> 標籤的內文)")]
        public FraudArticleSO articleData;

        [Tooltip("該頁面用來顯示文章內文的 TextMeshPro 元件")]
        public TMP_Text articleContentText;
    }

    [Header("網頁 UI 總容器與共用 Panel 設定")]
    public GameObject browserUICanvas;        // 網頁 UI 總 Canvas (不執行 SetActive(false))
    public Image sharedBackgroundImage;       // 用來呈現各頁面背景圖的共用 Image Panel

    [Header("頁面列表與預設頁面")]
    public string defaultPageID = "Home";      // 開啟電腦時預設顯示的頁面 ID
    public List<WebPage> webPages = new List<WebPage>();

    private Dictionary<string, WebPage> pageDict = new Dictionary<string, WebPage>();
    private Stack<string> pageHistory = new Stack<string>();
    private WebPage currentPage;
    private TMPLinkHandler currentLinkHandler; // 紀錄目前頁面的 TMPLinkHandler
    private bool isBrowserOpen = false;

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

        // 初始化頁面字典，並預設隱藏按鈕與 TMP 文字物件
        foreach (var page in webPages)
        {
            if (page != null && !string.IsNullOrEmpty(page.pageID))
            {
                pageDict[page.pageID] = page;

                // 預設隱藏該頁面的 TMP 物件
                if (page.articleContentText != null)
                {
                    page.articleContentText.gameObject.SetActive(false);
                }

                if (page.pageButtons != null)
                {
                    foreach (var btnInfo in page.pageButtons)
                    {
                        if (btnInfo != null && btnInfo.buttonComponent != null)
                        {
                            string targetID = btnInfo.targetPageID;
                            // 自動綁定按鈕點擊事件
                            btnInfo.buttonComponent.onClick.AddListener(() => SwitchToPage(targetID));
                            // 預設隱藏所有頁面按鈕
                            btnInfo.buttonComponent.gameObject.SetActive(false);
                        }
                    }
                }
            }
        }

        // 預設隱藏背景 Panel (不影響主 Canvas)
        if (sharedBackgroundImage != null)
        {
            sharedBackgroundImage.gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        if (!isBrowserOpen) return;

        // 按下 ESC 鍵離開電腦網頁
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

        // 開啟頁面背景 Panel
        sharedBackgroundImage.gameObject.SetActive(true);
        isBrowserOpen = true;

        // 顯示並解鎖滑鼠游標
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // 清空歷史紀錄並載入預設頁面
        pageHistory.Clear();
        SwitchToPage(defaultPageID, false);
    }

    public void CloseBrowser()
    {
        // 1. 解除前一頁的 TMPLinkHandler 事件監聽並隱藏 TMP
        UnbindCurrentLinkHandler();

        // 2. 隱藏當前頁面的按鈕與 TMP 文字
        if (currentPage != null)
        {
            HideAllButtonsInPage(currentPage);

            if (currentPage.articleContentText != null)
            {
                currentPage.articleContentText.gameObject.SetActive(false);
            }
        }

        // 3. 隱藏背景 Panel
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
            Debug.LogWarning("[VirtualBrowser] 找不到 Page ID: " + pageID);
            return;
        }

        // 1. 解除前一個頁面的 TMPLinkHandler 事件監聽
        UnbindCurrentLinkHandler();

        // 2. 隱藏目前頁面的所有按鈕與 TMP 文字
        if (currentPage != null)
        {
            HideAllButtonsInPage(currentPage);

            if (currentPage.articleContentText != null)
            {
                currentPage.articleContentText.gameObject.SetActive(false);
            }
        }

        // 3. 記錄舊頁面到歷史紀錄
        if (recordHistory && currentPage != null)
        {
            pageHistory.Push(currentPage.pageID);
        }

        // 4. 切換至新頁面資料
        currentPage = pageDict[pageID];

        // 5. 抽換共用 Panel 的背景圖片
        if (sharedBackgroundImage != null && currentPage.backgroundSprite != null)
        {
            sharedBackgroundImage.sprite = currentPage.backgroundSprite;
        }

        // 6. 顯示新頁面對應的按鈕清單
        ShowAllButtonsInPage(currentPage);

        // 7. 自動載入 FraudArticleSO 內容至 TMP、顯示 TMP 物件並監聽 TMPLinkHandler
        LoadArticleAndBindLinkHandler(currentPage);

        Debug.Log("[VirtualBrowser] 已切換至頁面: " + pageID);
    }

    /// <summary>
    /// 自動載入文章內容至 TMP，顯示 TMP GameObject，並註冊 TMPLinkHandler 點擊事件
    /// </summary>
    private void LoadArticleAndBindLinkHandler(WebPage page)
    {
        if (page == null || page.articleContentText == null) return;

        // 1. 若有文章 SO 資料，寫入文字並啟用 TMP 物件；否則確保隱藏
        if (page.articleData != null && !string.IsNullOrEmpty(page.articleData.articleBody))
        {
            page.articleContentText.text = page.articleData.articleBody;
            page.articleContentText.gameObject.SetActive(true); // 顯示負責顯示文字的 TMP 物件
        }
        else
        {
            page.articleContentText.gameObject.SetActive(false);
            return;
        }

        // 2. 取得 TMP 物件上的 TMPLinkHandler 並綁定點擊事件
        currentLinkHandler = page.articleContentText.GetComponent<TMPLinkHandler>();
        if (currentLinkHandler != null)
        {
            currentLinkHandler.OnLinkClicked += ProcessVerification;
        }
        else
        {
            Debug.LogWarning($"[VirtualBrowser] 頁面 {page.pageID} 的 TMP_Text 物件上未掛載 TMPLinkHandler 元件！");
        }
    }

    /// <summary>
    /// 解除當前 TMPLinkHandler 的事件訂閱
    /// </summary>
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
            Debug.Log("[VirtualBrowser] 已無上一頁紀錄。");
        }
    }

    public void GoHome()
    {
        SwitchToPage(defaultPageID);
    }

    private void HideAllButtonsInPage(WebPage page)
    {
        if (page == null || page.pageButtons == null) return;
        foreach (var btnInfo in page.pageButtons)
        {
            if (btnInfo != null && btnInfo.buttonComponent != null)
            {
                btnInfo.buttonComponent.gameObject.SetActive(false);
            }
        }
    }

    private void ShowAllButtonsInPage(WebPage page)
    {
        if (page == null || page.pageButtons == null) return;
        foreach (var btnInfo in page.pageButtons)
        {
            if (btnInfo != null && btnInfo.buttonComponent != null)
            {
                btnInfo.buttonComponent.gameObject.SetActive(true);
            }
        }
    }

    private void ProcessVerification(string snippetID)
    {
        if (InspectManager.Instance == null) return;
        InspectableObject currentTarget = InspectManager.Instance.GetCurrentTarget();

        if (currentTarget == null)
        {
            Debug.LogWarning("[打詐網] 目前沒有選取任何檢視物件！請先點擊下方物件欄中的物件再進行文字比對。");
            return;
        }

        if (!currentTarget.IsAllSubObjectsFound())
        {
            Debug.Log("[打詐網] 物件 " + currentTarget.mainInfoID + " 的 3D 線索尚未完全搜集完畢，無法進行網頁比對！");
            return;
        }

        bool isMatch = currentTarget.TryVerifySubInfo(snippetID);

        if (isMatch)
        {
            Debug.Log("[比對成功！] 線索 " + snippetID + " 匹配成功！");
            InspectManager.Instance.UpdateCurrentSlotProgress(currentTarget);
        }
        else
        {
            Debug.Log("[比對失敗] 文字 '" + snippetID + "' 與當前物件無關或已驗證過。");
        }
    }
}