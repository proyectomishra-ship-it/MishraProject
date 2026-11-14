using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public enum AttackSoundKind : byte { Light = 0, Heavy = 1, Special = 2 }

/// <summary>
/// Todos los sonidos de UN personaje (jugador o enemigo) en un solo componente.
/// Se agrega al prefab (Warrior, Goblin, Slime...) y se rellena arrastrando audios.
///
/// Automatico (no hay que programar nada):
///   - Recibir dano, curarse, morir y reaparecer  (a partir de la vida replicada)
///   - Pasos, salto y aterrizaje                  (a partir del movimiento)
///   - Ataques de jugadores                       (a partir de la animacion)
///   - Ataques de enemigos                        (los avisa CombatController)
/// Todo se oye en TODOS los jugadores, porque cada uno lo reproduce al ver el cambio.
/// </summary>
[AddComponentMenu("Mishra Audio/Character Audio")]
[DisallowMultipleComponent]
public class CharacterAudio : NetworkBehaviour
{
    [Serializable]
    public class StateSound
    {
        [Tooltip("Nombre EXACTO del estado en el Animator (ej: Attack1). " +
                 "Para ver la lista: menu de los 3 puntos del componente > 'Listar estados del Animator'.")]
        public string estado;
        public SoundSlot sonido = new SoundSlot();
        [Tooltip("Segundos de espera desde que empieza la animacion hasta que suena (para que coincida con el golpe).")]
        public float retraso = 0f;
    }

    [Serializable]
    public class NamedSound
    {
        public string nombre;
        public SoundSlot sonido = new SoundSlot();
    }

    // ── ATAQUES ──────────────────────────────────────────────────────────────
    [Header("ATAQUES")]
    public SoundSlot ataqueLigero = new SoundSlot();
    public SoundSlot ataquePesado = new SoundSlot();
    public SoundSlot ataqueEspecial = new SoundSlot();

    [Tooltip("JUGADORES: dejar activado (detecta el ataque desde la animacion). " +
             "ENEMIGOS: desactivar; sus ataques los avisa el CombatController.")]
    public bool ataquesDesdeAnimacion = true;

    // ── VIDA ─────────────────────────────────────────────────────────────────
    [Header("VIDA (automatico)")]
    [Tooltip("Al perder vida.")] public SoundSlot recibeDano = new SoundSlot();
    [Tooltip("Al recuperar bastante vida de golpe (pociones, curaciones).")] public SoundSlot curacion = new SoundSlot();
    [Tooltip("Cuando la vida llega a 0.")] public SoundSlot muerte = new SoundSlot();
    [Tooltip("Cuando vuelve a la vida despues de morir.")] public SoundSlot reaparece = new SoundSlot();

    [Tooltip("La curacion solo suena si recupera al menos este porcentaje de la vida maxima " +
             "(evita que suene con la regeneracion lenta).")]
    [Range(0f, 1f)] public float curacionMinima = 0.15f;

    // ── MOVIMIENTO ───────────────────────────────────────────────────────────
    [Header("MOVIMIENTO (automatico)")]
    public SoundSlot paso = new SoundSlot();
    public SoundSlot salto = new SoundSlot();
    public SoundSlot aterrizaje = new SoundSlot();

    [Tooltip("Metros que recorre entre un paso y el siguiente.")]
    public float distanciaEntrePasos = 1.8f;
    [Tooltip("Velocidad minima (m/s) para que cuenten los pasos.")]
    public float velocidadMinima = 0.6f;
    [Tooltip("Que tan cerca del suelo tiene que estar para considerarse apoyado.")]
    public float distanciaAlSuelo = 0.35f;
    public LayerMask capasDeSuelo = ~0;

    // ── EXTRA ────────────────────────────────────────────────────────────────
    [Header("OTROS SONIDOS SEGUN LA ANIMACION")]
    [Tooltip("Para cualquier otro estado del Animator (ej: un rugido, un hechizo).")]
    public StateSound[] porEstadoDeAnimacion;

    [Header("SONIDOS CON NOMBRE (para eventos de animacion o botones)")]
    [Tooltip("Se reproducen llamando PlaySound con el nombre.")]
    public NamedSound[] conNombre;

    [Header("AVANZADO")]
    [Tooltip("Vacio = se busca solo en el modelo.")]
    [SerializeField] private Animator animator;
    [SerializeField] private float ignorarSegundosAlInicio = 0.75f;

    // ── Interno ──────────────────────────────────────────────────────────────
    private struct Binding
    {
        public int hash;
        public SoundSlot slot;
        public float delay;
    }

    private CharacterStatsSyncController sync;
    private Animator anim;
    private readonly List<Binding> bindings = new List<Binding>();
    private int[] lastHash;
    private float[] lastNorm;
    private float animRetryTimer;

    private float spawnTime;
    private bool dead;

    private Vector3 lastPos;
    private bool wasGrounded = true;
    private float airTime;
    private float stepAccum;

    // =========================
    // RED
    // =========================

    public override void OnNetworkSpawn()
    {
        // Servidor dedicado sin pantalla ni parlantes: no hace falta nada.
        if (IsServer && !IsClient)
        {
            enabled = false;
            return;
        }

        spawnTime = Time.time;
        lastPos = transform.position;

        sync = GetComponent<CharacterStatsSyncController>();
        if (sync == null) sync = GetComponentInParent<CharacterStatsSyncController>();

        if (sync != null)
        {
            dead = sync.NetHealth.Value <= 0.001f;
            sync.NetHealth.OnValueChanged += OnHealthChanged;
        }

        BuildBindings();
    }

    public override void OnNetworkDespawn()
    {
        if (sync != null)
            sync.NetHealth.OnValueChanged -= OnHealthChanged;
    }

    // =========================
    // VIDA
    // =========================

    private void OnHealthChanged(float previous, float current)
    {
        if (Time.time - spawnTime < ignorarSegundosAlInicio)
        {
            dead = current <= 0.001f;
            return;
        }

        bool wasAlive = previous > 0.001f;
        bool isAlive = current > 0.001f;

        if (wasAlive && !isAlive)
        {
            dead = true;
            PlayLocal(muerte);
            return;
        }

        if (!wasAlive && isAlive)
        {
            dead = false;
            PlayLocal(reaparece);
            return;
        }

        if (current < previous)
        {
            PlayLocal(recibeDano);
        }
        else if (current > previous)
        {
            float max = sync != null ? sync.NetMaxHealth.Value : 0f;
            if (max > 0f && (current - previous) / max >= curacionMinima)
                PlayLocal(curacion);
        }
    }

    // =========================
    // ATAQUES DE ENEMIGOS (los llama CombatController en el servidor)
    // =========================

    /// <summary>Solo servidor: hace sonar el ataque en todos los clientes.</summary>
    public void PlayAttackOnAllClients(AttackSoundKind kind)
    {
        if (!IsServer) return;
        PlayAttackClientRpc((byte)kind);
    }

    [ClientRpc]
    private void PlayAttackClientRpc(byte kind)
    {
        switch ((AttackSoundKind)kind)
        {
            case AttackSoundKind.Heavy:   PlayLocal(ataquePesado);   break;
            case AttackSoundKind.Special: PlayLocal(ataqueEspecial); break;
            default:                      PlayLocal(ataqueLigero);   break;
        }
    }

    // =========================
    // API PUBLICA SENCILLA
    // =========================

    /// <summary>Reproduce un sonido de la lista "Sonidos con nombre". Pensado para eventos de animacion.</summary>
    public void PlaySound(string nombre)
    {
        if (conNombre == null) return;

        for (int i = 0; i < conNombre.Length; i++)
        {
            var n = conNombre[i];
            if (n != null && string.Equals(n.nombre, nombre, StringComparison.OrdinalIgnoreCase))
            {
                PlayLocal(n.sonido);
                return;
            }
        }

        Debug.LogWarning("[Audio] '" + name + "' no tiene un sonido con nombre '" + nombre + "'.", this);
    }

    private void PlayLocal(SoundSlot slot)
    {
        if (slot == null) return;
        AudioManager.Play(slot, transform.position + Vector3.up);
    }

    // =========================
    // ANIMATOR
    // =========================

    private void BuildBindings()
    {
        bindings.Clear();

        if (ataquesDesdeAnimacion)
        {
            AddBinding(PlayerAnimatorParams.AttackLight, ataqueLigero, 0f);
            AddBinding(PlayerAnimatorParams.AttackHeavy, ataquePesado, 0f);
            AddBinding(PlayerAnimatorParams.AttackSpecial, ataqueEspecial, 0f);
        }

        if (porEstadoDeAnimacion != null)
        {
            for (int i = 0; i < porEstadoDeAnimacion.Length; i++)
            {
                var e = porEstadoDeAnimacion[i];
                if (e != null && !string.IsNullOrEmpty(e.estado))
                    AddBinding(e.estado, e.sonido, e.retraso);
            }
        }
    }

    private void AddBinding(string stateName, SoundSlot slot, float delay)
    {
        if (slot == null) return;
        bindings.Add(new Binding { hash = Animator.StringToHash(stateName), slot = slot, delay = delay });
    }

    private static void ReadState(Animator a, int layer, out int hash, out float normalized)
    {
        var info = a.IsInTransition(layer)
            ? a.GetNextAnimatorStateInfo(layer)
            : a.GetCurrentAnimatorStateInfo(layer);

        hash = info.shortNameHash;
        normalized = info.normalizedTime;
    }

    private void PollAnimator()
    {
        if (bindings.Count == 0) return;

        if (anim == null)
        {
            animRetryTimer -= Time.deltaTime;
            if (animRetryTimer > 0f) return;
            animRetryTimer = 1f;

            anim = animator != null ? animator : GetComponentInChildren<Animator>(true);
            if (anim == null) return;
        }

        if (!anim.isActiveAndEnabled || anim.runtimeAnimatorController == null) return;

        int layers = anim.layerCount;

        // Primera lectura: solo memoriza el estado actual, sin sonar.
        if (lastHash == null || lastHash.Length != layers)
        {
            lastHash = new int[layers];
            lastNorm = new float[layers];
            for (int l = 0; l < layers; l++)
                ReadState(anim, l, out lastHash[l], out lastNorm[l]);
            return;
        }

        for (int l = 0; l < layers; l++)
        {
            ReadState(anim, l, out int hash, out float norm);

            // Entro a un estado nuevo, o repitio el mismo (ataque encadenado que reinicia la animacion).
            bool entered = hash != lastHash[l] || norm + 0.2f < lastNorm[l];

            lastHash[l] = hash;
            lastNorm[l] = norm;

            if (!entered) continue;

            for (int i = 0; i < bindings.Count; i++)
            {
                if (bindings[i].hash == hash)
                    Trigger(bindings[i]);
            }
        }
    }

    private void Trigger(Binding b)
    {
        if (b.delay <= 0f) PlayLocal(b.slot);
        else StartCoroutine(PlayDelayed(b.slot, b.delay));
    }

    private IEnumerator PlayDelayed(SoundSlot slot, float seconds)
    {
        yield return new WaitForSeconds(seconds);
        PlayLocal(slot);
    }

    // =========================
    // MOVIMIENTO
    // =========================

    private void PollMovement()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        Vector3 pos = transform.position;
        Vector3 delta = pos - lastPos;
        lastPos = pos;

        // Un salto enorme de posicion en un cuadro es un teletransporte o reaparicion: no cuenta.
        if (delta.sqrMagnitude > 25f)
        {
            stepAccum = 0f;
            return;
        }

        if (dead || Time.time - spawnTime < ignorarSegundosAlInicio)
        {
            wasGrounded = IsGrounded();
            return;
        }

        bool grounded = IsGrounded();
        float horizontal = new Vector2(delta.x, delta.z).magnitude;

        if (grounded)
        {
            if (!wasGrounded && airTime > 0.2f)
                PlayLocal(aterrizaje);

            airTime = 0f;

            if (horizontal / dt >= velocidadMinima)
            {
                stepAccum += horizontal;
                if (stepAccum >= distanciaEntrePasos)
                {
                    stepAccum = 0f;
                    PlayLocal(paso);
                }
            }
        }
        else
        {
            airTime += dt;
            stepAccum = 0f;

            // Se despego del suelo subiendo: es un salto (y no caer de un borde).
            if (wasGrounded && delta.y / dt > 1.5f)
                PlayLocal(salto);
        }

        wasGrounded = grounded;
    }

    private bool IsGrounded()
    {
        Vector3 origin = transform.position + Vector3.up * 0.15f;
        return Physics.Raycast(origin, Vector3.down, distanciaAlSuelo + 0.15f,
                               capasDeSuelo, QueryTriggerInteraction.Ignore);
    }

    // =========================
    // UPDATE
    // =========================

    private void Update()
    {
        if (!IsSpawned) return;

        PollAnimator();
        PollMovement();
    }

#if UNITY_EDITOR
    [ContextMenu("Listar estados del Animator")]
    private void ListarEstados()
    {
        var a = animator != null ? animator : GetComponentInChildren<Animator>(true);
        if (a == null || a.runtimeAnimatorController == null)
        {
            Debug.Log("[Audio] No encontre un Animator con controlador en este objeto. " +
                      "Los estados se pueden ver abriendo el Animator (ventana Window > Animation > Animator).");
            return;
        }

        RuntimeAnimatorController rc = a.runtimeAnimatorController;
        while (rc is AnimatorOverrideController ov && ov.runtimeAnimatorController != null)
            rc = ov.runtimeAnimatorController;

        var ctrl = rc as UnityEditor.Animations.AnimatorController;
        if (ctrl == null)
        {
            Debug.Log("[Audio] El controlador del Animator no se puede leer desde aqui.");
            return;
        }

        var sb = new System.Text.StringBuilder("[Audio] Estados del Animator:\n");
        foreach (var layer in ctrl.layers)
            AppendStates(layer.stateMachine, sb);

        Debug.Log(sb.ToString());
    }

    private static void AppendStates(UnityEditor.Animations.AnimatorStateMachine machine, System.Text.StringBuilder sb)
    {
        foreach (var s in machine.states)
            sb.AppendLine("  - " + s.state.name);

        foreach (var sub in machine.stateMachines)
            AppendStates(sub.stateMachine, sb);
    }
#endif
}
