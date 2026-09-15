using UnityEngine;

public class ProceduralBreathing : MonoBehaviour
{
    [Header("Breathing Settings")]
    public float breathSpeed = 2.0f;
    public float breathAmount = 1.0f;
    public Vector3 rotationAxis = new Vector3(1f, 0f, 0f);

    private Quaternion initialLocalRotation;

    private void Start()
    {
        initialLocalRotation = transform.localRotation;
    }

    private void LateUpdate()
    {
        float offset = Mathf.Sin(Time.time * breathSpeed) * breathAmount;
        transform.localRotation = initialLocalRotation * Quaternion.Euler(rotationAxis * offset);
    }
}