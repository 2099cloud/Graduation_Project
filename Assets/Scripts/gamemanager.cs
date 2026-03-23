using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class gamemanager : MonoBehaviour
{
    public GameObject Messege,ObjectName;
    // Start is called before the first frame update
    void Start()
    {
        GameObject[] gameObjects = GameObject.FindGameObjectsWithTag("ObjectExpo");
        for (int i = 0; i < gameObjects.Length; i++)
        {
            gameObjects[i].SetActive(false);
        }

        ObjectName.GetComponent<Text>().text = " ";
    }


        // Update is called once per frame
        void Update()
        {

        }
}

