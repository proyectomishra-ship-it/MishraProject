using UnityEngine;

/// <summary>
/// Controla la cámara de preview del inventario.
/// Se posiciona frente al jugador local y se activa/desactiva
/// junto con el panel de inventario.
///
/// SETUP EN UNITY:
///   - Agregar este script al GameObject InventoryPreviewCamera en la escena
///   - El GameObject debe estar desactivado por defecto
///   - Asignar la referencia desde InventoryUI.Initialize()
/// </summary>
public class InventoryPreviewCamera : MonoBehaviour
{
    [Tooltip("Distancia al jugador")]
    [SerializeField] private float distance = 2.5f;

    [Tooltip("Altura relativa al jugador")]
    [SerializeField] private float heightOffset = 1.0f;

    [Tooltip("Velocidad de seguimiento suave al jugador")]
    [SerializeField] private float followSpeed = 10f;

    private Transform target;
    private Camera previewCam;

    private void Awake()
    {
        previewCam = GetComponent<Camera>();
    }

    /// <summary>
    /// Llamar desde InventoryUI cuando se inicializa para el jugador local.
    /// </summary>
    public void SetTarget(Transform playerTransform)
    {
        target = playerTransform;
    }

    private void LateUpdate()
    {
        if (target == null) return;

        // Posición: frente al jugador, mirando hacia él
        Vector3 targetPos   = target.position + Vector3.up * heightOffset;
        Vector3 desiredPos  = targetPos - target.forward * distance;

        transform.position = Vector3.Lerp(
            transform.position,
            desiredPos,
            followSpeed * Time.deltaTime
        );

        transform.LookAt(targetPos);
    }

    public void Show()
    {
        if (target == null)
        {
            Debug.LogWarning("[PreviewCam] No hay target asignado.");
            return;
        }

        // Snap directo a la posición correcta antes de activar
        // para evitar que la cámara "vuele" desde su posición anterior
        Vector3 targetPos  = target.position + Vector3.up * heightOffset;
        transform.position = targetPos - target.forward * distance;
        transform.LookAt(targetPos);

        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }
}
