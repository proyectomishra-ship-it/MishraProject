using UnityEngine;

/// <summary>
/// Gira continuamente el objeto donde esta. Para indicadores de carga hechos
/// con una Image radial (sin arte). Usa tiempo sin escala: gira aunque el juego este en pausa.
/// </summary>
public class UISpinner : MonoBehaviour
{
    [Tooltip("Negativo = sentido horario.")]
    [SerializeField] private float degreesPerSecond = -270f;

    private void Update()
    {
        transform.Rotate(0f, 0f, degreesPerSecond * Time.unscaledDeltaTime);
    }
}
