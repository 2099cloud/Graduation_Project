using UnityEngine;

public class ProceduralBreathing : MonoBehaviour
{
    [Header("Breathing Settings")]
    public float breathSpeed = 2.0f;
    public float breathAmount = 1.0f;
    public Vector3 rotationAxis = new Vector3(1f, 0f, 0f);

    private Quaternion initialLocalRotation;
    private bool isBreathing = false; // 控制是否啟動呼吸效果

    private void Start()
    {
        initialLocalRotation = transform.localRotation;
    }

    private void LateUpdate()
    {
        // 只有當 isBreathing 為 true 時才執行呼吸運算（高效能）
        if (!isBreathing) return;

        float offset = Mathf.Sin(Time.time * breathSpeed) * breathAmount;
        transform.localRotation = initialLocalRotation * Quaternion.Euler(rotationAxis * offset);
    }

    /// <summary>
    /// 開啟或關閉呼吸效果（可由 GameManager 或 Timeline Signal 呼叫）
    /// </summary>
    public void SetBreathingActive(bool active)
    {
        isBreathing = active;
        if (!active)
        {
            // 當關閉時，恢復為原本的初始旋轉，避免停在歪斜的角度
            transform.localRotation = initialLocalRotation;
        }
    }
}