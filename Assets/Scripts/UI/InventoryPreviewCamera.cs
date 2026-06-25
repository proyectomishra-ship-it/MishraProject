using UnityEngine;

/// <summary>
/// Cámara de preview del inventario.
/// Hace snap frente al jugador al abrirse y se queda fija —
/// no sigue al jugador mientras se mueve.
/// </summary>
public class InventoryPreviewCamera : MonoBehaviour
{
    [Tooltip("Distancia al jugador")]
    [SerializeField] private float distance = 2.0f;

    [Tooltip("Altura a la que apunta la cámara (0 = pies, 1 = cabeza aprox)")]
    [SerializeField] private float heightOffset = 0.9f;

    [Tooltip("Rotación horizontal de la cámara alrededor del jugador (grados)")]
    [SerializeField] private float yawOffset = 0f;

    private Transform target;
    private Camera previewCam;

    private void Awake()
    {
        previewCam = GetComponent<Camera>();
    }

    public void SetTarget(Transform playerTransform)
    {
        target = playerTransform;
    }

    /// <summary>
    /// Activa la cámara y hace snap inmediato a la posición frente al jugador.
    /// No se mueve más hasta que se llame Show() de nuevo.
    /// </summary>
    public void Show()
    {
        if (target == null)
        {
            Debug.LogWarning("[PreviewCam] No hay target asignado.");
            return;
        }

        SnapToTarget();
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    private void SnapToTarget()
    {
        Vector3 lookAt = target.position + Vector3.up * heightOffset;

        // Calcular dirección con yawOffset para poder rotar la cámara alrededor del jugador
        Quaternion yaw = Quaternion.Euler(0f, target.eulerAngles.y + yawOffset, 0f);
        Vector3 forward = yaw * Vector3.forward;

        transform.position = lookAt - forward * distance;
        transform.LookAt(lookAt);
    }
}