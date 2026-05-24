using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Movement : MonoBehaviour
{
    //Settings for characterControllerSetting();
    private float speed = 6.0f;
    private CharacterController characterController;
    Transform cameraTransform;

    //Settings for playerInteractor();
    public Camera playerCamera;   // 玩家攝影機
    public float interactRange = 5f;   // 射線最大距離
    GameObject ObjectName, Point;



    // Start is called before the first frame update
    void Start()
    {
        //Settings for characterControllerSetting();
        characterController = GetComponent<CharacterController>();
        cameraTransform = GameObject.Find("Main Camera").transform;
        
        //Settings for playerInteractor();
        playerCamera = GameObject.Find("Main Camera").GetComponent<Camera>();
    }

    // Update is called once per frame
    void Update()
    {

        characterControllerSetting();
        playerInteractor();
    }






    public void characterControllerSetting()
    {
        float horizontalInput = Input.GetAxis("Horizontal");
        float verticalInput = Input.GetAxis("Vertical");
        Vector3 moveDirection = transform.forward * verticalInput * speed;

        characterController.SimpleMove(moveDirection);
        this.transform.rotation = Quaternion.Euler(0, cameraTransform.rotation.eulerAngles.y, 0);

        if (Input.GetKey("a"))
        {
            transform.Translate(-0.1f, 0, 0);

        }
        if (Input.GetKey("d"))
        {
            transform.Translate(0.1f, 0, 0);

        }
    }


    public void playerInteractor()
    {
        // 從螢幕中央發射 Ray
        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));//向量維持在 (0.5f, 0.5f, 0f)為最佳
    
        if (Physics.Raycast(ray, out RaycastHit hit, interactRange))
        {
            // 嘗試取得互動物件
            
            if ((hit.collider.gameObject.tag) == "Interactable_Object")
            {
                Debug.Log("瞄準到物件：" + hit.collider.name);
                ObjectName.GetComponent<Text>().text = hit.collider.name;//待修改
                Point.GetComponent<Image>().color = new Color(0, 0, 0);
                
                // 按E進行互動
                if (Input.GetKeyDown(KeyCode.E))
                {
                    //hit.collider.gameObject.GetComponent<Target>().Interact();
                    //ObjectName.Interact();


                }

            }
            else
            {
                ObjectName.GetComponent<Text>().text = "";
                Point.GetComponent<Image>().color = new Color(255, 255, 255);
            }
        }

    }
}
