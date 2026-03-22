using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraMovement : MonoBehaviour
{
    public float x;
    public float y;
    public float xSpeed = 3000000;//x敏度
    public float ySpeed = 3000000;//y敏度
    private Quaternion rotationEuler;
    void LateUpdate()
    {
        //證取滑鼠的X、Y轴移動訊息
        x += Input.GetAxis("Mouse X") * xSpeed;
        y -= Input.GetAxis("Mouse Y") * ySpeed;
        //保證X在360度以內
        if (x > 360)
        {
            x -= 360;
        }
        else if (x < 0)
        {
            x += 360;
        }
        rotationEuler = Quaternion.Euler(y, x, 0);
        transform.rotation = rotationEuler;
    }

    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }
}
