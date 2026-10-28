using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI; // 引入 UI 命名空間以使用 Image
using UnityEngine.SceneManagement;

public class Scene_Manager : MonoBehaviour
{
    [Header("背景淡出設定")]
    [Tooltip("請拖入 Background 物件的 Image 組件")]
    public Image background;
    [Tooltip("淡出至黑色的所需時間 (秒)")]
    public float fadeDuration = 1.0f;

    private bool isStarting = false; // 防重複點擊標記

    public void StartGame()
    {
        if (isStarting) return;
        StartCoroutine(FadeToBlackAndLoad());
    }

    private IEnumerator FadeToBlackAndLoad()
    {
        isStarting = true;

        if (background != null)
        {
            Color startColor = background.color;
            Color targetColor = Color.black;
            float elapsedTime = 0f;

            while (elapsedTime < fadeDuration)
            {
                elapsedTime += Time.deltaTime;
                // 緩慢將顏色 Lerp 轉為黑色
                background.color = Color.Lerp(startColor, targetColor, elapsedTime / fadeDuration);
                yield return null;
            }

            background.color = targetColor;
        }

        // 淡出完畢後切換場景
        SceneManager.LoadScene(1);
    }

    public void ExitGame()
    {
        Application.Quit();
    }

    public void LoadScene()
    {
        SceneManager.LoadScene("scene");
    }

    // ==========================================
    // 【新增】專門給 Timeline 結尾 Signal 呼叫的函數 (上傳資料並載入 Scene 2)
    // ==========================================
    public void LoadScene2()
    {
        // 1. 上傳累積的 Analytics 資料
        if (AnalyticsManager.Instance != null)
        {
            AnalyticsManager.Instance.UploadAllEvents();
            Debug.Log("[Scene Manager] Analytics uploaded before loading Scene 2.");
        }

        // 2. 切換至 Scene 2
        SceneManager.LoadScene(2);
    }
}