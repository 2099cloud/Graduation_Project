using System;
using System.Collections;
using UnityEngine;

public class TimerManager : MonoBehaviour
{
    public static TimerManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 啟動背景獨立計時，時間到後執行回調函數
    /// </summary>
    public void StartTimer(float seconds, Action onComplete)
    {
        StartCoroutine(TimerRoutine(seconds, onComplete));
    }

    private IEnumerator TimerRoutine(float seconds, Action onComplete)
    {
        Debug.Log($"[TimerManager] 開始背景獨立倒數: {seconds} 秒");
        yield return new WaitForSeconds(seconds);
        Debug.Log("[TimerManager] 倒數結束，觸發後續動作！");
        onComplete?.Invoke();
    }
}