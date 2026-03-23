using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Movement : MonoBehaviour
{
    private float speed = 6.0f;
    private CharacterController characterController;
    Transform cameraTransform;
    // Start is called before the first frame update
    void Start()
    {
        characterController = GetComponent<CharacterController>();
        cameraTransform = GameObject.Find("Main Camera").transform;
        
    }

    // Update is called once per frame
    void Update()
    {

        characterControllerSetting();
       
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


    public void playerInteracter()
    {
       

    }
}
