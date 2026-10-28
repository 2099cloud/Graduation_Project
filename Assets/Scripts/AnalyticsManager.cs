using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class AnalyticsManager : MonoBehaviour
{
    public static AnalyticsManager Instance { get; private set; }

    [Header("Google Apps Script Web App URL")]
    public string googleScriptUrl = "YOUR_GOOGLE_APPS_SCRIPT_WEB_APP_URL_HERE";

    private string sessionID;
    private List<Dictionary<string, object>> eventQueue = new List<Dictionary<string, object>>();
    private bool isUploading = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            sessionID = Guid.NewGuid().ToString();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Track detailed custom event and cache it in memory instead of sending immediately
    /// </summary>
    public void TrackEvent(string eventName, Dictionary<string, object> parameters)
    {
        if (parameters == null) parameters = new Dictionary<string, object>();

        parameters["session_id"] = sessionID;
        parameters["timestamp"] = DateTime.UtcNow.ToString("o");
        parameters["event_name"] = eventName;

        // Cache event into queue
        eventQueue.Add(parameters);
    }

    /// <summary>
    /// Call this method when the player ends the game or reaches the summary screen to upload all queued events
    /// </summary>
    public void UploadAllEvents()
    {
        if (!isUploading && eventQueue.Count > 0)
        {
            StartCoroutine(SendQueueToGoogleSheets());
        }
    }

    private IEnumerator SendQueueToGoogleSheets()
    {
        isUploading = true;

        // Create a copy of the queue to process
        List<Dictionary<string, object>> queueCopy = new List<Dictionary<string, object>>(eventQueue);
        eventQueue.Clear();

        foreach (var data in queueCopy)
        {
            string jsonBody = MiniJsonSerialize(data);

            using (UnityWebRequest www = new UnityWebRequest(googleScriptUrl, "POST"))
            {
                byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);
                www.uploadHandler = new UploadHandlerRaw(bodyRaw);
                www.downloadHandler = new DownloadHandlerBuffer();
                www.SetRequestHeader("Content-Type", "application/json");

                yield return www.SendWebRequest();

                if (www.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning("[Analytics] Failed to log event: " + www.error);
                }
            }
        }

        isUploading = false;
    }

    // Helper method for quick JSON serialization without external libraries
    private string MiniJsonSerialize(Dictionary<string, object> dict)
    {
        List<string> entries = new List<string>();
        foreach (var kvp in dict)
        {
            string valStr = kvp.Value != null ? kvp.Value.ToString() : "";
            entries.Add($"\"{kvp.Key}\": \"{valStr}\"");
        }
        return "{" + string.Join(",", entries) + "}";
    }
}