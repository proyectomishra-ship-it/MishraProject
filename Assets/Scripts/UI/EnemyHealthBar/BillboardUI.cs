using UnityEngine;

public class BillboardUI : MonoBehaviour
{
    private Camera cam;

    private void Start()
    {
        cam = Camera.main;
    }

    private void LateUpdate()
    {
        if (cam == null)
            return;

        transform.forward = cam.transform.forward;
    }
}