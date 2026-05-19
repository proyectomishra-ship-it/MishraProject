using UnityEngine;
using Unity.Netcode;

public class MagicWeaponBehavior : IWeaponBehavior
{
    private readonly WeaponData weapon;

    private readonly Transform castOrigin;

    public MagicWeaponBehavior(
        WeaponData weapon,
        Transform castOrigin)
    {
        this.weapon = weapon;
        this.castOrigin = castOrigin;
    }

    public void ExecuteAttack(
        Character attacker,
        Character target,
        bool heavy)
    {
        if (weapon == null)
            return;

        ResourceController resources =
            attacker.GetResourceController();

        if (!resources.UseMana(
            weapon.ManaCost))
        {
            Debug.Log(
                "[Magic] Not enough mana");

            return;
        }

        float damage =
            attacker.GetStats().Attack.Value +
            attacker.GetStats().Intelligence.Value;

        if (heavy)
        {
            damage *= weapon.HeavyMultiplier;
        }

        SpawnProjectile(
            weapon.ProjectilePrefab,
            attacker,
            target,
            damage,
            heavy);
    }

    public void ExecuteSpecialAttack(
        Character attacker,
        Character target)
    {
        if (weapon == null)
            return;

        float damage =
            attacker.GetStats().Attack.Value *
            weapon.SpecialMultiplier;

        SpawnProjectile(
            weapon.SpecialProjectilePrefab != null
                ? weapon.SpecialProjectilePrefab
                : weapon.ProjectilePrefab,
            attacker,
            target,
            damage,
            true);
    }

    private void SpawnProjectile(
        GameObject prefab,
        Character attacker,
        Character target,
        float damage,
        bool heavy)
    {
        if (prefab == null)
            return;

        Vector3 origin =
            castOrigin.position +
            Vector3.up * 1.5f;

        Vector3 direction =
            (
                target.transform.position +
                Vector3.up -
                origin
            ).normalized;

        GameObject obj =
            Object.Instantiate(
                prefab,
                origin,
                Quaternion.LookRotation(direction));

        if (!obj.TryGetComponent(
            out NetworkObject netObj))
        {
            Debug.LogError(
                "[Magic] Missing NetworkObject");

            Object.Destroy(obj);

            return;
        }

        if (!obj.TryGetComponent(
            out NetworkProjectile projectile))
        {
            Debug.LogError(
                "[Magic] Missing NetworkProjectile");

            Object.Destroy(obj);

            return;
        }

        AttackData attackData =
            new AttackData
            {
                Attacker = attacker,
                Target = target,
                Damage = damage,
                DamageType = weapon.DamageType,
                IsHeavy = heavy,
                IsCritical = false,
                HitPoint = target.transform.position
            };

        netObj.Spawn();

        projectile.Initialize(
            attackData,
            direction,
           weapon.ProjectileSpeed);
    }
}