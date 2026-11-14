using System;
using UnityEngine;

/// <summary>
/// Un "casillero de sonido": se llena arrastrando archivos de audio en el Inspector.
/// Se usa en todos los componentes de audio (personajes, interfaz, ambiente...).
/// </summary>
[Serializable]
public class SoundSlot
{
    [Tooltip("Arrastra aqui uno o varios sonidos. Si pones varios, se elige uno al azar cada vez (suena mas natural).")]
    public AudioClip[] sonidos;

    [Range(0f, 1f)]
    public float volumen = 1f;

    [Tooltip("Variacion aleatoria del tono para que no suene siempre igual. 1 = sin cambio.")]
    [Range(0.5f, 1.5f)] public float tonoMinimo = 0.95f;
    [Range(0.5f, 1.5f)] public float tonoMaximo = 1.05f;

    [Tooltip("Segundos minimos entre dos reproducciones (evita que suene ametrallado).")]
    public float esperaMinima = 0.05f;

    [Tooltip("Activado: se oye mas fuerte cuanto mas cerca estas (sonidos del mundo). " +
             "Desactivado: suena igual en todos lados (interfaz, musica).")]
    public bool sonido3D = true;

    [NonSerialized] private float lastPlayTime = -999f;
    [NonSerialized] private int lastIndex = -1;

    public bool TieneSonidos => sonidos != null && sonidos.Length > 0;

    /// <summary>Elige un sonido (al azar, evitando repetir el anterior) y su tono.</summary>
    public bool TryPick(out AudioClip clip, out float vol, out float pitch)
    {
        clip = null;
        vol = volumen;
        pitch = 1f;

        if (!TieneSonidos) return false;
        if (Time.unscaledTime - lastPlayTime < esperaMinima) return false;

        int index = UnityEngine.Random.Range(0, sonidos.Length);
        if (sonidos.Length > 1 && index == lastIndex)
            index = (index + 1) % sonidos.Length;

        clip = sonidos[index];
        if (clip == null) return false;

        lastIndex = index;
        lastPlayTime = Time.unscaledTime;

        float lo = Mathf.Min(tonoMinimo, tonoMaximo);
        float hi = Mathf.Max(tonoMinimo, tonoMaximo);
        pitch = UnityEngine.Random.Range(lo, hi);
        return true;
    }
}
