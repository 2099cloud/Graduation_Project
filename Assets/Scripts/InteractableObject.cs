using UnityEngine;
public class InteractableObject : MonoBehaviour
{
    [Header("設定這個物件對應的 Ink 節點")]
    public string targetKnot;

    // 可以加一個開關，控制目前這個物件是否可以被點擊
    public bool isInteractable = true;

    private void Start()
    {
        
    }
}