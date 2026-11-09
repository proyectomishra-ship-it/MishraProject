using UnityEngine;

/// <summary>
/// Orbita lenta de la camara alrededor de un punto, para usar el propio mundo
/// del juego como fondo del menu principal (sin imagenes). Va en la Main Camera.
/// </summary>
public class MenuCameraOrbit : MonoBehaviour
{
    [Header("Objetivo")]
    [Tooltip("Objeto vacio en el centro de la escena que se quiere mostrar.")]
    [SerializeField] private Transform focusPoint;

    [Header("Orbita")]
    [SerializeField] private float distance = 25f;
    [SerializeField] private float height = 6f;
    [Tooltip("Velocidad de giro. Valores bajos (2 a 5) se sienten cinematicos.")]
    [SerializeField] private float degreesPerSecond = 3f;
    [SerializeField] private float startAngle = 0f;

    [Header("Mirada")]
    [Tooltip("Altura sobre el punto de enfoque hacia donde mira la camara.")]
    [SerializeField] private float lookHeightOffset = 2f;

    [Header("Vaiven suave")]
    [SerializeField] private float bobAmplitude = 0.3f;
    [SerializeField] private float bobSpeed = 0.4f;

    private float angle;

    private void Start()
    {
        angle = startAngle;
    }

    private void LateUpdate()
    {
        if (focusPoint == null) return;

        angle += degreesPerSecond * Time.deltaTime;
        float rad = angle * Mathf.Deg2Rad;

        Vector3 offset = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad)) * distance;
        float bob = Mathf.Sin(Time.time * bobSpeed) * bobAmplitude;

        transform.position = focusPoint.position + offset + Vector3.up * (height + bob);
        transform.LookAt(focusPoint.position + Vector3.up * lookHeightOffset);
    }

    private void OnDrawGizmosSelected()
    {
        if (focusPoint == null) return;

        Gizmos.color = new Color(1f, 0.8f, 0.3f, 0.8f);
        Vector3 c = focusPoint.position + Vector3.up * height;
        Vector3 prev = c + new Vector3(0f, 0f, distance);
        for (int i = 1; i <= 48; i++)
        {
            float a = i / 48f * Mathf.PI * 2f;
            Vector3 next = c + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * distance;
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
        Gizmos.DrawLine(focusPoint.position, focusPoint.position + Vector3.up * lookHeightOffset);
    }
}
