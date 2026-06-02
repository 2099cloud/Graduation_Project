using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Movement : MonoBehaviour
{
    // Settings for characterControllerSetting();
    private float speed = 1.3f;
    private CharacterController characterController;
    Transform cameraTransform;

    // Settings for playerInteractor();
    public Camera playerCamera;   // 玩家攝影機
    public float interactRange = 5f;   // 射線最大距離
    public GameObject ObjectName, Point; // 修正原本未宣告初值的問題（若在 Inspector 拖放請保持 public）

    public Animator animator;

    void Start()
    {
        characterController = GetComponent<CharacterController>();
        cameraTransform = GameObject.Find("Main Camera").transform;
        playerCamera = GameObject.Find("Main Camera").GetComponent<Camera>();

        // 自動防呆抓取 Animator
        if (animator == null) animator = GetComponent<Animator>();
    }

    void Update()
    {
        characterControllerSetting();
        playerInteractor();
        AnimatorController();
    }

    public void characterControllerSetting()
    {
        // 讀取輸入
        float horizontalInput = Input.GetAxis("Horizontal");
        float verticalInput = Input.GetAxis("Vertical");

        // 【核心修正】：將前後（transform.forward）與 左右（transform.right）的移動向量結合
        // 這樣 WASD 就能同時作用於 CharacterController 
        Vector3 moveDirection = (transform.forward * verticalInput) + (transform.right * horizontalInput);

        // 乘以速度後傳給 SimpleMove 處理移動與重力
        characterController.SimpleMove(moveDirection * speed);

        // 讓角色轉向跟隨攝影機的水平朝向
        this.transform.rotation = Quaternion.Euler(0, cameraTransform.rotation.eulerAngles.y, 0);

        // 註銷原本的 transform.Translate，因為它會打亂 Input 數值並導致角色瞬移穿牆
        /*
        if (Input.GetKey("a")) { transform.Translate(-0.1f, 0, 0); }
        if (Input.GetKey("d")) { transform.Translate(0.1f, 0, 0); }
        */
    }

    public void playerInteractor()
    {
        if (playerCamera == null) return;

        // 從螢幕中央發射 Ray
        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        if (Physics.Raycast(ray, out RaycastHit hit, interactRange))
        {
            if (hit.collider.gameObject.CompareTag("Interactable_Object"))
            {
                Debug.Log("瞄準到物件：" + hit.collider.name);
                if (ObjectName != null) ObjectName.GetComponent<Text>().text = hit.collider.name;
                if (Point != null) Point.GetComponent<Image>().color = new Color(0, 0, 0);

                // 按 E 進行互動
                if (Input.GetKeyDown(KeyCode.E))
                {
                    // 互動觸發邏輯放這裡
                }
            }
            else
            {
                if (ObjectName != null) ObjectName.GetComponent<Text>().text = "";
                if (Point != null) Point.GetComponent<Image>().color = new Color(255, 255, 255);
            }
        }
    }

    public void AnimatorController()
    {
        // 這裡維持原樣，現在 horizontal 和 vertical 能拿到完整的 -1 ~ 1 數值了！
        float inputX = Input.GetAxis("Horizontal");
        float inputY = Input.GetAxis("Vertical");

        if (animator != null)
        {
            animator.SetFloat("MoveX", inputX);
            animator.SetFloat("MoveY", inputY);
        }
    }
}