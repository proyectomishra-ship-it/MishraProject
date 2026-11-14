using UnityEngine;

/// <summary>
/// Cambia la musica cuando el jugador local entra en una zona (un pueblo, una cueva, un jefe).
/// Al salir vuelve la musica de la escena. Se coloca en un objeto vacio en el centro de la zona.
/// </summary>
[AddComponentMenu("Mishra Audio/Music Area")]
public class MusicArea : MonoBehaviour
{
    public AudioClip musica;
    [Range(0f, 1f)] public float volumen = 1f;

    [Tooltip("Radio de la zona en metros (se ve como una esfera al seleccionar el objeto).")]
    public float radio = 60f;

    [Tooltip("Distancia extra que hay que alejarse para que se considere que salio de la zona.")]
    public float margenDeSalida = 5f;

    [SerializeField] private float intervaloDeChequeo = 0.25f;

    private bool inside;
    private float timer;
    private PlayerTeleportController localPlayer;

    private void Awake()
    {
        timer = Random.Range(0f, intervaloDeChequeo);
    }

    private void OnDisable()
    {
        if (inside)
        {
            inside = false;
            AudioManager.ClearMusicOverride(this);
        }
    }

    private void Update()
    {
        timer -= Time.deltaTime;
        if (timer > 0f) return;
        timer = intervaloDeChequeo;

        var player = GetLocalPlayer();
        if (player == null) return;

        float sqr = (player.transform.position - transform.position).sqrMagnitude;

        if (!inside)
        {
            if (sqr <= radio * radio)
            {
                inside = true;
                AudioManager.SetMusicOverride(this, musica, volumen);
            }
        }
        else
        {
            float exit = radio + margenDeSalida;
            if (sqr > exit * exit)
            {
                inside = false;
                AudioManager.ClearMusicOverride(this);
            }
        }
    }

    private PlayerTeleportController GetLocalPlayer()
    {
        if (localPlayer != null && localPlayer.IsSpawned && localPlayer.IsOwner)
            return localPlayer;

        localPlayer = null;
        var players = PlayerTeleportController.Active;
        for (int i = 0; i < players.Count; i++)
        {
            var p = players[i];
            if (p != null && p.IsSpawned && p.IsOwner)
            {
                localPlayer = p;
                break;
            }
        }
        return localPlayer;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.6f, 0.9f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, radio);
    }
}
