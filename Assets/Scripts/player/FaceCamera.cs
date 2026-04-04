using UnityEngine;

public class FaceCamera : MonoBehaviour
{
    [SerializeField] private Camera targetCamera;
    [SerializeField] private bool lockXRotation = true;   
    [SerializeField] private bool lockZRotation = true;

    private void Awake()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;
    }

    private void LateUpdate()
    {
        if (targetCamera == null) return;

        transform.rotation = Quaternion.LookRotation(
            transform.position - targetCamera.transform.position,
            Vector3.up
        );

        Vector3 e = transform.eulerAngles;
        if (lockXRotation) e.x = 0f;
        if (lockZRotation) e.z = 0f;
        transform.eulerAngles = e;
    }
}