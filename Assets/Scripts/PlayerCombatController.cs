
using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;
using Unity.Netcode;

public class PlayerCombatController : NetworkBehaviour
{
    private Player player;
    private CharacterStats stats;
    private EquipmentController equipment;
    private ResourceController resources;
    private TargetingController targeting;
    private PlayerAnimationController anim;
    private CharacterStatsSyncController levelSync;

    [Header("Fallback")]
    [SerializeField] private float holdThreshold = 0.4f;
    [SerializeField] private float fallbackHeavyMultiplier = 2f;
    [Tooltip("Costo de stamina de un ataque liviano cuando no hay arma equipada (el pesado cuesta el triple).")]
    [FormerlySerializedAs("fallbackHeavyStaminaCost")]
    [SerializeField] private float fallbackStaminaCost = 8f;

    // El ataque pesado cuesta el triple de stamina que el liviano.
    private const float HeavyStaminaMultiplier = 3f;
    [SerializeField] private float fallbackMagicManaCost = 20f;

    [Header("Melee Hit Validation")]
    [SerializeField] private float attackConeAngle = 60f;
    [SerializeField] private float sphereCastRadius = 0.8f;
    [SerializeField] private LayerMask enemyLayer;

    private float holdTimer;
    private bool isHoldingAttack;
    private bool heavyTriggered;
    private float nextAttackTime;

    // Habilidad especial de clase (Mage / Warrior / Hunter)
    private float nextSpecialTime;
    private Coroutine barrageRoutine;

    public void Initialize(Player player)
    {
        this.player = player;
        stats = player.GetStats();
        equipment = player.GetEquipment();
        resources = player.GetResourceController();
        targeting = player.GetComponent<TargetingController>();
        anim = player.GetComponent<PlayerAnimationController>();
    }

    // =========================================================
    // DESBLOQUEO DEL ESPECIAL
    // =========================================================

    /// <summary>Nivel al que se desbloquea el ataque especial (se edita en el PlayerClassData).</summary>
    public int SpecialUnlockLevel =>
        player != null && player.ClassData != null ? player.ClassData.SpecialUnlockLevel : 1;

    public bool IsSpecialUnlocked =>
        player != null && player.CurrentLevel >= SpecialUnlockLevel;

    private WeaponData GetCurrentWeapon()
        => equipment?.GetEquippedWeapon();

    private IWeaponBehavior GetCurrentBehavior()
        => WeaponBehaviorFactory.Create(GetCurrentWeapon(), transform);

    private float GetEffectiveHoldThreshold()
    {
        WeaponData weapon = GetCurrentWeapon();

        return weapon == null || weapon.AttackSpeed <= 0f
            ? holdThreshold
            : holdThreshold / weapon.AttackSpeed;
    }

    /// <summary>
    /// false para clases que gastan maná en vez de stamina (Mage).
    /// Sin ClassData se mantiene el comportamiento de stamina.
    /// </summary>
    private bool UsesStaminaForAttacks()
    {
        return player.ClassData == null ||
               player.ClassData.ConsumesStaminaOnAttack;
    }

    private float GetHeavyMultiplier()
    {
        WeaponData weapon = GetCurrentWeapon();
        return weapon != null
            ? weapon.HeavyMultiplier
            : fallbackHeavyMultiplier;
    }

    // =========================================================
    // AIM
    // =========================================================

    private Vector3 GetAimDirection()
    {
        Camera cam = Camera.main;

        if (cam == null)
            return player.transform.forward;

        Ray ray = cam.ViewportPointToRay(
            new Vector3(0.5f, 0.5f, 0f));

        return ray.direction.normalized;
    }

    // =========================================================
    // INPUT
    // =========================================================

    public void OnAttackPressed()
    {
        if (!IsOwner || player == null || player.IsDead())
            return;

        isHoldingAttack = true;
        heavyTriggered = false;
        holdTimer = 0f;
    }

    public void OnAttackHeld()
    {
        if (!IsOwner || !isHoldingAttack || heavyTriggered)
            return;

        holdTimer += Time.deltaTime;

        if (holdTimer >= GetEffectiveHoldThreshold())
        {
            heavyTriggered = true;
            isHoldingAttack = false;

            RequestHeavyAttackServerRpc(GetAimDirection());
        }
    }

    public void OnAttackReleased()
    {
        if (!IsOwner || !isHoldingAttack)
            return;

        isHoldingAttack = false;

        if (!heavyTriggered)
            RequestAttackServerRpc(GetAimDirection());
    }

    // =========================================================
    // RPC
    // =========================================================

    [ServerRpc]
    private void RequestAttackServerRpc(Vector3 aimDirection)
    {
        PerformAttack(false, aimDirection);
    }

    [ServerRpc]
    private void RequestHeavyAttackServerRpc(Vector3 aimDirection)
    {
        PerformAttack(true, aimDirection);
    }

   
    // Método local llamado desde Player.cs.
    public void RequestSpecialAttack()
    {
        if (!IsOwner || player == null || player.IsDead())
            return;

        if (!IsSpecialUnlocked)
        {
            SkillNoticeUI.Show($"El ataque especial se desbloquea en el nivel {SpecialUnlockLevel}", 2f);
            return;
        }

        RequestSpecialAttackInternalServerRpc(GetAimDirection());
    }

    [ServerRpc]
    private void RequestSpecialAttackInternalServerRpc(
        Vector3 aimDirection)
    {
        PerformSpecialAttack(aimDirection);
    }

    // =========================================================
    // ATTACK
    // =========================================================

    private void PerformAttack(bool heavy, Vector3 aimDirection)
    {
        if (!CanAttackNow())
            return;

        aimDirection = NormalizeAim(aimDirection);

        WeaponData weapon = GetCurrentWeapon();
        bool isRanged = weapon != null && weapon.IsRanged;

        // El ataque comienza aunque no exista un enemigo.
        // El target solo es necesario para aplicar daño melee.
        Character target = isRanged
            ? null
            : FindMeleeTarget(aimDirection);

        if (weapon != null && weapon.IsMagic)
        {
            // Armas mágicas (Staff / Grimoire): cada hechizo cuesta maná.
            // El ataque cargado cuesta ManaCost x HeavyMultiplier y no usa stamina.
            float manaCost = weapon.ManaCost *
                             (heavy ? weapon.HeavyMultiplier : 1f);

            if (manaCost > 0f &&
                (resources == null || !resources.UseMana(manaCost)))
            {
                Debug.Log("[PlayerCombat] Not enough mana");
                return;
            }
        }
        else if (UsesStaminaForAttacks())
        {
            // Hunter / Warrior: todo ataque básico gasta stamina.
            // Liviano = costo del arma. Pesado = el triple.
            float baseCost = weapon != null
                ? weapon.StaminaCost
                : fallbackStaminaCost;

            float cost = baseCost * (heavy ? HeavyStaminaMultiplier : 1f);

            if (cost > 0f &&
                (resources == null || !resources.UseStamina(cost)))
            {
                Debug.Log(
                    $"[PlayerCombat] Not enough stamina " +
                    $"(necesita {cost:F0}, heavy:{heavy})");
                return;
            }
        }

        PlayAttackFeedback(
            heavy ? AttackAnimKind.Heavy : AttackAnimKind.Light);

        // Ranged: dispara incluso si target es null.
        if (isRanged)
        {
            IWeaponBehavior behavior = GetCurrentBehavior();

            if (behavior is IAimedWeaponBehavior aimedBehavior)
            {
                if (heavy)
                    aimedBehavior.PerformAimedHeavyAttack(
                        player, aimDirection, GetHeavyMultiplier());
                else
                    aimedBehavior.PerformAimedAttack(
                        player, aimDirection);
            }
            else
            {
                Debug.LogError(
                    "[PlayerCombat] El arma ranged no implementa " +
                    "IAimedWeaponBehavior.");
            }

            return;
        }

        // Melee: sin blanco válido, el golpe falla pero se ejecuta.
        if (target == null)
            return;

        IWeaponBehavior meleeBehavior = GetCurrentBehavior();

        if (heavy)
            meleeBehavior.PerformHeavyAttack(
                player, target, GetHeavyMultiplier());
        else
            meleeBehavior.PerformAttack(player, target);

        Debug.Log(
            $"[PlayerCombat] ATTACK {player.name} -> {target.name} " +
            $"| Heavy:{heavy}");
    }

    // =========================================================
    // SPECIAL ATTACK
    // =========================================================

    private void PerformSpecialAttack(Vector3 aimDirection)
    {
        if (!CanAttackNow())
            return;

        // El servidor es la autoridad: el especial no se usa antes de su nivel.
        if (!IsSpecialUnlocked)
        {
            Debug.Log(
                $"[PlayerCombat] Especial bloqueado hasta nivel {SpecialUnlockLevel} " +
                $"(nivel actual {player.CurrentLevel})");

            return;
        }

        aimDirection = NormalizeAim(aimDirection);

        // Si la clase define una habilidad propia, reemplaza al especial del arma.
        if (TryPerformClassSpecial(aimDirection))
            return;

        WeaponData weapon = GetCurrentWeapon();
        bool isRanged = weapon != null && weapon.IsRanged;

        Character target = isRanged
            ? null
            : FindMeleeTarget(aimDirection);

        // En melee no se consume maná si no hay un blanco válido.
        if (!isRanged && target == null)
        {
            PlayAttackFeedback(AttackAnimKind.Special);
            return;
        }

        float manaCost = weapon != null
            ? weapon.ManaCost
            : fallbackMagicManaCost;

        if (resources == null || !resources.UseMana(manaCost))
        {
            Debug.Log("[PlayerCombat] Not enough mana");
            return;
        }

        PlayAttackFeedback(AttackAnimKind.Special);

        IWeaponBehavior behavior = GetCurrentBehavior();

        if (isRanged)
        {
            if (behavior is IAimedWeaponBehavior aimedBehavior)
            {
                aimedBehavior.PerformAimedSpecialAttack(
                    player, aimDirection);
            }
            else
            {
                Debug.LogError(
                    "[PlayerCombat] El arma especial no implementa " +
                    "IAimedWeaponBehavior.");
            }

            return;
        }

        behavior.PerformSpecialAttack(player, target);
    }

    // =========================================================
    // CLASS SPECIAL ABILITIES — SERVER
    // Mage: onda expansiva | Warrior: hoja de energía | Hunter: ráfagas
    // Cada una lee sus valores de su propio asset (ver SpecialAbility).
    // =========================================================

    /// <summary>
    /// Devuelve true si la clase tiene habilidad propia (se haya podido
    /// lanzar o no). Devuelve false si no tiene: en ese caso el llamador
    /// sigue con el especial del arma de siempre.
    /// </summary>
    private bool TryPerformClassSpecial(Vector3 aimDirection)
    {
        SpecialAbility ability =
            player.ClassData != null
                ? player.ClassData.SpecialAbility
                : null;

        if (ability == null)
            return false;

        if (Time.time < nextSpecialTime)
        {
            Debug.Log(
                $"[PlayerCombat] Special en cooldown " +
                $"({nextSpecialTime - Time.time:F1}s)");

            return true;
        }

        switch (ability)
        {
            case MageShockwaveAbility mage:
                CastMageShockwave(mage);
                return true;

            case WarriorBladeAbility warrior:
                CastWarriorBlade(warrior, aimDirection);
                return true;

            case HunterBarrageAbility hunter:
                CastHunterBarrage(hunter, aimDirection);
                return true;

            default:
                Debug.LogWarning(
                    $"[PlayerCombat] Habilidad '{ability.name}' sin implementación " +
                    "en PlayerCombatController. Se usa el especial del arma.");

                return false;
        }
    }

    // ---------- Mage ----------

    private void CastMageShockwave(MageShockwaveAbility ability)
    {
        if (!SpendSpecialMana(ability.ManaCost))
            return;

        CommitSpecial(ability);

        Vector3 center = player.transform.position;

        var targets = SpecialAbilityEffects.FindEnemiesInRadius(player,center,ability.Radius,ability.DamageLayers);

        float damage = stats.Attack.Value * ability.DamageMultiplier;

        int hits = SpecialAbilityEffects.ApplyDamage(targets, player, damage);

        PlaySpecialVfxClientRpc(center, player.transform.forward);

        Debug.Log(
            $"[PlayerCombat] SHOCKWAVE {player.name} | " +
            $"enemigos:{hits} dmg:{damage:F0} radio:{ability.Radius}");
    }

    // ---------- Warrior ----------

    private void CastWarriorBlade(
        WarriorBladeAbility ability,
        Vector3 aimDirection)
    {
        // Sin maná: solo cooldown. Se lanza aunque no haya enemigos al frente.
        CommitSpecial(ability);

        PlaySpecialVfxClientRpc(player.transform.position, aimDirection);

        StartCoroutine(WarriorBladeHitRoutine(ability, aimDirection));
    }

    private IEnumerator WarriorBladeHitRoutine(
        WarriorBladeAbility ability,
        Vector3 aimDirection)
    {
        // El daño llega cuando la hoja ya está barriendo, no en el click.
        if (ability.HitDelay > 0f)
            yield return new WaitForSeconds(ability.HitDelay);

        if (player == null || player.IsDead())
            yield break;

        // Los enemigos se buscan recién ahora: cuentan los que estén
        // en el arco en este momento.
        var targets = SpecialAbilityEffects.FindEnemiesInCone(player,aimDirection,ability.BladeLength,ability.SweepAngle,ability.DamageLayers);

        float damage = stats.Attack.Value * ability.DamageMultiplier;

        int hits = SpecialAbilityEffects.ApplyDamage(targets, player, damage);

        Debug.Log(
            $"[PlayerCombat] BLADE {player.name} | " +
            $"enemigos:{hits} dmg:{damage:F0} largo:{ability.BladeLength}");
    }

    // ---------- Hunter ----------

    private void CastHunterBarrage(
        HunterBarrageAbility ability,
        Vector3 aimDirection)
    {
        // Una ráfaga a la vez.
        if (barrageRoutine != null)
            return;

        WeaponData weapon = GetCurrentWeapon();

        if (weapon == null || !weapon.IsRanged)
        {
            Debug.LogWarning(
                "[PlayerCombat] La ráfaga necesita un arma a distancia equipada.");

            return;
        }

        CommitSpecial(ability);

        // Mientras salen las flechas no se puede disparar el ataque normal encima.
        nextAttackTime = Mathf.Max(
            nextAttackTime,
            Time.time + ability.TotalDuration + 0.2f);

        barrageRoutine = StartCoroutine(
            ArrowBarrageRoutine(weapon, ability, aimDirection));
    }

    /// <summary>
    /// Dispara las flechas una tras otra, agrupadas en ráfagas:
    /// pausa corta entre flechas y una más larga entre ráfagas.
    /// </summary>
    private IEnumerator ArrowBarrageRoutine(
        WeaponData weapon,
        HunterBarrageAbility ability,
        Vector3 aimDirection)
    {
        var behavior = new RangedWeaponBehavior(weapon, transform);
        float damage = stats.Attack.Value * ability.DamageMultiplier;

        var betweenArrows = new WaitForSeconds(ability.DelayBetweenArrows);
        var betweenBursts = new WaitForSeconds(ability.DelayBetweenBursts);

        for (int i = 0; i < ability.TotalArrows; i++)
        {
            // Si muere a mitad de la ráfaga, se corta.
            if (player == null || player.IsDead())
                break;

            // La primera flecha de cada ráfaga sale recta; el resto se abre un poco.
            bool firstOfBurst = i % ability.ArrowsPerBurst == 0;

            Vector3 direction = SpecialAbilityEffects.ApplySpread(
                aimDirection,
                firstOfBurst ? 0f : ability.SpreadAngle);

            behavior.FireProjectile(player, direction, damage);

            if (i == ability.TotalArrows - 1)
                break;

            bool endOfBurst = (i + 1) % ability.ArrowsPerBurst == 0;

            yield return endOfBurst ? betweenBursts : betweenArrows;
        }

        barrageRoutine = null;
    }

    // ---------- Comunes ----------

    /// <summary>Cobra el maná de la habilidad. Con costo 0 (Warrior, Hunter) siempre pasa.</summary>
    private bool SpendSpecialMana(float manaCost)
    {
        if (manaCost <= 0f)
            return true;

        if (resources != null && resources.UseMana(manaCost))
            return true;

        Debug.Log("[PlayerCombat] Not enough mana");
        return false;
    }

    /// <summary>Inicia el cooldown de la habilidad y dispara la animación de ataque especial.</summary>
    private void CommitSpecial(SpecialAbility ability)
    {
        nextSpecialTime = Time.time + ability.Cooldown;
        PlayAttackFeedback(AttackAnimKind.Special);
    }

    /// <summary>
    /// Reproduce el efecto visual en todos los clientes. Cada cliente lo arma
    /// con los valores de su propio asset de habilidad, así que por red
    /// solo viajan la posición y la dirección.
    /// </summary>
    [ClientRpc]
    private void PlaySpecialVfxClientRpc(Vector3 position, Vector3 direction)
    {
        SpecialAbility ability =
            player != null && player.ClassData != null
                ? player.ClassData.SpecialAbility
                : null;

        if (ability != null)
            ability.PlayVfx(player, position, direction);
    }

    // =========================================================
    // PASIVA DE CLASE
    // Se activa sola al llegar al nivel de la pasiva. El servidor decide el estado
    // y lo sincroniza (también a quien entre más tarde) para el efecto visual.
    // =========================================================

    private readonly NetworkVariable<bool> passiveActive = new NetworkVariable<bool>(false);

    private PassiveRuntime passiveRuntime;
    private float nextPassiveCheck;

    private PassiveAbility Passive =>
        player != null && player.ClassData != null ? player.ClassData.Passive : null;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        passiveActive.OnValueChanged += HandlePassiveActiveChanged;

        if (IsServer)
            passiveRuntime = Passive?.CreateRuntime(player);

        // Quien entra tarde recibe el valor ya sincronizado.
        Passive?.SetVisualActive(player, passiveActive.Value);

        // Avisos de desbloqueo (solo el dueño).
        if (IsOwner)
        {
            levelSync = GetComponent<CharacterStatsSyncController>();

            if (levelSync != null)
                levelSync.NetLevel.OnValueChanged += HandleOwnerLevelChanged;
        }
    }

    /// <summary>Dueño: avisa cuando se desbloquea la pasiva o el ataque especial.</summary>
    private void HandleOwnerLevelChanged(int previous, int current)
    {
        if (previous <= 0 || current <= previous)
            return;

        // Se compara contra todo el rango por si se suben varios niveles de golpe.
        PassiveAbility passive = Passive;

        if (passive != null && passive.UnlockLevel > previous && passive.UnlockLevel <= current)
            SkillNoticeUI.Show($"¡Pasiva desbloqueada: {passive.DisplayName}!");

        if (SpecialUnlockLevel > previous && SpecialUnlockLevel <= current)
            SkillNoticeUI.Show("¡Ataque especial desbloqueado!  (click derecho)");
    }

    public override void OnNetworkDespawn()
    {
        passiveActive.OnValueChanged -= HandlePassiveActiveChanged;

        if (levelSync != null)
            levelSync.NetLevel.OnValueChanged -= HandleOwnerLevelChanged;

        passiveRuntime?.Dispose();
        passiveRuntime = null;

        Passive?.SetVisualActive(player, false);

        base.OnNetworkDespawn();
    }

    private void HandlePassiveActiveChanged(bool previous, bool current)
    {
        Passive?.SetVisualActive(player, current);
    }

    private void Update()
    {
        if (!IsServer || !IsSpawned || player == null)
            return;

        if (Time.time < nextPassiveCheck)
            return;

        nextPassiveCheck = Time.time + 0.25f;

        PassiveAbility passive = Passive;

        bool active =
            passive != null &&
            !player.IsDead() &&
            stats.Level >= passive.UnlockLevel;

        if (passiveActive.Value != active)
            passiveActive.Value = active;

        passiveRuntime?.Tick(active);
    }

    // =========================================================
    // MELEE TARGET RESOLUTION — SERVER
    // =========================================================

    private Character FindMeleeTarget(Vector3 aimDirection)
    {
        float range = stats.AttackRange.Value;

        if (range <= 0f)
            return null;

        Vector3 origin =
            player.transform.position + Vector3.up;

        Collider[] hits = Physics.OverlapSphere(
            player.transform.position,
            range,
            enemyLayer,
            QueryTriggerInteraction.Collide);

        Character bestTarget = null;
        float bestScore = float.MaxValue;

        foreach (Collider hit in hits)
        {
            Character candidate =
                hit.GetComponentInParent<Character>();

            if (candidate == null ||
                candidate == player ||
                candidate.GetStats() == null ||
                candidate.GetStats().CurrentHealth <= 0)
            {
                continue;
            }

            Vector3 targetPosition =
                candidate.transform.position + Vector3.up;

            Vector3 toTarget = targetPosition - origin;
            float distance = toTarget.magnitude;

            if (distance > range || distance <= 0.001f)
                continue;

            float angle = Vector3.Angle(
                aimDirection, toTarget / distance);

            if (angle > attackConeAngle)
                continue;

            // Comprobar que el golpe alcanza al candidato y no
            // atraviesa un obstáculo antes de llegar a él.
            if (!ValidateMeleeHit(candidate, origin, toTarget, distance))
                continue;

            float score = angle * 2f + distance;

            if (score < bestScore)
            {
                bestScore = score;
                bestTarget = candidate;
            }
        }

        return bestTarget;
    }

    private bool ValidateMeleeHit(
        Character target,
        Vector3 origin,
        Vector3 toTarget,
        float distance)
    {
        if (!Physics.SphereCast(
            origin,
            sphereCastRadius,
            toTarget.normalized,
            out RaycastHit hit,
            distance + 0.1f,
            enemyLayer,
            QueryTriggerInteraction.Collide))
        {
            return false;
        }

        Character hitCharacter =
            hit.collider.GetComponentInParent<Character>();

        return hitCharacter == target;
    }

    private Vector3 NormalizeAim(Vector3 direction)
    {
        if (direction.sqrMagnitude < 0.001f)
            return player.transform.forward;

        return direction.normalized;
    }

    // =========================================================
    // ANIMATION / COOLDOWN
    // =========================================================

    private bool CanAttackNow()
    {
        return IsServer &&
               player != null &&
               !player.IsDead() &&
               Time.time >= nextAttackTime;
    }

    private void PlayAttackFeedback(AttackAnimKind kind)
    {
        WeaponData weapon = GetCurrentWeapon();

        float attackSpeed =
            weapon != null && weapon.AttackSpeed > 0f
                ? weapon.AttackSpeed
                : 1f;

        nextAttackTime = Time.time + 1f / attackSpeed;

        anim?.PlayAttack(kind);
    }

    private void OnDrawGizmosSelected()
    {
        if (stats == null)
            return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(
            transform.position,
            stats.AttackRange.Value);
    }
}
