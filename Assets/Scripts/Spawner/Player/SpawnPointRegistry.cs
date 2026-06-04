using UnityEngine;

/// <summary>
/// Vive en Scene1. Registra los puntos de spawn para que
/// ClassAwareNetworkBootstrap (que está en DontDestroyOnLoad junto
/// al NetworkManager) pueda accederlos de forma segura.
///
/// SETUP EN UNITY:
///   1. Crear un GameObject vacío en Scene1 → llamarlo "SpawnPointRegistry"
///   2. Agregarle este script
///   3. En el Inspector, asignar todos tus SpawnPoints al array
///   4. Listo — ClassAwareNetworkBootstrap lo encontrará automáticamente
///
/// NOTA: Este componente NO usa DontDestroyOnLoad a propósito.
///   Vive solo mientras Scene1 esté cargada. Al descargarse, la
///   referencia estática se limpia sola en OnDestroy.
/// </summary>
public class SpawnPointRegistry : MonoBehaviour
{
    // -------------------------------------------------------
    // Acceso global (solo válido mientras Scene1 está cargada)
    // -------------------------------------------------------
    public static SpawnPointRegistry Instance { get; private set; }

    [Header("Puntos de spawn — asignar en el Inspector de Scene1")]
    [SerializeField] private Transform[] spawnPoints;

    private int currentIndex = 0;

    // -------------------------------------------------------
    // Ciclo de vida
    // -------------------------------------------------------

    private void Awake()
    {
        // Si por alguna razón hubiese una instancia previa (re-carga de escena),
        // la nueva toma el lugar sin problema.
        Instance = this;

        int count = spawnPoints != null ? spawnPoints.Length : 0;
        Debug.Log($"[SpawnPointRegistry] Registrado con {count} punto(s) de spawn.");

        ValidatePoints();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
            Debug.Log("[SpawnPointRegistry] Instancia limpiada (Scene1 descargada).");
        }
    }

    // -------------------------------------------------------
    // API pública
    // -------------------------------------------------------

    /// <summary>
    /// Devuelve la siguiente posición de spawn en modo round-robin.
    /// Salta automáticamente cualquier referencia nula en el array.
    /// </summary>
    public Vector3 GetNextPosition()
    {
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogWarning("[SpawnPointRegistry] El array de spawn points está vacío. " +
                             "Asignalo en el Inspector del GameObject SpawnPointRegistry en Scene1.");
            return Fallback();
        }

        // Intentar encontrar un Transform válido en hasta N iteraciones
        // para saltar slots nulos sin colgarse en un loop infinito.
        for (int attempts = 0; attempts < spawnPoints.Length; attempts++)
        {
            int idx = currentIndex % spawnPoints.Length;
            currentIndex++;

            Transform sp = spawnPoints[idx];
            if (sp != null)
                return sp.position;

            Debug.LogWarning($"[SpawnPointRegistry] SpawnPoint[{idx}] es null, saltando al siguiente.");
        }

        // Todos los slots eran null
        Debug.LogError("[SpawnPointRegistry] Todos los spawn points son null. " +
                       "Verificá las asignaciones en el Inspector.");
        return Fallback();
    }

    /// <summary>
    /// Reinicia el índice round-robin (útil si la partida se reinicia).
    /// </summary>
    public void ResetIndex() => currentIndex = 0;

    /// <summary>
    /// Cantidad de puntos de spawn válidos (no null) actualmente registrados.
    /// </summary>
    public int ValidCount
    {
        get
        {
            if (spawnPoints == null) return 0;
            int c = 0;
            foreach (var sp in spawnPoints)
                if (sp != null) c++;
            return c;
        }
    }

    // -------------------------------------------------------
    // Helpers privados
    // -------------------------------------------------------

    /// <summary>
    /// Posición aleatoria de último recurso. Logea un warning visible.
    /// </summary>
    private Vector3 Fallback()
    {
        Vector3 pos = new Vector3(Random.Range(-5f, 5f), 1f, Random.Range(-5f, 5f));
        Debug.LogWarning($"[SpawnPointRegistry] Usando posición de fallback: {pos}");
        return pos;
    }

    /// <summary>
    /// Avisa en consola si algún slot del array quedó sin asignar.
    /// Solo corre una vez en Awake para detectar errores de setup.
    /// </summary>
    private void ValidatePoints()
    {
        if (spawnPoints == null)
        {
            Debug.LogError("[SpawnPointRegistry] El array spawnPoints es null. " +
                           "Inicializalo y asigná los puntos en el Inspector.");
            return;
        }

        for (int i = 0; i < spawnPoints.Length; i++)
        {
            if (spawnPoints[i] == null)
                Debug.LogError($"[SpawnPointRegistry] SpawnPoint[{i}] no está asignado. " +
                               $"Asignalo en el Inspector o eliminá el slot vacío.");
        }
    }
}