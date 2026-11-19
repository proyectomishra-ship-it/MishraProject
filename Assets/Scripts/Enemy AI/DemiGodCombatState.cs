using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Unity.Netcode;

/// <summary>
/// Combate completo del DemiGod (reemplaza a DemiGodPhase1/2/3State). Solo servidor.
///
/// El boss alterna entre dos POSTURAS:
///   - A distancia: mantiene la distancia, esquiva de costado y usa hechizos (Fireball, Orb, Volley, Meteors, Summon).
///   - Cuerpo a cuerpo: se planta frente al jugador y usa Strike y Slam.
/// Al cambiar de postura usa un movimiento de TRANSICIÓN que cierra la distancia (embestida) o la abre
/// (golpe de área que empuja). Además reacciona a cómo juegues: si te pegas a él mientras dispara, te castiga;
/// si te alejas mientras pelea cuerpo a cuerpo, embiste o dispara.
///
/// Todo ataque tiene aviso en el suelo (carril o círculo) y daño retardado: se esquiva moviéndose.
/// Las fases (por vida, en DemiGodAIController) cambian los movimientos disponibles y acortan los tiempos.
/// </summary>
public class DemiGodCombatState : EnemyStateAttack
{
    private enum Stance { Ranged, Melee }

    private readonly DemiGodAIController boss;

    private CharacterAnimationController anim;
    private CharacterAudio audioFx;
    private TargetingController targeting;
    private NavMeshAgent agent;

    // Postura
    private Stance stance;
    private float stanceTimer;
    private float stanceDuration;
    private Stance? stanceAfterMove;

    // Movimientos
    private bool busy;
    private Coroutine routine;
    private DemiGodMoveSettings lastMove;
    private float nextMoveTime;
    private float panicReadyTime;
    private float farTimer;
    private bool openerPending;

    // Desplazamiento
    private float nextPathTime;
    private float strafeTimer;
    private int strafeSign = 1;

    // Respaldo del agente (se restaura al salir)
    private bool prevUpdateRotation;
    private float prevStopping;
    private bool chargeActive;
    private float chargeSavedSpeed;
    private float chargeSavedAccel;

    private readonly List<DemiGodMoveSettings> candidates = new List<DemiGodMoveSettings>();
    private readonly List<Player> playerBuffer = new List<Player>();

    private DemiGodCombatSettings Settings => boss.Combat;
    private float Tempo => Settings.Tempo(boss.CurrentPhase);

    public DemiGodCombatState(Enemy enemy, DemiGodAIController ai)
        : base(enemy, ai, attackCooldown: 1f)
    {
        boss = ai;
    }

    // =========================================================
    // CICLO DE VIDA
    // =========================================================

    public override void OnEnter()
    {
        base.OnEnter();

        if (!enemy.IsServer)
            return;

        anim = enemy.GetComponent<CharacterAnimationController>();
        audioFx = enemy.GetComponent<CharacterAudio>();
        targeting = enemy.GetComponent<TargetingController>();
        agent = ai.Agent;

        if (agent != null)
        {
            prevUpdateRotation = agent.updateRotation;
            prevStopping = agent.stoppingDistance;

            // El boss siempre mira al jugador, aunque retroceda o se desplace de costado.
            agent.updateRotation = false;
            agent.isStopped = false;
        }

        busy = false;
        routine = null;
        stanceAfterMove = null;
        nextMoveTime = Time.time + 0.4f;

        Character target = ai.CurrentTarget;
        float dist = target != null ? HorizontalDistance(target) : 0f;

        BeginStance(dist <= Settings.rangedMinDistance + 2f ? Stance.Melee : Stance.Ranged, false);
    }

    public override void OnExit()
    {
        if (routine != null)
        {
            ai.StopCoroutine(routine);
            routine = null;
        }

        busy = false;

        if (agent != null)
        {
            if (chargeActive)
            {
                agent.speed = chargeSavedSpeed;
                agent.acceleration = chargeSavedAccel;
                chargeActive = false;
            }

            agent.updateRotation = prevUpdateRotation;
            agent.stoppingDistance = prevStopping;

            if (agent.isOnNavMesh)
                agent.ResetPath();
        }
    }

    /// <summary>Lo llama el controlador al pasar de fase: el boss abre con un golpe de área.</summary>
    public void OnPhaseChanged()
    {
        openerPending = true;
    }

    protected override void PerformAttack() { }

    // =========================================================
    // BUCLE PRINCIPAL
    // =========================================================

    public override void OnUpdate()
    {
        if (!enemy.IsServer)
            return;

        Character target = ValidateTarget();

        if (target == null)
            return;

        float dist = HorizontalDistance(target);

        if (dist > Settings.leashDistance)
        {
            ai.StateMachine.ChangeState(ai.ChaseState);
            return;
        }

        targeting?.ForceTarget(target);

        // Mientras ejecuta un movimiento, la propia rutina se encarga de mirar y moverse.
        if (busy)
            return;

        FaceTarget(target, 240f);

        // --- Apertura al cambiar de fase ---
        if (openerPending)
        {
            openerPending = false;

            DemiGodMoveSettings slam = FindMove(DemiGodMoveType.Slam);

            if (slam != null)
            {
                Begin(slam, Stance.Ranged);
                return;
            }
        }

        stanceTimer += Time.deltaTime;

        // --- Fin de la postura: transición ---
        if (stanceTimer >= stanceDuration)
        {
            StartTransition(dist);
            return;
        }

        UpdateReactions(target, dist);

        if (busy)
            return;

        // --- Elegir el siguiente movimiento (con una pausa entre uno y otro) ---
        if (Time.time >= nextMoveTime)
        {
            DemiGodMoveSettings move = PickMove(stance, dist);

            if (move != null)
            {
                Begin(move, null);
                return;
            }
        }

        Locomote(target, dist);
    }

    private Character ValidateTarget()
    {
        Character target = ai.CurrentTarget;

        bool invalid = target == null || (target is Player p && p.IsDead());

        if (!invalid)
            return target;

        // Objetivo caído: buscar a otro jugador, y si no hay, volver a Idle.
        Character other = ai.Perception != null ? ai.Perception.DetectPlayer(true) : null;

        if (other != null && !(other is Player op && op.IsDead()))
        {
            ai.SetTarget(other);
            return other;
        }

        ai.SetTarget(null);
        ai.StateMachine.ChangeState(ai.IdleState);
        return null;
    }

    // =========================================================
    // POSTURAS Y TRANSICIONES
    // =========================================================

    private void BeginStance(Stance newStance, bool allowRetarget)
    {
        stance = newStance;
        stanceTimer = 0f;
        farTimer = 0f;
        strafeTimer = 0f;

        Vector2 range = newStance == Stance.Ranged
            ? Settings.rangedStanceSeconds
            : Settings.meleeStanceSeconds;

        stanceDuration = Random.Range(range.x, range.y) * Tempo;

        if (allowRetarget)
            TryRetarget();
    }

    private void StartTransition(float dist)
    {
        if (stance == Stance.Ranged)
        {
            // A distancia -> cuerpo a cuerpo: embiste si está lejos; si ya está cerca, camina.
            DemiGodMoveSettings charge = FindMove(DemiGodMoveType.Charge);

            if (charge != null && CanUse(charge, dist))
                Begin(charge, Stance.Melee);
            else
                BeginStance(Stance.Melee, true);
        }
        else
        {
            // Cuerpo a cuerpo -> a distancia: golpe de área que lo empuja y se aleja.
            DemiGodMoveSettings slam = FindMove(DemiGodMoveType.Slam);

            if (slam != null && CanUse(slam, dist))
                Begin(slam, Stance.Ranged);
            else
                BeginStance(Stance.Ranged, true);
        }
    }

    /// <summary>Reacciones a cómo juega el jugador, sin cambiar la postura.</summary>
    private void UpdateReactions(Character target, float dist)
    {
        if (stance == Stance.Ranged)
        {
            // Se te pegó mientras disparo: castigo.
            if (dist < Settings.panicDistance && Time.time >= panicReadyTime)
            {
                DemiGodMoveSettings punish = FindMove(DemiGodMoveType.Strike);

                if (punish == null || !CanUse(punish, dist))
                    punish = FindMove(DemiGodMoveType.Slam);

                if (punish != null && CanUse(punish, dist))
                {
                    panicReadyTime = Time.time + 3f * Tempo;
                    Begin(punish, null);
                }
            }

            return;
        }

        // Cuerpo a cuerpo y el jugador huye: embiste o dispara.
        if (dist > Settings.farDistance)
            farTimer += Time.deltaTime;
        else
            farTimer = 0f;

        if (farTimer < 1.5f)
            return;

        farTimer = 0f;

        DemiGodMoveSettings charge = FindMove(DemiGodMoveType.Charge);

        if (charge != null && CanUse(charge, dist))
        {
            Begin(charge, null);
            return;
        }

        DemiGodMoveSettings ranged = PickMove(Stance.Ranged, dist);

        if (ranged != null)
            Begin(ranged, null);
    }

    private void TryRetarget()
    {
        if (Random.value > Settings.retargetChance)
            return;

        Character current = ai.CurrentTarget;
        FillLivingPlayers();

        var options = new List<Player>();

        foreach (Player p in playerBuffer)
        {
            if (p != current && HorizontalDistance(p) <= Settings.leashDistance * 0.7f)
                options.Add(p);
        }

        if (options.Count > 0)
            ai.SetTarget(options[Random.Range(0, options.Count)]);
    }

    // =========================================================
    // ELECCIÓN DE MOVIMIENTOS
    // =========================================================

    private static bool BelongsTo(DemiGodMoveType type, Stance pool)
    {
        switch (type)
        {
            case DemiGodMoveType.Fireball:
            case DemiGodMoveType.Orb:
            case DemiGodMoveType.Volley:
            case DemiGodMoveType.Meteors:
            case DemiGodMoveType.Summon:
                return pool == Stance.Ranged;

            case DemiGodMoveType.Strike:
            case DemiGodMoveType.Slam:
                return pool == Stance.Melee;

            default:
                return false; // Charge: solo como transición
        }
    }

    private DemiGodMoveSettings FindMove(DemiGodMoveType type)
    {
        foreach (DemiGodMoveSettings m in Settings.moves)
        {
            if (m.type == type && m.enabled && boss.CurrentPhase >= m.minPhase)
                return m;
        }

        return null;
    }

    /// <summary>Disponible ahora mismo: recarga lista, distancia válida y, si invoca, hay lugar.</summary>
    private bool CanUse(DemiGodMoveSettings m, float dist)
    {
        if (!m.enabled || boss.CurrentPhase < m.minPhase || Time.time < m.nextReadyTime)
            return false;

        if (m.minDistance > 0f && dist < m.minDistance)
            return false;

        if (m.maxDistance > 0f && dist > m.maxDistance)
            return false;

        if (m.type == DemiGodMoveType.Summon && !boss.CanSummon)
            return false;

        return true;
    }

    private DemiGodMoveSettings PickMove(Stance pool, float dist)
    {
        candidates.Clear();

        foreach (DemiGodMoveSettings m in Settings.moves)
        {
            if (m.weight > 0f && BelongsTo(m.type, pool) && CanUse(m, dist))
                candidates.Add(m);
        }

        // Evita repetir el mismo movimiento dos veces seguidas (si hay otra opción).
        if (candidates.Count > 1 && lastMove != null)
            candidates.Remove(lastMove);

        if (candidates.Count == 0)
            return null;

        float total = 0f;

        foreach (DemiGodMoveSettings m in candidates)
            total += m.weight;

        float roll = Random.value * total;

        foreach (DemiGodMoveSettings m in candidates)
        {
            roll -= m.weight;

            if (roll <= 0f)
                return m;
        }

        return candidates[candidates.Count - 1];
    }

    private void Begin(DemiGodMoveSettings move, Stance? after)
    {
        busy = true;
        stanceAfterMove = after;
        lastMove = move;
        move.nextReadyTime = Time.time + move.cooldown * Tempo;

        if (agent != null && agent.isOnNavMesh)
        {
            agent.ResetPath();
            agent.velocity = Vector3.zero;
        }

        Debug.Log($"[DemiGod] {stance} -> {move.type}");

        switch (move.type)
        {
            case DemiGodMoveType.Fireball: routine = ai.StartCoroutine(FireballRoutine(move)); break;
            case DemiGodMoveType.Orb:      routine = ai.StartCoroutine(OrbRoutine(move)); break;
            case DemiGodMoveType.Volley:   routine = ai.StartCoroutine(VolleyRoutine(move)); break;
            case DemiGodMoveType.Meteors:  routine = ai.StartCoroutine(MeteorsRoutine(move)); break;
            case DemiGodMoveType.Summon:   routine = ai.StartCoroutine(SummonRoutine(move)); break;
            case DemiGodMoveType.Strike:   routine = ai.StartCoroutine(StrikeRoutine(move)); break;
            case DemiGodMoveType.Slam:     routine = ai.StartCoroutine(SlamRoutine(move)); break;
            case DemiGodMoveType.Charge:   routine = ai.StartCoroutine(ChargeRoutine(move)); break;
            default: EndMove(); break;
        }
    }

    private void EndMove()
    {
        busy = false;
        routine = null;

        Vector2 breath = Settings.breathSeconds;
        nextMoveTime = Time.time + Random.Range(breath.x, breath.y) * Tempo;

        if (stanceAfterMove.HasValue)
        {
            BeginStance(stanceAfterMove.Value, true);
            stanceAfterMove = null;
        }
    }

    // =========================================================
    // MOVIMIENTOS A DISTANCIA
    // =========================================================

    private IEnumerator FireballRoutine(DemiGodMoveSettings m)
    {
        float lead = Mathf.Min(m.windup, Settings.animationLead);

        yield return Wait(m.windup - lead, true);
        PlayAnimation(m.animation);
        yield return Wait(lead, true);

        if (!Alive())
            yield break;

        Character target = ai.CurrentTarget;

        if (target != null)
            FireProjectile(boss.SpellPrefab, AimDirection(target), Damage(m));

        yield return Wait(0.25f, true);
        EndMove();
    }

    private IEnumerator OrbRoutine(DemiGodMoveSettings m)
    {
        Character target = ai.CurrentTarget;
        GameObject prefab = boss.SpecialPrefab != null ? boss.SpecialPrefab : boss.SpellPrefab;

        if (target == null || prefab == null)
        {
            EndMove();
            yield break;
        }

        // La dirección se fija al empezar el aviso: el carril muestra exactamente por dónde pasa.
        Vector3 aim = AimDirection(target);
        Vector3 flat = Flat(aim);

        LookAt(flat);
        boss.ShowLane(enemy.transform.position, flat, m.length, m.size, m.windup);

        float lead = Mathf.Min(m.windup, Settings.animationLead);

        yield return Wait(m.windup - lead, false);
        PlayAnimation(m.animation);
        yield return Wait(lead, false);

        if (!Alive())
            yield break;

        FireProjectile(prefab, aim, Damage(m));

        yield return Wait(0.35f, false);
        EndMove();
    }

    private IEnumerator VolleyRoutine(DemiGodMoveSettings m)
    {
        float lead = Mathf.Min(m.windup, Settings.animationLead);

        yield return Wait(m.windup - lead, true);
        PlayAnimation(m.animation);
        yield return Wait(lead, true);

        if (!Alive())
            yield break;

        Character target = ai.CurrentTarget;

        if (target != null)
        {
            Vector3 aim = AimDirection(target);

            for (int i = 0; i < m.count; i++)
            {
                float angle = (i - (m.count - 1) * 0.5f) * m.size;
                Vector3 dir = Quaternion.AngleAxis(angle, Vector3.up) * aim;

                FireProjectile(boss.SpellPrefab, dir, Damage(m));
            }
        }

        yield return Wait(0.4f, true);
        EndMove();
    }

    private IEnumerator MeteorsRoutine(DemiGodMoveSettings m)
    {
        PlayAnimation(m.animation);

        for (int i = 0; i < m.count; i++)
        {
            if (!Alive())
                yield break;

            Character target = ai.CurrentTarget;

            if (target == null)
                break;

            // La primera cae sobre el jugador; las siguientes, cerca de donde esté en ese momento.
            Vector3 center = target.transform.position;

            if (i > 0)
            {
                Vector2 jitter = Random.insideUnitCircle * 2.5f;
                center += new Vector3(jitter.x, 0f, jitter.y);
            }

            boss.ShowCircle(center, m.size, m.windup);
            ai.StartCoroutine(ZoneRoutine(center, m));

            yield return Wait(0.6f, true);
        }

        yield return Wait(0.3f, true);
        EndMove();
    }

    /// <summary>Una zona de impacto: independiente del boss, se resuelve aunque él ya esté haciendo otra cosa.</summary>
    private IEnumerator ZoneRoutine(Vector3 center, DemiGodMoveSettings m)
    {
        yield return new WaitForSeconds(m.windup);

        if (!Alive())
            yield break;

        float damage = Damage(m);

        FillLivingPlayers();

        foreach (Player p in playerBuffer)
        {
            if (InCircle(p, center, m.size))
                DamagePlayer(p, damage);
        }

        boss.PlayImpact(center, m.size);
    }

    private IEnumerator SummonRoutine(DemiGodMoveSettings m)
    {
        float lead = Mathf.Min(m.windup, Settings.animationLead);

        yield return Wait(m.windup - lead, true);
        PlayAnimation(m.animation);
        yield return Wait(lead, true);

        if (!Alive())
            yield break;

        boss.SummonWave();

        yield return Wait(0.5f, true);
        EndMove();
    }

    // =========================================================
    // MOVIMIENTOS CUERPO A CUERPO
    // =========================================================

    private IEnumerator StrikeRoutine(DemiGodMoveSettings m)
    {
        Character target = ai.CurrentTarget;

        if (target == null)
        {
            EndMove();
            yield break;
        }

        // Carril frontal fijado al empezar: se esquiva saliendo de él.
        Vector3 dir = Flat(target.transform.position - enemy.transform.position);
        Vector3 origin = enemy.transform.position;

        LookAt(dir);
        boss.ShowLane(origin, dir, m.length, m.size, m.windup);

        float lead = Mathf.Min(m.windup, Settings.animationLead);

        yield return Wait(m.windup - lead, false);
        PlayAnimation(m.animation);
        yield return Wait(lead, false);

        if (!Alive())
            yield break;

        float damage = Damage(m);

        FillLivingPlayers();

        foreach (Player p in playerBuffer)
        {
            if (InLane(p, origin, dir, m.length, m.size))
                DamagePlayer(p, damage);
        }

        boss.PlayImpact(origin + dir * (m.length * 0.6f), m.size * 0.5f);

        yield return Wait(0.45f, false);
        EndMove();
    }

    private IEnumerator SlamRoutine(DemiGodMoveSettings m)
    {
        Vector3 center = enemy.transform.position;

        boss.ShowCircle(center, m.size, m.windup);

        float lead = Mathf.Min(m.windup, Settings.animationLead);

        yield return Wait(m.windup - lead, true);
        PlayAnimation(m.animation);
        yield return Wait(lead, true);

        if (!Alive())
            yield break;

        float damage = Damage(m);

        FillLivingPlayers();

        foreach (Player p in playerBuffer)
        {
            if (!InCircle(p, center, m.size))
                continue;

            DamagePlayer(p, damage);
            Knockback(p, Flat(p.transform.position - center), Settings.knockbackDistance);
        }

        boss.PlayImpact(center, m.size);

        yield return Wait(0.6f, false);
        EndMove();
    }

    // =========================================================
    // TRANSICIÓN: EMBESTIDA
    // =========================================================

    private IEnumerator ChargeRoutine(DemiGodMoveSettings m)
    {
        Character target = ai.CurrentTarget;

        if (target == null || agent == null || !agent.isOnNavMesh)
        {
            EndMove();
            yield break;
        }

        Vector3 start = enemy.transform.position;
        Vector3 dir = Flat(target.transform.position - start);
        float length = Mathf.Min(m.length, HorizontalDistance(target) + 4f);
        Vector3 end = start + dir * length;

        // Si hay un borde del NavMesh en el camino (pared, precipicio), la embestida termina ahí.
        if (NavMesh.Raycast(start, end, out NavMeshHit edge, NavMesh.AllAreas))
            end = edge.position;

        Vector3 path = end - start;
        path.y = 0f;
        length = path.magnitude;

        if (length < 3f)
        {
            // Sin espacio para embestir: camina en vez de quedarse parado.
            EndMove();
            yield break;
        }

        dir = path / length;

        LookAt(dir);
        boss.ShowLane(start, dir, length, m.size, m.windup);

        float lead = Mathf.Min(m.windup, Settings.animationLead);

        yield return Wait(m.windup - lead, false);
        PlayAnimation(m.animation);
        yield return Wait(lead, false);

        if (!Alive())
            yield break;

        // ---- Carrera ----
        chargeActive = true;
        chargeSavedSpeed = agent.speed;
        chargeSavedAccel = agent.acceleration;

        agent.speed = Settings.chargeSpeed;
        agent.acceleration = 80f;
        agent.stoppingDistance = 0.1f;
        agent.isStopped = false;
        agent.SetDestination(end);

        var alreadyHit = new HashSet<Player>();
        float damage = Damage(m);
        float timeout = length / Mathf.Max(1f, Settings.chargeSpeed) + 1f;
        float elapsed = 0f;

        while (elapsed < timeout)
        {
            if (!Alive())
                yield break;

            Vector3 velocity = agent.velocity;
            velocity.y = 0f;

            if (velocity.sqrMagnitude > 0.5f)
                enemy.transform.rotation = Quaternion.LookRotation(velocity);

            FillLivingPlayers();

            foreach (Player p in playerBuffer)
            {
                if (alreadyHit.Contains(p) || !InCircle(p, enemy.transform.position, m.size * 0.5f + 0.6f))
                    continue;

                alreadyHit.Add(p);
                DamagePlayer(p, damage);
                Knockback(p, dir, Settings.knockbackDistance * 0.6f);
            }

            if (!agent.pathPending && agent.remainingDistance <= 0.7f)
                break;

            elapsed += Time.deltaTime;
            yield return null;
        }

        agent.speed = chargeSavedSpeed;
        agent.acceleration = chargeSavedAccel;
        chargeActive = false;

        if (agent.isOnNavMesh)
        {
            agent.ResetPath();
            agent.velocity = Vector3.zero;
        }

        boss.PlayImpact(enemy.transform.position, 3f);

        // Ventana de vulnerabilidad: queda quieto, es el momento de atacarlo.
        yield return Wait(Settings.chargeRecovery, false);
        EndMove();
    }

    // =========================================================
    // DESPLAZAMIENTO
    // =========================================================

    private void Locomote(Character target, float dist)
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
            return;

        DemiGodCombatSettings s = Settings;

        if (stance == Stance.Melee)
        {
            if (dist > s.meleeDistance)
                MoveTo(target.transform.position, s.meleeDistance * 0.8f);
            else
                StopMoving();

            return;
        }

        if (dist < s.rangedMinDistance)
            Retreat(target);
        else if (dist > s.rangedMaxDistance)
            MoveTo(target.transform.position, s.rangedMaxDistance * 0.7f);
        else
            Strafe(target);
    }

    private void MoveTo(Vector3 position, float stoppingDistance)
    {
        if (Time.time < nextPathTime)
            return;

        nextPathTime = Time.time + 0.2f;

        agent.stoppingDistance = stoppingDistance;
        agent.isStopped = false;
        agent.SetDestination(position);
    }

    private void StopMoving()
    {
        if (agent.hasPath)
            agent.ResetPath();
    }

    private void Retreat(Character target)
    {
        if (Time.time < nextPathTime)
            return;

        Vector3 away = Flat(enemy.transform.position - target.transform.position);
        Vector3 destination = enemy.transform.position + away * 6f;

        if (NavMesh.SamplePosition(destination, out NavMeshHit hit, 5f, NavMesh.AllAreas))
            MoveTo(hit.position, 0.3f);
        else
            Strafe(target);
    }

    private void Strafe(Character target)
    {
        strafeTimer -= Time.deltaTime;

        if (strafeTimer > 0f)
            return;

        strafeTimer = Random.Range(1.5f, 3f);
        strafeSign = -strafeSign;

        Vector3 toTarget = Flat(target.transform.position - enemy.transform.position);
        Vector3 side = Vector3.Cross(Vector3.up, toTarget) * strafeSign;
        Vector3 destination = enemy.transform.position + side * 4f;

        if (NavMesh.SamplePosition(destination, out NavMeshHit hit, 3f, NavMesh.AllAreas))
        {
            nextPathTime = 0f;
            MoveTo(hit.position, 0.3f);
        }
    }

    // =========================================================
    // UTILIDADES
    // =========================================================

    /// <summary>Espera 'seconds' (frame a frame). Con 'track' sigue mirando al objetivo.</summary>
    private IEnumerator Wait(float seconds, bool track)
    {
        float elapsed = 0f;

        while (elapsed < seconds)
        {
            if (!Alive())
                yield break;

            if (track && ai.CurrentTarget != null)
                FaceTarget(ai.CurrentTarget, 360f);

            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private bool Alive()
    {
        return enemy != null
            && enemy.IsSpawned
            && enemy.GetStats() != null
            && enemy.GetStats().CurrentHealth > 0f;
    }

    private float Damage(DemiGodMoveSettings m)
        => enemy.GetStats().Attack.Value * m.damageMultiplier;

    private void PlayAnimation(AttackAnimKind kind)
    {
        anim?.PlayAttack(kind);

        audioFx?.PlayAttackOnAllClients(
            kind == AttackAnimKind.Heavy ? AttackSoundKind.Heavy :
            kind == AttackAnimKind.Special ? AttackSoundKind.Special :
            AttackSoundKind.Light);
    }

    private float HorizontalDistance(Character target)
    {
        Vector3 a = enemy.transform.position;
        Vector3 b = target.transform.position;
        a.y = 0f;
        b.y = 0f;

        return Vector3.Distance(a, b);
    }

    private static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v.sqrMagnitude < 0.0001f ? Vector3.forward : v.normalized;
    }

    private void FaceTarget(Character target, float degreesPerSecond)
    {
        Vector3 dir = target.transform.position - enemy.transform.position;
        dir.y = 0f;

        if (dir.sqrMagnitude < 0.001f)
            return;

        enemy.transform.rotation = Quaternion.RotateTowards(
            enemy.transform.rotation,
            Quaternion.LookRotation(dir.normalized),
            degreesPerSecond * Time.deltaTime);
    }

    private void LookAt(Vector3 flatDirection)
    {
        if (flatDirection.sqrMagnitude > 0.001f)
            enemy.transform.rotation = Quaternion.LookRotation(flatDirection);
    }

    private Vector3 SpawnPoint => enemy.transform.position + Vector3.up * 2f;

    private Vector3 AimDirection(Character target)
        => (target.transform.position + Vector3.up - SpawnPoint).normalized;

    // ---------- Jugadores y daño ----------

    private void FillLivingPlayers()
    {
        playerBuffer.Clear();

        foreach (Player p in Object.FindObjectsByType<Player>(FindObjectsSortMode.None))
        {
            if (p != null && !p.IsDead())
                playerBuffer.Add(p);
        }
    }

    private bool InCircle(Player p, Vector3 center, float radius)
    {
        Vector3 d = p.transform.position - center;

        if (Mathf.Abs(d.y) > 4f)
            return false;

        d.y = 0f;
        return d.magnitude <= radius + 0.35f;
    }

    private bool InLane(Player p, Vector3 origin, Vector3 dir, float length, float width)
    {
        Vector3 rel = p.transform.position - origin;

        if (Mathf.Abs(rel.y) > 4f)
            return false;

        rel.y = 0f;

        float along = Vector3.Dot(rel, dir);
        float side = Mathf.Abs(Vector3.Dot(rel, Vector3.Cross(Vector3.up, dir)));

        return along >= -0.5f && along <= length + 0.5f && side <= width * 0.5f + 0.4f;
    }

    private void DamagePlayer(Player p, float damage)
    {
        p.GetComponent<DamageReceiver>()?.TakeDamage(damage, enemy);
    }

    private void Knockback(Player p, Vector3 direction, float distance)
    {
        p.GetComponent<MovementController>()?.ApplyKnockback(direction, distance, Settings.knockbackDuration);
    }

    // ---------- Proyectiles ----------

    private void FireProjectile(GameObject prefab, Vector3 direction, float damage)
    {
        if (prefab == null || NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
            return;

        GameObject go = Object.Instantiate(prefab, SpawnPoint, Quaternion.LookRotation(direction));

        if (!go.TryGetComponent<NetworkObject>(out var netObj))
        {
            Debug.LogError("[DemiGod] El proyectil necesita NetworkObject.");
            Object.Destroy(go);
            return;
        }

        netObj.Spawn();

        if (go.TryGetComponent<NetworkProjectile>(out var projectile))
            projectile.Initialize(enemy, damage, direction);
    }
}
