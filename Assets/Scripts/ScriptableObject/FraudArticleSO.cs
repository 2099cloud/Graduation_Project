using UnityEngine;

public enum FraudCategory
{
    eCommerceScam,     // 假網拍詐騙
    InvestmentScam,  // 投資詐騙
    FakePoliceScam          // 假檢警詐騙
}

[CreateAssetMenu(fileName = "NewFraudArticle", menuName = "AntiFraud/Fraud Article Data")]
public class FraudArticleSO : ScriptableObject
{
    [Header("基本資訊")]
    public string articleID;
    public string articleTitle;
    public FraudCategory category;

    [Header("比對設定")]
    [Tooltip("對應 3D 證據主物件的 ID (例如: Box_01)")]
    public string targetMainInfoID;

    [Header("內文 (請使用 <link=\"子線索ID\">關鍵字</link>)")]
    [TextArea(10, 20)]
    public string articleBody;
}