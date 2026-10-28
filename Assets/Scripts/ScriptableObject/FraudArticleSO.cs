using System.Collections.Generic;
using UnityEngine;

public enum FraudCategory
{
    None,
    shoppingScam,     // 電商詐騙
    InvestmentScam,    // 投資詐騙
    FakePoliceScam,     // 假警察詐騙
    RomanceScam     // 假交友詐騙
}

[CreateAssetMenu(fileName = "NewFraudArticle", menuName = "AntiFraud/Fraud Article Data")]
public class FraudArticleSO : ScriptableObject
{
    [Header("基本資訊")]
    public string articleID;
    public string articleTitle;
    public FraudCategory category;

    [Header("關聯設定")]
    [Tooltip("對應 3D 證據主物件 ID (例如: Box_01)")]
    public string targetMainInfoID;

    [Header("內文清單 (請使用 <link=\"子線索ID\">關鍵字</link>)")]
    [TextArea(5, 10)]
    public List<string> articleBodies = new List<string>();

    // ---- 相容舊程式碼:避免 CS1061 'articleBody' 未定義 ----

    /// <summary>
    /// 舊版單一內文欄位的相容屬性,回傳所有段落以換行合併後的字串。
    /// </summary>
    public string articleBody
    {
        get { return string.Join("\n", articleBodies); }
    }

    /// <summary>內文段落數量。</summary>
    public int BodyCount
    {
        get { return articleBodies != null ? articleBodies.Count : 0; }
    }

    /// <summary>安全取得指定段落,索引超出範圍時回傳空字串。</summary>
    public string GetBody(int index)
    {
        if (articleBodies == null || index < 0 || index >= articleBodies.Count)
            return string.Empty;
        return articleBodies[index];
    }
}