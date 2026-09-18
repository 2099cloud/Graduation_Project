using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using TMPro;

public class LabelGenerator : MonoBehaviour
{
    public GameObject labelPrefab;
    public TextAsset csvFile;
    public float yOffset = 2.0f;

    private List<GameObject> generatedLabels = new List<GameObject>();
    private PlayableDirector activeDirector;

    void Start()
    {
        GenerateLabelsFromCSV();
        BindTimelineEvents();
    }

    void OnDestroy()
    {
        UnbindTimelineEvents();
    }

    void GenerateLabelsFromCSV()
    {
        if (csvFile == null) return;

        string[] lines = csvFile.text.Split('\n');

        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;

            string[] data = lines[i].Split(',');
            if (data.Length < 2) continue;

            string displayName = data[0].Trim();
            string hierarchyName = data[1].Trim();

            GameObject targetObj = GameObject.Find(hierarchyName);

            if (targetObj != null)
            {
                SpawnLabel(displayName, targetObj.transform);
            }
            else
            {
                Debug.LogWarning("Target object not found: " + hierarchyName);
            }
        }
    }

    void SpawnLabel(string text, Transform target)
    {
        Vector3 spawnPos = target.position + Vector3.up * yOffset;
        GameObject newLabel = Instantiate(labelPrefab, spawnPos, Quaternion.identity, transform);

        TextMeshPro tmp = newLabel.GetComponentInChildren<TextMeshPro>();
        if (tmp != null)
        {
            tmp.text = text;
        }

        generatedLabels.Add(newLabel);
    }

    private void BindTimelineEvents()
    {
        activeDirector = FindObjectOfType<PlayableDirector>();
        if (activeDirector != null)
        {
            activeDirector.played += OnTimelinePlayed;
            activeDirector.stopped += OnTimelineStopped;

            if (activeDirector.state == PlayState.Playing)
            {
                SetLabelsActive(false);
            }
        }
    }

    private void UnbindTimelineEvents()
    {
        if (activeDirector != null)
        {
            activeDirector.played -= OnTimelinePlayed;
            activeDirector.stopped -= OnTimelineStopped;
        }
    }

    private void OnTimelinePlayed(PlayableDirector director)
    {
        SetLabelsActive(false);
    }

    private void OnTimelineStopped(PlayableDirector director)
    {
        SetLabelsActive(true);
    }

    private void SetLabelsActive(bool isActive)
    {
        foreach (GameObject label in generatedLabels)
        {
            if (label != null)
            {
                label.SetActive(isActive);
            }
        }
    }
}