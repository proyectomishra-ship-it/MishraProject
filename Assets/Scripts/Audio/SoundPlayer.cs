using UnityEngine;

/// <summary>
/// Para cualquier sonido suelto: un cofre, una puerta, una trampa, un boton especial.
/// Se conecta desde el Inspector (por ejemplo en el "On Click" de un boton: SoundPlayer > Play2D).
/// </summary>
[AddComponentMenu("Mishra Audio/Sound Player")]
public class SoundPlayer : MonoBehaviour
{
    public SoundSlot sonido = new SoundSlot();

    [Tooltip("Marcar para que suene solo cuando el objeto aparece o se activa.")]
    public bool sonarAlActivarse;

    [Tooltip("Que volumen del menu Opciones controla este sonido.")]
    public AudioCategory categoria = AudioCategory.Efectos;

    private void OnEnable()
    {
        if (sonarAlActivarse)
            Play();
    }

    /// <summary>Suena en la posicion de este objeto (se atenua con la distancia si el sonido es 3D).</summary>
    public void Play()
    {
        AudioManager.Play(sonido, transform.position, categoria);
    }

    /// <summary>Suena igual en todos lados. Usar esta para botones y menus.</summary>
    public void Play2D()
    {
        AudioManager.Play2D(sonido, categoria);
    }
}
