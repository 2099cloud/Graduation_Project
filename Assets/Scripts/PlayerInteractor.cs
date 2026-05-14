using UnityEngine;
using UnityEngine.UI;
public class PlayerInteractor : MonoBehaviour
{
    public Camera playerCamera;   // 玩家攝影機
    public float interactRange = 5f;   // 射線最大距離
    public GameObject ObjectName,Point;
   
    private void Start()
    {
        ObjectName = GameObject.Find("ObjectName");
        Point = GameObject.Find("Point");
    }
    void Update()
    {
        // 從螢幕中央發射 Ray
        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));//向量維持在 (0.5f, 0.5f, 0f)為最佳

        if (Physics.Raycast(ray, out RaycastHit hit, interactRange))
        {
            // 嘗試取得互動物件
            /*Target target = hit.collider.GetComponent<Target>();
            if (target != null)
            {
                Debug.Log("瞄準到互動物件：" + hit.collider.name);
                ObjectName.GetComponent<Text>().text = hit.collider.name;
                Point.GetComponent<Image>().color = new Color(0, 0, 0);
                // 玩家按下互動鍵（例如 E）
                if (Input.GetKeyDown(KeyCode.E))
                {
                    target.Interact();
                    
                    
                }

            }
            else
            {
                ObjectName.GetComponent<Text>().text = " ";
                Point.GetComponent<Image>().color = new Color(255, 255, 255);
            }*/
        }
       
    }
}
