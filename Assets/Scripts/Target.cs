using UnityEngine;

public class Target : MonoBehaviour
{
    public GameObject ObjectExpo,Player,playerCamera;
    public string interactMessage = "你與物件互動了！";

    public void Start()
    {
        Player= GameObject.Find("Main Character");
        playerCamera = GameObject.Find("Main Camera");
    }
    public void Interact()
    {
        if (ObjectExpo.activeInHierarchy == false)
        {
            ObjectExpo.SetActive(true);
            Debug.Log(interactMessage);
            Player.GetComponent<CharacterController>().enabled = false;
            playerCamera.GetComponent<CameraMovement>().enabled = false;
        }
        else
        {
            Player.GetComponent<CharacterController>().enabled = true;
            playerCamera.GetComponent<CameraMovement>().enabled = true;
            ObjectExpo.SetActive(false);
        }
       
        // 這裡可以放開門、撿東西、觸發事件等邏輯
    }
    
}
