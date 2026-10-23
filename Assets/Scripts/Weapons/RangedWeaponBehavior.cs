
using UnityEngine;
using Unity.Netcode;

public class RangedWeaponBehavior :
    IWeaponBehavior,
    IAimedWeaponBehavior
{
    private readonly WeaponData weapon;
    private readonly Transform spawnOrigin;

    public RangedWeaponBehavior(
        WeaponData weapon,
        Transform spawnOrigin)
    {
        this.weapon = weapon;
        this.spawnOrigin = spawnOrigin;
    }

    // =========================================================
    // API ORIGINAL — enemigos y ataques con objetivo
    // =========================================================

    public void PerformAttack(Character attacker, Character target)
    {
        if (target == null)
            return;

        SpawnProjectile(
            weapon != null ? weapon.ProjectilePrefab : null,
            attacker,
            target.transform.position + Vector3.up,
            attacker.GetStats().Attack.Value);
    }

    public void PerformHeavyAttack(
        Character attacker,
        Character target,
        float multiplier)
    {
        if (target == null)
            return;

        SpawnProjectile(
            weapon != null ? weapon.ProjectilePrefab : null,
            attacker,
            target.transform.position + Vector3.up,
            attacker.GetStats().Attack.Value * multiplier);
    }

    public void PerformSpecialAttack(
        Character attacker,
        Character target)
    {
        if (weapon == null || target == null)
            return;

        if (!attacker.UseMana(weapon.ManaCost))
            return;

        SpawnProjectile(
            weapon.GetSpecialProjectile(),
            attacker,
            target.transform.position + Vector3.up,
            attacker.GetStats().Attack.Value *
            weapon.SpecialMultiplier);
    }

    // =========================================================
    // API DIRECCIONAL — jugador
    // =========================================================

    public void PerformAimedAttack(
        Character attacker,
        Vector3 direction)
    {
        SpawnProjectile(
            weapon != null ? weapon.ProjectilePrefab : null,
            attacker,
            direction,
            attacker.GetStats().Attack.Value,
            true);
    }

    public void PerformAimedHeavyAttack(
        Character attacker,
        Vector3 direction,
        float multiplier)
    {
        SpawnProjectile(
            weapon != null ? weapon.ProjectilePrefab : null,
            attacker,
            direction,
            attacker.GetStats().Attack.Value * multiplier,
            true);
    }

    public void PerformAimedSpecialAttack(
        Character attacker,
        Vector3 direction)
    {
        if (weapon == null)
            return;

        SpawnProjectile(
            weapon.GetSpecialProjectile(),
            attacker,
            direction,
            attacker.GetStats().Attack.Value *
            weapon.SpecialMultiplier,
            true);
    }

    // =========================================================
    // PROJECTILE SPAWNING
    // =========================================================

    private void SpawnProjectile(
        GameObject prefab,
        Character attacker,
        Vector3 targetPosition,
        float damage)
    {
        if (prefab == null || targetPosition == Vector3.zero)
            return;

        Vector3 origin =
            spawnOrigin.position + Vector3.up * 1.5f;

        Vector3 direction =
            (targetPosition - origin).normalized;

        SpawnProjectile(
            prefab, attacker, direction, damage, true);
    }

    private void SpawnProjectile(
        GameObject prefab,
        Character attacker,
        Vector3 direction,
        float damage,
        bool isDirection)
    {
        if (!NetworkManager.Singleton ||
            !NetworkManager.Singleton.IsServer)
        {
            return;
        }

        if (prefab == null)
        {
            Debug.LogWarning(
                "[RangedWeapon] No hay un prefab de proyectil asignado.");
            return;
        }

        if (direction.sqrMagnitude < 0.001f)
            direction = attacker.transform.forward;

        direction.Normalize();

        Vector3 origin =
            spawnOrigin.position + Vector3.up * 1.5f;

        GameObject obj = Object.Instantiate(
            prefab,
            origin,
            Quaternion.LookRotation(direction));

        if (!obj.TryGetComponent<NetworkObject>(out var netObj) ||
            !obj.TryGetComponent<NetworkProjectile>(out var projectile))
        {
            Debug.LogError(
                "[RangedWeapon] El prefab necesita NetworkObject " +
                "y NetworkProjectile.");

            Object.Destroy(obj);
            return;
        }

        netObj.Spawn();

        projectile.Initialize(attacker, damage, direction);
    }
}
