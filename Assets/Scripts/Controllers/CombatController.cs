using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

public class CombatController : NetworkBehaviour
{
    private Character character;
    private CharacterStats stats;

    [Header("Attack Config")]
    [SerializeField] private float heavyAttackMultiplier = 2f;
    [SerializeField] private float holdThreshold = 0.4f;

    [Header("Validation")]
    [SerializeField] private float attackConeAngle = 60f;
    [SerializeField] private float sphereCastRadius = 0.8f;
    [SerializeField] private LayerMask enemyLayer;

    private float attackHeldTime = 0f;
    private bool isHoldingAttack = false;
    private bool heavyConsumed = false;

    public void Initialize(Character character)
    {
        this.character = character;
        this.stats = character.GetStats();
    }

    // =========================
    // INPUT (CLIENTE)
    // =========================

    public void OnAttackPressed()
    {
        if (!IsOwner) return;
        AttackPressedServerRpc();
    }

    public void OnAttackHeld(float deltaTime)
    {
        if (!IsOwner) return;
        AttackHeldServerRpc(deltaTime);
    }

    public void OnAttackReleased()
    {
        if (!IsOwner) return;
        AttackReleasedServerRpc();
    }

    // =========================
    // SERVER RPC
    // =========================

    [ServerRpc]
    private void AttackPressedServerRpc(ServerRpcParams rpcParams = default)
    {
        isHoldingAttack = true;
        attackHeldTime = 0f;
        heavyConsumed = false;
    }

    [ServerRpc]
    private void AttackHeldServerRpc(float deltaTime)
    {
        if (!isHoldingAttack || heavyConsumed) return;

        attackHeldTime += deltaTime;

        if (attackHeldTime >= holdThreshold)
        {
            PerformAttack(true);
            heavyConsumed = true;
            isHoldingAttack = false;
        }
    }

    [ServerRpc]
    private void AttackReleasedServerRpc()
    {
        if (!isHoldingAttack) return;

        isHoldingAttack = false;

        if (!heavyConsumed)
        {
            PerformAttack(false);
        }
    }

    // =========================
    // CORE COMBAT (SERVER)
    // =========================

    private void PerformAttack(bool heavy)
    {
        if (!IsServer) return;

        // FIX: Usar primero el target que ya confirmó TargetingController (server-side).
        // Esa clase hace detección por cámara + validación de rango en SetTargetServerRpc.
        // Solo caemos a FindBestTarget si no hay target confirmado (p.ej. modo sin cámara / IA).
        Character target = character.GetComponent<TargetingController>()?.CurrentTarget;

        if (target == null)
        {
            target = CombatTargetingSystem.FindBestTarget(
                character,
                stats.AttackRange.Value,
                attackConeAngle,
                enemyLayer
            );
        }

        if (target == null)
        {
            Debug.LogWarning("[Combat] Sin target válido");
            return;
        }

        if (!ValidateHitWithSphereCast(target))
        {
            Debug.LogWarning("[Combat] SphereCast falló");
            return;
        }

        float damage = stats.Attack.Value * (heavy ? heavyAttackMultiplier : 1f);

        var receiver = target.GetComponent<DamageReceiver>();

        if (receiver == null)
        {
            Debug.LogError("[Combat] Target sin DamageReceiver");
            return;
        }
        Debug.Log($"[Combat] Target elegido: {target.name} | Dist: {Vector3.Distance(transform.position, target.transform.position):F2}");
        receiver.TakeDamage(damage, character);

        Debug.Log($"[Combat] {character.name} hizo {damage} a {target.name}");
    }


    private bool ValidateHitWithSphereCast(Character target)
    {

        Vector3 origin = transform.position + Vector3.up * 1.0f;

        // DirecciÃ³n hacia el target
        Vector3 targetPos = target.transform.position + Vector3.up * 1.0f;
        Vector3 dir = (targetPos - origin).normalized;

        // Distancia REAL entre centros
        float distance = Vector3.Distance(origin, targetPos);

        // Ajuste por tamaÃ±o de cÃ¡psulas (MUY IMPORTANTE)
        float effectiveRange = distance + 0.5f;

        Debug.Log($"[Combat] Cast -> Dist: {distance:F2} | Range: {stats.AttackRange.Value}");

        if (Physics.SphereCast(
            origin,
            sphereCastRadius,
            dir,
            out RaycastHit hit,
            effectiveRange,
            enemyLayer,
            QueryTriggerInteraction.Ignore))
        {
            Character hitChar = hit.collider.GetComponent<Character>();

            Debug.Log($"[Combat] Hit: {hit.collider.name}");

            return hitChar != null && hitChar == target;
        }

        Debug.DrawLine(origin, origin + dir * effectiveRange, Color.red, 1f);

        return false;
    }
    // =========================
    // DEBUG
    // =========================

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, stats != null ? stats.AttackRange.Value : 2f);
    }

    // =========================
    // PUBLIC API 
    // =========================

    public void Attack()
    {
        OnAttackPressed();
        OnAttackReleased();
    }

    public void SpecialAttack()
    {
        if (!IsOwner) return;
        SpecialAttackServerRpc();
    }

    [ServerRpc]
    private void SpecialAttackServerRpc()
    {
        PerformSpecialAttack();
    }

    private void PerformSpecialAttack()
    {
        if (!IsServer) return;

        Character target = character.GetComponent<TargetingController>()?.CurrentTarget;

        if (target == null)
        {
            target = CombatTargetingSystem.FindBestTarget(
                character,
                stats.AttackRange.Value,
                attackConeAngle,
                enemyLayer
            );
        }

        if (target == null)
        {
            Debug.LogWarning("[Combat] SpecialAttack sin target");
            return;
        }

        if (!ValidateHitWithSphereCast(target))
        {
            Debug.LogWarning("[Combat] SpecialAttack spherecast fallÃ³");
            return;
        }

        float damage = stats.Attack.Value * 1.5f;

        var receiver = target.GetComponent<DamageReceiver>();
        if (receiver == null) return;

        receiver.TakeDamage(damage, character);

        Debug.Log($"[Combat] SPECIAL {character.name} hizo {damage} a {target.name}");
    }
}