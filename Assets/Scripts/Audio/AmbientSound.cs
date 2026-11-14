using UnityEngine;

/// <summary>
/// Sonido ambiente en bucle que se oye mas fuerte al acercarse: fogata, rio, viento, cascada.
/// Se agrega a un objeto vacio colocado en el mapa.
/// </summary>
[AddComponentMenu("Mishra Audio/Ambient Sound")]
public class AmbientSound : MonoBehaviour
{
    public AudioClip sonido;
    [Range(0f, 1f)] public float volumen = 0.6f;

    [Tooltip("Hasta esta distancia suena a volumen completo.")]
    public float distanciaMinima = 3f;
    [Tooltip("Mas lejos que esto ya no se oye.")]
    public float distanciaMaxima = 30f;

    private AudioSource source;

    private void Awake()
    {
        source = GetComponent<AudioSource>();
        if (source == null) source = gameObject.AddComponent<AudioSource>();

        source.clip = sonido;
        source.loop = true;
        source.playOnAwake = false;
        source.spatialBlend = 1f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = distanciaMinima;
        source.maxDistance = distanciaMaxima;
    }

    private void OnEnable()
    {
        if (source != null && sonido != null && !source.isPlaying)
            source.Play();
    }

    private void OnDisable()
    {
        if (source != null)
            source.Stop();
    }

    private void Update()
    {
        if (source != null)
            source.volume = volumen * AudioManager.GainFor(AudioCategory.Efectos);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.4f, 1f, 0.6f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, distanciaMinima);
        Gizmos.color = new Color(0.4f, 1f, 0.6f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, distanciaMaxima);
    }
}
