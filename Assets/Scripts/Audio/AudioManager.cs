using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum AudioCategory { General, Musica, Efectos, Interfaz }

/// <summary>
/// Centro del audio. Se crea SOLO al iniciar el juego (no hay que ponerlo en ninguna escena).
///  - Reproduce efectos con un grupo de AudioSources reutilizables.
///  - Maneja la musica con transicion suave entre canciones.
///  - Guarda los volumenes del jugador (General, Musica, Efectos, Interfaz).
///  - Se asegura de que haya un unico AudioListener activo.
/// Configuracion en Resources/AudioConfig.asset.
/// </summary>
[AddComponentMenu("")]
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    private const int PoolSize = 24;
    private const string KeyGeneral = "Audio.General";
    private const string KeyMusica = "Audio.Musica";
    private const string KeyEfectos = "Audio.Efectos";
    private const string KeyInterfaz = "Audio.Interfaz";

    private class MusicOverride
    {
        public object owner;
        public AudioClip clip;
        public float volume;
    }

    public event System.Action OnVolumesChanged;

    private AudioConfig config;

    private readonly List<AudioSource> pool = new List<AudioSource>();
    private int poolCursor;

    private readonly AudioSource[] music = new AudioSource[2];
    private readonly float[] musicTarget = new float[2];
    private int activeMusic;
    private AudioClip currentClip;
    private AudioConfig.SceneMusic sceneTrack;
    private readonly List<MusicOverride> overrides = new List<MusicOverride>();

    private float volGeneral = 1f, volMusica = 0.7f, volEfectos = 1f, volInterfaz = 1f;
    private float guardTimer;

    // =========================
    // ARRANQUE
    // =========================

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;

        var go = new GameObject("AudioManager");
        DontDestroyOnLoad(go);
        go.AddComponent<AudioManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        config = Resources.Load<AudioConfig>("AudioConfig");
        if (config == null)
            Debug.LogWarning("[Audio] No hay Resources/AudioConfig.asset. " +
                             "Crealo con el menu Mishra > Audio > Create Audio Config.");

        LoadVolumes();
        BuildPool();
        BuildMusicSources();

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Instance = null;
        }
    }

    private void LoadVolumes()
    {
        float g = config != null ? config.volumenGeneral : 1f;
        float m = config != null ? config.volumenMusica : 0.7f;
        float e = config != null ? config.volumenEfectos : 1f;
        float i = config != null ? config.volumenInterfaz : 1f;

        volGeneral = PlayerPrefs.GetFloat(KeyGeneral, g);
        volMusica = PlayerPrefs.GetFloat(KeyMusica, m);
        volEfectos = PlayerPrefs.GetFloat(KeyEfectos, e);
        volInterfaz = PlayerPrefs.GetFloat(KeyInterfaz, i);
    }

    private void BuildPool()
    {
        for (int i = 0; i < PoolSize; i++)
        {
            var go = new GameObject("SFX_" + i);
            go.transform.SetParent(transform, false);

            var s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.rolloffMode = AudioRolloffMode.Linear;
            pool.Add(s);
        }
    }

    private void BuildMusicSources()
    {
        for (int i = 0; i < music.Length; i++)
        {
            var go = new GameObject("Music_" + i);
            go.transform.SetParent(transform, false);

            var s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.loop = true;
            s.spatialBlend = 0f;
            s.volume = 0f;
            music[i] = s;
        }
    }

    // =========================
    // EFECTOS (API ESTATICA)
    // =========================

    /// <summary>Reproduce un sonido en una posicion del mundo (se atenua con la distancia si es 3D).</summary>
    public static void Play(SoundSlot slot, Vector3 position, AudioCategory category = AudioCategory.Efectos)
    {
        if (Instance == null || slot == null) return;
        Instance.PlayInternal(slot, position, category, slot.sonido3D);
    }

    /// <summary>Reproduce un sonido sin posicion (se oye igual en todos lados).</summary>
    public static void Play2D(SoundSlot slot, AudioCategory category = AudioCategory.Efectos)
    {
        if (Instance == null || slot == null) return;
        Instance.PlayInternal(slot, Vector3.zero, category, false);
    }

    /// <summary>Reproduce uno de los sonidos de interfaz configurados en AudioConfig.</summary>
    public static void PlayUI(UISound sound)
    {
        if (Instance == null || Instance.config == null) return;
        Instance.PlayInternal(Instance.config.Get(sound), Vector3.zero, AudioCategory.Interfaz, false);
    }

    private void PlayInternal(SoundSlot slot, Vector3 position, AudioCategory category, bool spatial)
    {
        if (slot == null) return;
        if (!slot.TryPick(out AudioClip clip, out float vol, out float pitch)) return;

        var s = GetSource();
        s.Stop();
        s.transform.position = position;
        s.clip = clip;
        s.volume = Mathf.Clamp01(vol * Gain(category));
        s.pitch = pitch;
        s.loop = false;
        s.spatialBlend = spatial ? 1f : 0f;
        s.minDistance = config != null ? config.distanciaMinima : 3f;
        s.maxDistance = config != null ? config.distanciaMaxima : 45f;
        s.Play();
    }

    private AudioSource GetSource()
    {
        for (int i = 0; i < pool.Count; i++)
        {
            int idx = (poolCursor + i) % pool.Count;
            if (!pool[idx].isPlaying)
            {
                poolCursor = (idx + 1) % pool.Count;
                return pool[idx];
            }
        }

        // Todos ocupados: se reutiliza el siguiente en turno.
        var stolen = pool[poolCursor];
        poolCursor = (poolCursor + 1) % pool.Count;
        return stolen;
    }

    // =========================
    // VOLUMENES
    // =========================

    public static float GetVolume(AudioCategory category)
    {
        if (Instance == null) return 1f;

        switch (category)
        {
            case AudioCategory.Musica:   return Instance.volMusica;
            case AudioCategory.Efectos:  return Instance.volEfectos;
            case AudioCategory.Interfaz: return Instance.volInterfaz;
            default:                     return Instance.volGeneral;
        }
    }

    public static void SetVolume(AudioCategory category, float value)
    {
        if (Instance == null) return;
        value = Mathf.Clamp01(value);

        switch (category)
        {
            case AudioCategory.Musica:   Instance.volMusica = value;   PlayerPrefs.SetFloat(KeyMusica, value);   break;
            case AudioCategory.Efectos:  Instance.volEfectos = value;  PlayerPrefs.SetFloat(KeyEfectos, value);  break;
            case AudioCategory.Interfaz: Instance.volInterfaz = value; PlayerPrefs.SetFloat(KeyInterfaz, value); break;
            default:                     Instance.volGeneral = value;  PlayerPrefs.SetFloat(KeyGeneral, value);  break;
        }

        PlayerPrefs.Save();
        Instance.OnVolumesChanged?.Invoke();
    }

    /// <summary>Volumen final de una categoria (ya multiplicado por el general). 1 si no hay AudioManager.</summary>
    public static float GainFor(AudioCategory category)
    {
        return Instance == null ? 1f : Instance.Gain(category);
    }

    private float Gain(AudioCategory category)
    {
        switch (category)
        {
            case AudioCategory.Musica:   return volGeneral * volMusica;
            case AudioCategory.Interfaz: return volGeneral * volInterfaz;
            case AudioCategory.General:  return volGeneral;
            default:                     return volGeneral * volEfectos;
        }
    }

    // =========================
    // MUSICA
    // =========================

    /// <summary>
    /// Cambia la musica mientras el "owner" siga activo (por ejemplo una zona del mapa).
    /// Al llamar ClearMusicOverride vuelve la musica de la escena.
    /// </summary>
    public static void SetMusicOverride(object owner, AudioClip clip, float volume = 1f)
    {
        if (Instance == null || owner == null) return;
        Instance.SetOverride(owner, clip, volume);
    }

    public static void ClearMusicOverride(object owner)
    {
        if (Instance == null || owner == null) return;
        Instance.RemoveOverride(owner);
    }

    private void SetOverride(object owner, AudioClip clip, float volume)
    {
        overrides.RemoveAll(o => o.owner == owner);
        overrides.Add(new MusicOverride { owner = owner, clip = clip, volume = volume });
        RefreshMusic();
    }

    private void RemoveOverride(object owner)
    {
        if (overrides.RemoveAll(o => o.owner == owner) > 0)
            RefreshMusic();
    }

    private void RefreshMusic()
    {
        if (overrides.Count > 0)
        {
            var o = overrides[overrides.Count - 1];
            ApplyMusic(o.clip, o.volume);
        }
        else if (sceneTrack != null)
        {
            ApplyMusic(sceneTrack.musica, sceneTrack.volumen);
        }
        else
        {
            ApplyMusic(null, 0f);
        }
    }

    private void ApplyMusic(AudioClip clip, float baseVolume)
    {
        if (clip == currentClip)
        {
            musicTarget[activeMusic] = clip == null ? 0f : baseVolume;
            return;
        }

        currentClip = clip;
        musicTarget[activeMusic] = 0f;          // la cancion actual se apaga suavemente
        if (clip == null) return;

        activeMusic = 1 - activeMusic;          // la nueva entra en la otra fuente
        var s = music[activeMusic];
        s.clip = clip;
        s.volume = 0f;
        s.Play();
        musicTarget[activeMusic] = baseVolume;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single) return;

        overrides.Clear();   // las zonas de la escena anterior ya no existen

        if (config == null || config.musicaPorEscena == null) return;

        foreach (var entry in config.musicaPorEscena)
        {
            if (entry != null && entry.escena == scene.name)
            {
                sceneTrack = entry;
                RefreshMusic();
                return;
            }
        }
        // Escena sin entrada: sigue la musica que ya sonaba.
    }

    // =========================
    // UPDATE
    // =========================

    private void Update()
    {
        UpdateMusicVolumes();

        guardTimer -= Time.unscaledDeltaTime;
        if (guardTimer <= 0f)
        {
            guardTimer = 2f;
            EnsureSingleListener();
        }
    }

    private void UpdateMusicVolumes()
    {
        float seconds = config != null ? Mathf.Max(0.05f, config.segundosDeTransicion) : 2f;
        float step = Time.unscaledDeltaTime / seconds;
        float gain = Gain(AudioCategory.Musica);

        for (int i = 0; i < music.Length; i++)
        {
            var s = music[i];
            float target = musicTarget[i] * gain;
            s.volume = Mathf.MoveTowards(s.volume, target, step);

            if (musicTarget[i] <= 0f && s.volume <= 0.001f && s.isPlaying)
                s.Stop();
        }
    }

    /// <summary>Si hay mas de un AudioListener activo (Unity avisa y el sonido se confunde), deja solo el de la camara principal.</summary>
    private void EnsureSingleListener()
    {
        var listeners = FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
        if (listeners.Length <= 1) return;

        AudioListener keep = null;

        var cam = Camera.main;
        if (cam != null) keep = cam.GetComponent<AudioListener>();

        if (keep == null || !keep.enabled)
        {
            keep = null;
            foreach (var l in listeners)
            {
                if (l.enabled) { keep = l; break; }
            }
        }

        if (keep == null) return;

        foreach (var l in listeners)
        {
            if (l != keep && l.enabled)
            {
                l.enabled = false;
                Debug.Log("[Audio] Habia mas de un AudioListener. Se desactivo el de '" + l.gameObject.name + "'.");
            }
        }
    }
}
