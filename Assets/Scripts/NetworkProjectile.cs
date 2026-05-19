using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(NetworkObject))]
public class NetworkProjectile : NetworkBehaviour
{
    [Header("Config")]
    [SerializeField] private float speed = 10f;
    [SerializeField] private float lifetime = 5f;

    private float timer;

    private AttackData attackData;

    private bool initialized;

    // =========================
    // INITIALIZE
    // =========================

    public void Initialize(
        AttackData attackData,
        Vector3 direction,
        float projectileSpeed)
    {
        this.attackData = attackData;

        speed = projectileSpeed;

        transform.forward = direction.normalized;

        initialized = true;

        Debug.Log(
            $"[Projectile] Inicializado por " +
            $"{attackData.Attacker?.name} | " +
            $"Damage: {attackData.Damage} | " +
            $"Type: {attackData.DamageType}");
    }

    // =========================
    // UPDATE
    // =========================

    private void Update()
    {
        if (!IsServer)
            return;

        if (!initialized)
            return;

        transform.position +=
            transform.forward *
            speed *
            Time.deltaTime;

        timer += Time.deltaTime;

        if (timer >= lifetime)
        {
            Debug.Log("[Projectile] Lifetime expirado");

            Despawn();
        }
    }

    // =========================
    // COLLISION
    // =========================

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer)
            return;

        Debug.Log(
            $"[Projectile] Colision con {other.name}");

        Character target =
            other.GetComponentInParent<Character>();

        if (target == null)
        {
            Debug.Log(
                "[Projectile] Impacto sin Character");

            return;
        }

        // Ignore self hit
        if (target == attackData.Attacker)
        {
            Debug.Log(
                "[Projectile] Ignorado (self hit)");

            return;
        }

        DamageReceiver receiver =
            target.GetComponent<DamageReceiver>();

        if (receiver == null)
        {
            Debug.LogError(
                "[Projectile] Target sin DamageReceiver");

            return;
        }

        Debug.Log(
            $"<color=red>[Projectile] HIT {target.name} recibe " +
            $"{attackData.Damage}</color>");

        // =========================
        // APPLY DAMAGE
        // =========================

        AttackData finalAttackData =
            attackData;

        finalAttackData.Target = target;

        finalAttackData.HitPoint =
            transform.position;

        receiver.TakeDamage(finalAttackData);

        Despawn();
    }

    // =========================
    // DESPAWN
    // =========================

    private void Despawn()
    {
        if (NetworkObject != null &&
            NetworkObject.IsSpawned)
        {
            NetworkObject.Despawn();
        }
        else
        {
            Destroy(gameObject);
        }
    }
}