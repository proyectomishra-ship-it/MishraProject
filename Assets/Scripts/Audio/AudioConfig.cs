using System;
using UnityEngine;

public enum UISound { Hover, Click, PanelOpen, PanelClose, Error, Confirm, Equip, Craft, ItemPickup, Gold, QuestComplete }

/// <summary>
/// Un solo archivo donde se configura el audio general del juego:
/// volumenes iniciales, sonidos de menus y musica de cada escena.
/// Se crea con el menu Mishra > Audio > Create Audio Config y queda en
/// Assets/Resources/AudioConfig.asset (el juego lo carga solo).
/// </summary>
[CreateAssetMenu(fileName = "AudioConfig", menuName = "Mishra/Audio Config")]
public class AudioConfig : ScriptableObject
{
    [Serializable]
    public class SceneMusic
    {
        [Tooltip("Nombre EXACTO de la escena (ej: MainMenu, Scene1).")]
        public string escena;
        [Tooltip("Dejar vacio = esa escena queda en silencio.")]
        public AudioClip musica;
        [Range(0f, 1f)] public float volumen = 1f;
    }

    [Header("VOLUMENES INICIALES (0 a 1). El jugador los cambia en Opciones.")]
    [Range(0f, 1f)] public float volumenGeneral = 1f;
    [Range(0f, 1f)] public float volumenMusica = 0.7f;
    [Range(0f, 1f)] public float volumenEfectos = 1f;
    [Range(0f, 1f)] public float volumenInterfaz = 1f;

    [Header("SONIDOS DE INTERFAZ (se aplican solos a todos los botones con Themed Button)")]
    public SoundSlot botonEncima = new SoundSlot { sonido3D = false };
    public SoundSlot botonClick = new SoundSlot { sonido3D = false };
    public SoundSlot abrirPanel = new SoundSlot { sonido3D = false };
    public SoundSlot cerrarPanel = new SoundSlot { sonido3D = false };
    public SoundSlot error = new SoundSlot { sonido3D = false };
    public SoundSlot confirmar = new SoundSlot { sonido3D = false };

    [Header("SONIDOS DE INVENTARIO Y MISIONES (suenan solo en el jugador que hace la accion)")]
    [Tooltip("Al equipar un arma o armadura desde el inventario.")]
    public SoundSlot equipar = new SoundSlot { sonido3D = false };
    [Tooltip("Al craftear con exito. Si falla (faltan materiales, oro o espacio) suena Error.")]
    public SoundSlot craftear = new SoundSlot { sonido3D = false };
    [Tooltip("Al recoger un objeto del suelo.")]
    public SoundSlot recogerItem = new SoundSlot { sonido3D = false };
    [Tooltip("Al recibir oro (por derrotar enemigos o por recompensas de mision).")]
    public SoundSlot ganarOro = new SoundSlot { sonido3D = false };
    [Tooltip("Al completar una mision (principal o secundaria).")]
    public SoundSlot misionCompletada = new SoundSlot { sonido3D = false };

    [Header("MUSICA POR ESCENA")]
    [Tooltip("Si una escena NO esta en la lista, sigue sonando la musica anterior " +
             "(util para que el tema del menu continue en las pantallas de red y de clase).")]
    public SceneMusic[] musicaPorEscena;

    [Tooltip("Segundos que dura el cambio suave entre dos canciones.")]
    public float segundosDeTransicion = 2f;

    [Header("EFECTOS 3D (cuanto se alcanzan a oir)")]
    public float distanciaMinima = 3f;     // hasta aqui suena a volumen completo
    public float distanciaMaxima = 45f;    // mas alla ya no se oye

    public SoundSlot Get(UISound sound)
    {
        switch (sound)
        {
            case UISound.Hover: return botonEncima;
            case UISound.Click: return botonClick;
            case UISound.PanelOpen: return abrirPanel;
            case UISound.PanelClose: return cerrarPanel;
            case UISound.Error: return error;
            case UISound.Confirm: return confirmar;
            case UISound.Equip: return equipar;
            case UISound.Craft: return craftear;
            case UISound.ItemPickup: return recogerItem;
            case UISound.Gold: return ganarOro;
            case UISound.QuestComplete: return misionCompletada;
            default: return null;
        }
    }
}