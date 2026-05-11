using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class gamemanager : MonoBehaviour
{
    public GameObject Messege, ObjectName;
    public bool incoming_Call = false;

    [Header("手機更換貼圖設定")]
    public GameObject phoneObject;      // 拖入你的 Phone 物件
    public string newTextureName;       // Resources 資料夾內的圖片名稱

    // 1. 定義委派 (Delegate)：規定廣播的格式（這裡規定不需要帶參數）
    public delegate void TextureChangedAction();

    // 2. 定義事件 (Event)：建立一個名為 OnPhoneScammed 的頻道
    public static event TextureChangedAction OnPhoneScammed;

    void Start()
    {
        // 原本的邏輯：隱藏物件
        GameObject[] gameObjects = GameObject.FindGameObjectsWithTag("ObjectExpo");
        for (int i = 0; i < gameObjects.Length; i++)
        {
            gameObjects[i].SetActive(false);
        }

        ObjectName.GetComponent<Text>().text = " ";

        // 啟動計時器：30 秒後換圖
        StartCoroutine(ChangePhoneTextureAfterTime(10f));
    }

    IEnumerator ChangePhoneTextureAfterTime(float delay)
    {
        yield return new WaitForSeconds(delay);

        if (phoneObject != null)
        {
            // 從 Resources 載入圖片
            Texture2D newTex = Resources.Load<Texture2D>(newTextureName);

            if (newTex != null)
            {
                Renderer rend = phoneObject.GetComponent<Renderer>();

                // --- 關鍵修改區 ---
                // 取得所有材質的暫存陣列
                Material[] mats = rend.materials;

                // 檢查是否有 Element 1 (陣列長度至少要為 2)
                if (mats.Length >= 2)
                {
                    // 修改 Element 1 的貼圖 (索引值為 1)
                    mats[1].SetTexture("_BaseMap", newTex);

                    // 必須將修改後的陣列重新指定回 renderer 才會生效
                    rend.materials = mats;
                    
                    if (OnPhoneScammed != null)
                    {
                        OnPhoneScammed();
                        Debug.Log("OnPhoneScammed();");
                    }
                    Debug.Log("手機 Element 1 貼圖已更換！");
                }
                else
                {
                    Debug.LogWarning("該物件沒有 Element 1 材質！");
                }
                // ------------------
            }
            else
            {
                Debug.LogError("找不到指定的圖片檔案，請確認是否放在 Resources 資料夾內。");
            }
        }
    }
}