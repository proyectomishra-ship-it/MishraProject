
using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(NetworkObject))]
public class NetworkProjectile : NetworkBehaviour
{
    [Header("Configuración")]
    [SerializeField, Min(0.1f)] private float speed = 10f;
    [SerializeField, Min(0.1f)] private float lifetime = 5f;
    [SerializeField, Min(0f)] private float damage = 10f;

    [Header("Detección de impacto")]
    [Tooltip("Capas cuyos colliders pueden recibir daño del proyectil.")]
    [SerializeField] private LayerMask damageLayers = ~0;

    private float timer;
    private Character owner;

    private bool hasHit;
    private bool isDespawning;

    // =========================
    // INITIALIZATION
    // =========================

    public void Initialize(
        Character owner,
        float damage,
        Vector3 direction)
    {
        this.owner = owner;
        this.damage = damage;

        timer = 0f;
        hasHit = false;
        isDespawning = false;

        if (direction.sqrMagnitude > 0.001f)
            transform.forward = direction.normalized;

        Debug.Log(
            $"[Projectile] Inicializado por {owner.name} | Damage: {damage}");
    }

    // =========================
    // NETWORK LIFECYCLE
    // =========================

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // Cada Spawn comienza un nuevo ciclo de vida.
        timer = 0f;
        hasHit = false;
        isDespawning = false;
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();

        timer = 0f;
        hasHit = false;
        isDespawning = false;
        owner = null;
    }

    // =========================
    // MOVEMENT / LIFETIME
    // =========================

    private void Update()
    {
        if (!IsSpawned)
            return;

        // El movimiento se simula en todas las instancias.
        transform.position +=
            transform.forward * speed * Time.deltaTime;

        // Lifetime exclusivamente del servidor.
        if (!IsServer || isDespawning)
            return;

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
        if (!IsServer || hasHit || isDespawning)
            return;

        // Comprobar si el collider pertenece a una capa permitida.
        int layerBit = 1 << other.gameObject.layer;

        if ((damageLayers.value & layerBit) == 0)
            return;

        Debug.Log($"[Projectile] Colisión con {other.name}");

        Character target =
            other.GetComponentInParent<Character>();

        if (target == null)
            return;

        if (target == owner)
            return;

        DamageReceiver receiver =
            target.GetComponent<DamageReceiver>();

        if (receiver == null)
            return;

        // Evita procesar más de un impacto antes del despawn.
        hasHit = true;

        Debug.Log(
            $"<color=red>[Projectile] HIT {target.name} recibe {damage}</color>");

        receiver.TakeDamage(damage, owner);

        Despawn();
    }

    // =========================
    // DESPAWN
    // =========================

    private void Despawn()
    {
        if (isDespawning)
            return;

        isDespawning = true;

        if (NetworkObject != null && NetworkObject.IsSpawned)
        {
            NetworkObject.Despawn();
        }
        else
        {
            Destroy(gameObject);
        }
    }
}
