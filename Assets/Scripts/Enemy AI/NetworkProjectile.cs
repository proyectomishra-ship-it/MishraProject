
using System.Collections.Generic;
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

    // Mejoras de pasivas de clase (las configura la pasiva antes de hacer aparecer el proyectil).
    private float explosionRadius;
    private float explosionDamagePercent;
    private Color explosionColor = Color.white;
    private bool explosionHurtsPlayers;

    private bool pierce;
    private readonly HashSet<Character> piercedTargets = new HashSet<Character>();

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
        piercedTargets.Clear();
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
        
        Vector3 previousPosition = transform.position;   // <-- NUEVO (antes del movimiento)

        // El movimiento se simula en todas las instancias.
        transform.position +=
            transform.forward * speed * Time.deltaTime;

        // Lifetime exclusivamente del servidor.
        if (!IsServer || isDespawning)
            return;

        // Flecha explosiva: detona al chocar con cualquier superficie sólida.   // <-- NUEVO
        if (explosionRadius > 0f && !hasHit && CheckSurfaceImpact(previousPosition))
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

        if (pierce)
        {
            // Atraviesa: cada enemigo recibe un solo impacto.
            if (!piercedTargets.Add(target))
                return;
        }
        else
        {
            // Evita procesar más de un impacto antes del despawn.
            hasHit = true;
        }

        Debug.Log(
            $"<color=red>[Projectile] HIT {target.name} recibe {damage}</color>");

        receiver.TakeDamage(damage, owner);

        if (explosionRadius > 0f)
            Explode(target);

        if (!pierce)
            Despawn();
    }

    // =========================
    // PASIVAS DE CLASE
    // =========================

    /// <summary>Servidor: el proyectil explota al impactar dañando el área.</summary>
    public void SetExplosion(float radius, float damagePercent, Color color, bool hurtPlayers)
    {
        explosionRadius = radius;
        explosionDamagePercent = damagePercent;
        explosionColor = color;
        explosionHurtsPlayers = hurtPlayers;
    }

    /// <summary>Servidor: el proyectil atraviesa enemigos en vez de detenerse en el primero.</summary>
    public void SetPierce(bool value)
    {
        pierce = value;
    }

    private void Explode(Character directTarget)
    {
        ExplodeAt(transform.position, directTarget);

    }

    private void ExplodeAt(Vector3 center, Character directTarget)
    {
        var targets = SpecialAbilityEffects.FindEnemiesInRadius(
            owner, center, explosionRadius, damageLayers);

        // El enemigo golpeado ya recibió el daño completo de la flecha.
        if (directTarget != null)
            targets.Remove(directTarget);

        if (!explosionHurtsPlayers)
            targets.RemoveAll(t => t is Player);

        SpecialAbilityEffects.ApplyDamage(targets, owner, damage * explosionDamagePercent);

        if (owner is Player shooter)
            shooter.PlayImpactVfx(center, explosionRadius, explosionColor);
    }

    private bool CheckSurfaceImpact(Vector3 previousPosition)
    {
        Vector3 delta = transform.position - previousPosition;
        float distance = delta.magnitude;

        if (distance < 0.0001f)
            return false;

        RaycastHit[] hits = Physics.RaycastAll(
            previousPosition, delta / distance, distance, ~0, QueryTriggerInteraction.Ignore);

        bool found = false;
        RaycastHit best = default;

        foreach (RaycastHit hit in hits)
        {
            // Personajes (incluido el tirador): los maneja OnTriggerEnter.
            if (hit.collider.GetComponentInParent<Character>() != null)
                continue;

            if (!found || hit.distance < best.distance)
            {
                best = hit;
                found = true;
            }
        }

        if (!found)
            return false;

        hasHit = true;

        // Se separa un poco de la superficie para que el área no quede medio enterrada.
        ExplodeAt(best.point + best.normal * 0.1f, null);
        Despawn();

        return true;
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
