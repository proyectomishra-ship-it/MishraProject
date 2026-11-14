using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Conecta sliders del menu de Opciones con los volumenes del juego.
/// Se agrega al panel de Opciones y se arrastran los sliders (los que no se usen, se dejan vacios).
/// Los valores se guardan solos y se recuerdan al volver a abrir el juego.
/// </summary>
[AddComponentMenu("Mishra Audio/Volume Settings UI")]
public class VolumeSettingsUI : MonoBehaviour
{
    [SerializeField] private Slider general;
    [SerializeField] private Slider musica;
    [SerializeField] private Slider efectos;
    [SerializeField] private Slider interfaz;

    private void Awake()
    {
        Setup(general, AudioCategory.General);
        Setup(musica, AudioCategory.Musica);
        Setup(efectos, AudioCategory.Efectos);
        Setup(interfaz, AudioCategory.Interfaz);
    }

    private void OnEnable()
    {
        Refresh(general, AudioCategory.General);
        Refresh(musica, AudioCategory.Musica);
        Refresh(efectos, AudioCategory.Efectos);
        Refresh(interfaz, AudioCategory.Interfaz);
    }

    private static void Setup(Slider slider, AudioCategory category)
    {
        if (slider == null) return;

        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.wholeNumbers = false;
        slider.onValueChanged.AddListener(value => AudioManager.SetVolume(category, value));
    }

    private static void Refresh(Slider slider, AudioCategory category)
    {
        if (slider != null)
            slider.SetValueWithoutNotify(AudioManager.GetVolume(category));
    }
}
