using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Único mecanismo que decide qué enemigos están activos. Funciona igual en el
/// overworld y en las cuevas: un enemigo está DESPIERTO si hay algún jugador
/// cerca y DORMIDO si no. No depende de la "zona" donde esté, así que cambiar
/// de ubicación (portales) no puede dejar a un grupo en un estado equivocado.
///
/// DORMIDO DESDE EL NACIMIENTO:
///   - Si todavía no hay jugadores spawneados, todos los enemigos cuentan como
///     "lejos" y duermen (parámetro sleepWhenNoPlayers).
///   - Un enemigo recién spawneado cuya IA todavía se está inicializando no se
///     duerme a medias: se espera a que termine (unos frames) y se lo duerme
///     enseguida. Sin esto, InitializeAI() terminaba DESPUÉS de dormirlo, entraba
///     en Patrol y el NavMeshAgent empezaba a caminar con la IA "apagada".
///   - Cuando aparece o se va un jugador se evalúa de inmediato.
///
/// QUÉ AHORRA UN ENEMIGO DORMIDO:
///   - EnemyAIController apagado: sin máquina de estados ni percepción. La
///     percepción hace un Physics.OverlapSphere por frame y reserva un array
///     nuevo cada vez, así que es lo más caro por enemigo.
///   - NavMeshAgent desactivado (disableNavMeshAgentWhileAsleep): sin simulación
///     de agente. Al despertar se reactiva y, si quedó fuera del NavMesh, se re-ubica.
///
/// QUÉ SIGUE CORRIENDO (a propósito):
///   - CharacterRegenerationController: un enemigo herido que se duerme sigue
///     curándose. Es barato (un tick cada 0.25 s) y evita el truco de pegar y huir.
///   - Colliders y renderers: sin movimiento no cuestan casi nada, y Unity no dibuja lo que está fuera de cámara.
///
/// Por qué es robusto ante cambios de ubicación repetidos:
///   1. El estado deseado se recalcula desde cero en cada evaluación (distancia al
///      jugador más cercano). No hay contadores ni eventos "entró/salió".
///   2. Cada teletransporte pide una evaluación inmediata (RequestEvaluation).
///   3. Idempotente: si algo reactiva a un dormido, se lo vuelve a dormir.
///   4. Al dormir se sale de Chase/Attack/Flee ANTES de apagar, para que las
///      AttackState liberen sus slots de CombatSlotManager (usan CurrentTarget).
///   5. Los jugadores se leen de NetworkManager.ConnectedClients (servidor), no de
///      un componente del prefab.
///   6. Si el manager se apaga o destruye, despierta a todos.
///
/// SETUP: UN solo GameObject vacío en la escena con este componente. No necesita
/// NetworkObject. Solo actúa en el servidor.
///
/// REGLAS DE DISTANCIA:
///   - wakeDistance debe ser MAYOR que el rango de detección, alerta y ataque de
///     cualquier enemigo (hoy alerta = 15 m), más un margen.
///   - Cada ubicación debe estar a más de sleepDistance + el tamaño de la zona de
///     las demás; si no, sus enemigos se mantienen despiertos entre sí.
///   - Un enemigo visible a lo lejos (desde una colina) queda congelado si supera
///     sleepDistance. Si eso se nota, subí las distancias.
/// </summary>
public class EnemySleepManager : MonoBehaviour
{
    public static EnemySleepManager Instance { get; private set; }

    [Header("Distancias (metros)")]
    [Tooltip("Más lejos que esto del jugador más cercano, el enemigo se duerme.")]
    [SerializeField] private float sleepDistance = 80f;
    [Tooltip("Más cerca que esto, el enemigo despierta. Menor que sleepDistance para evitar parpadeo. " +
             "Debe superar el rango de detección/alerta/ataque de los enemigos.")]
    [SerializeField] private float wakeDistance = 60f;

    [Header("Arranque")]
    [Tooltip("Sin jugadores spawneados, todos los enemigos duermen (arranque de sesión, o todos desconectados).")]
    [SerializeField] private bool sleepWhenNoPlayers = true;
    [Tooltip("Segundos máximos que se espera a que termine la inicialización de la IA de un enemigo recién " +
             "spawneado antes de dormirlo igual (por si su IA nunca termina de inicializar).")]
    [SerializeField] private float initGraceSeconds = 2f;

    [Header("Ahorro")]
    [Tooltip("Además de apagar la IA, desactiva el NavMeshAgent mientras duerme.")]
    [SerializeField] private bool disableNavMeshAgentWhileAsleep = true;

    [Header("Frecuencia")]
    [SerializeField] private float checkInterval = 0.5f;

    [Header("Debug")]
    [Tooltip("Muestra contadores en pantalla y loguea cuando cambian.")]
    [SerializeField] private bool debugInfo;

    // enemigo dormido -> si su NavMeshAgent ya estaba detenido al dormirlo
    private readonly Dictionary<EnemyAIController, bool> sleeping = new();
    // enemigo que quiere dormir pero su IA todavía se inicializa -> desde cuándo
    private readonly Dictionary<EnemyAIController, float> pendingSince = new();
    private readonly List<EnemyAIController> toRemove = new();
    private readonly List<Vector3> playerPositions = new();

    private float timer;
    private bool evaluateRequested;
    private int lastAwake = -1;
    private int lastAsleep = -1;
    private int lastPlayers;
    private int lastPending;

    /// <summary>
    /// Pide una evaluación al final del frame actual. Se puede llamar varias
    /// veces en el mismo frame (ej. dos jugadores cruzando un portal): se
    /// evalúa una sola vez, ya con todas las posiciones actualizadas.
    /// </summary>
    public static void RequestEvaluation()
    {
        if (Instance != null) Instance.evaluateRequested = true;
    }

    private void OnValidate()
    {
        if (wakeDistance > sleepDistance) wakeDistance = sleepDistance;
        if (checkInterval < 0.1f) checkInterval = 0.1f;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
            Debug.LogWarning("[EnemySleep] Hay más de un EnemySleepManager en la escena. Dejá solo uno.", this);

        Instance = this;

        // Primera evaluación en el primer LateUpdate: los enemigos nacen dormidos.
        evaluateRequested = true;
    }

    private void OnDisable()
    {
        WakeAll();
    }

    private void OnDestroy()
    {
        WakeAll();
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        timer -= Time.deltaTime;
        if (timer > 0f) return;

        timer = checkInterval;
        evaluateRequested = true;
    }

    // LateUpdate: corre después de los callbacks de física (donde se disparan
    // los portales) y de los Update de la IA, con las posiciones ya finales.
    private void LateUpdate()
    {
        if (!evaluateRequested) return;
        evaluateRequested = false;
        Evaluate();
    }

    private void Evaluate()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsServer) return;

        // Jugadores reales, leídos desde el servidor.
        playerPositions.Clear();
        foreach (var client in nm.ConnectedClients.Values)
        {
            var po = client.PlayerObject;
            if (po != null && po.IsSpawned)
                playerPositions.Add(po.transform.position);
        }

        lastPlayers = playerPositions.Count;

        bool noPlayers = playerPositions.Count == 0;
        if (noPlayers && !sleepWhenNoPlayers) return;

        float sleepSqr = sleepDistance * sleepDistance;
        float wakeSqr = wakeDistance * wakeDistance;

        var enemies = FindObjectsByType<EnemyAIController>(FindObjectsSortMode.None);
        int total = 0;
        bool deferred = false;

        foreach (var ai in enemies)
        {
            if (ai == null || !ai.IsSpawned) continue;
            total++;

            // Sin jugadores: todos están "infinitamente lejos".
            float d = noPlayers ? float.MaxValue : NearestSqrDistance(ai.transform.position);

            if (sleeping.ContainsKey(ai))
            {
                if (d <= wakeSqr)
                    Wake(ai);
                else
                    EnforceSleep(ai); // alguien lo reactivó por fuera: volver a dormirlo
            }
            else if (d > sleepSqr)
            {
                if (IsReadyToSleep(ai))
                    Sleep(ai);
                else
                    deferred = true; // IA inicializando: reintentar el próximo frame
            }
            else
            {
                pendingSince.Remove(ai); // volvió a estar cerca: ya no quiere dormir
            }
        }

        CleanUp();

        // Mientras haya enemigos esperando a terminar de inicializar, se reevalúa
        // cada frame (son unos pocos frames). Si no, el intervalo normal alcanza.
        if (deferred) evaluateRequested = true;

        int asleep = sleeping.Count;
        int awake = Mathf.Max(0, total - asleep);
        lastPending = pendingSince.Count;

        if (debugInfo && (asleep != lastAsleep || awake != lastAwake))
            Debug.Log($"[EnemySleep] dormidos: {asleep} | despiertos: {awake} | jugadores: {lastPlayers}");

        lastAsleep = asleep;
        lastAwake = awake;
    }

    private float NearestSqrDistance(Vector3 pos)
    {
        float best = float.MaxValue;

        for (int i = 0; i < playerPositions.Count; i++)
        {
            float d = (playerPositions[i] - pos).sqrMagnitude;
            if (d < best) best = d;
        }

        return best;
    }

    /// <summary>
    /// true si la IA ya terminó de inicializarse (StateMachine creada) o si pasó
    /// el tiempo de gracia. Dormir antes de que InitializeAI() termine deja al
    /// agente caminando: esa corrutina sigue aunque el componente esté apagado.
    /// </summary>
    private bool IsReadyToSleep(EnemyAIController ai)
    {
        if (ai.StateMachine != null) return true;

        if (!pendingSince.TryGetValue(ai, out float since))
        {
            pendingSince[ai] = Time.time;
            return false;
        }

        return Time.time - since >= initGraceSeconds;
    }

    private void Sleep(EnemyAIController ai)
    {
        pendingSince.Remove(ai);

        // Primero salir del combate (libera slots), con el agente todavía activo.
        ResetAggro(ai);

        bool wasStopped = false;
        var agent = ai.Agent;

        if (agent != null && agent.enabled)
        {
            if (agent.isOnNavMesh)
            {
                wasStopped = agent.isStopped;
                agent.isStopped = true;
                agent.velocity = Vector3.zero;
                if (agent.hasPath) agent.ResetPath();
            }

            if (disableNavMeshAgentWhileAsleep)
                agent.enabled = false;
        }

        ai.enabled = false;
        sleeping[ai] = wasStopped;
    }

    /// <summary>
    /// Saca al enemigo de un estado de combate de forma limpia.
    /// ORDEN IMPORTANTE: primero ChangeState (los OnExit de las AttackState
    /// liberan sus slots usando CurrentTarget) y recién después se limpia el target.
    /// El jefe (DemiGod) se congela sin resetear, para no romper su lógica de fases.
    /// </summary>
    private static void ResetAggro(EnemyAIController ai)
    {
        if (ai is DemiGodAIController) return;

        var sm = ai.StateMachine;
        if (sm == null) return; // IA sin inicializar

        var current = sm.CurrentState;
        bool inCombat = current is EnemyStateAttack
                     || current is EnemyStateChase
                     || current is EnemyStateFlee;

        if (!inCombat) return;

        EnemyState rest = ai.HasPatrolPoints ? (EnemyState)ai.PatrolState : ai.IdleState;
        if (rest != null) sm.ChangeState(rest);

        ai.SetTarget(null);
        ai.SetAlerted(false);
    }

    // Reaplica el estado dormido si algo lo deshizo (IA reactivada, agente reactivado).
    private void EnforceSleep(EnemyAIController ai)
    {
        var agent = ai.Agent;

        if (agent != null && agent.enabled)
        {
            if (agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.velocity = Vector3.zero;
                if (agent.hasPath) agent.ResetPath();
            }

            if (disableNavMeshAgentWhileAsleep)
                agent.enabled = false;
        }

        if (ai.enabled) ai.enabled = false;
    }

    private void Wake(EnemyAIController ai)
    {
        bool wasStopped = sleeping[ai];
        sleeping.Remove(ai);

        RestoreAgent(ai, wasStopped);
        ai.enabled = true;
    }

    private static void RestoreAgent(EnemyAIController ai, bool wasStopped)
    {
        var agent = ai.Agent;
        if (agent == null) return;

        if (!agent.enabled) agent.enabled = true;

        // Si por cualquier motivo quedó fuera del NavMesh, lo re-ubicamos
        // (si no, el enemigo despertaría sin poder moverse).
        if (!agent.isOnNavMesh)
        {
            if (NavMesh.SamplePosition(ai.transform.position, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            {
                agent.Warp(hit.position);
            }
            else
            {
                Debug.LogWarning($"[EnemySleep] {ai.name} despertó fuera del NavMesh (a más de 2 m).", ai);
                return;
            }
        }

        agent.isStopped = wasStopped;
    }

    private void WakeAll()
    {
        foreach (var kv in sleeping)
        {
            var ai = kv.Key;
            if (ai == null) continue;

            RestoreAgent(ai, kv.Value);
            ai.enabled = true;
        }

        sleeping.Clear();
        pendingSince.Clear();
    }

    // Quita enemigos que murieron/despawnearon mientras dormían o esperaban.
    private void CleanUp()
    {
        toRemove.Clear();
        foreach (var kv in sleeping)
            if (kv.Key == null || !kv.Key.IsSpawned)
                toRemove.Add(kv.Key);
        foreach (var key in toRemove)
            sleeping.Remove(key);

        toRemove.Clear();
        foreach (var kv in pendingSince)
            if (kv.Key == null || !kv.Key.IsSpawned)
                toRemove.Add(kv.Key);
        foreach (var key in toRemove)
            pendingSince.Remove(key);
    }

    private void OnGUI()
    {
        if (!debugInfo) return;
        GUI.Label(new Rect(10, 10, 520, 24),
            $"Enemigos dormidos: {sleeping.Count} | despiertos: {Mathf.Max(0, lastAwake)} | " +
            $"inicializando: {lastPending} | jugadores: {lastPlayers}");
    }
}
