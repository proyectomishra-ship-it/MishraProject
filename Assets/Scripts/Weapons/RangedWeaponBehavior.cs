using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Comportamiento de armas ranged.
/// Usado principalmente por NPCs/enemigos.
/// </summary>
public class RangedWeaponBehavior : IWeaponBehavior
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

    public void PerformAttack(
        Character attacker,
        Character target)
    {
        float damage =
            attacker.GetStats().Attack.Value;

        SpawnProjectile(
            weapon.ProjectilePrefab,
            attacker,
            target,
            damage);
    }

    public void PerformHeavyAttack(
        Character attacker,
        Character target,
        float multiplier)
    {
        float damage =
            attacker.GetStats().Attack.Value *
            multiplier;

        SpawnProjectile(
            weapon.ProjectilePrefab,
            attacker,
            target,
            damage);
    }

    public void PerformSpecialAttack(
        Character attacker,
        Character target)
    {
        if (!attacker.UseMana(weapon.ManaCost))
            return;

        float damage =
            attacker.GetStats().Attack.Value *
            weapon.SpecialMultiplier;

        SpawnProjectile(
            weapon.GetSpecialProjectile(),
            attacker,
            target,
            damage);
    }

    private void SpawnProjectile(
        GameObject prefab,
        Character attacker,
        Character target,
        float damage)
    {
        if (prefab == null)
            return;

        Vector3 origin =
            spawnOrigin.position + Vector3.up * 1.5f;

        Vector3 direction =
            (
                target.transform.position +
                Vector3.up -
                origin
            ).normalized;

        GameObject obj = Object.Instantiate(
            prefab,
            origin,
            Quaternion.LookRotation(direction));

        if (!obj.TryGetComponent(
            out NetworkProjectile projectile))
        {
            Debug.LogError(
                "[RangedWeapon] El prefab no tiene NetworkProjectile.");

            Object.Destroy(obj);
            return;
        }

        if (!obj.TryGetComponent(
            out NetworkObject netObj))
        {
            Debug.LogError(
                "[RangedWeapon] El prefab no tiene NetworkObject.");

            Object.Destroy(obj);
            return;
        }

        netObj.Spawn();

        projectile.Initialize(
            attacker,
            damage,
            direction);

        Debug.Log(
            $"[RangedWeapon] Projectile spawned → {target.name}");
    }
}