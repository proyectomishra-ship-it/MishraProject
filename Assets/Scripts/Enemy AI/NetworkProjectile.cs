using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(NetworkObject))]
public class NetworkProjectile : NetworkBehaviour
{
    [Header("Config")]
    [SerializeField] private float speed = 10f;
    [SerializeField] private float lifetime = 5f;
    [SerializeField] private float damage = 10f;

    private float timer;

    private Character owner;

    public void Initialize(Character owner, float damage, Vector3 direction)
    {
        this.owner = owner;
        this.damage = damage;

        transform.forward = direction;

        Debug.Log($"[Projectile] Inicializado por {owner.name} | Damage: {damage}");
    }

    private void Update()
    {
        // El movimiento corre en TODAS las instancias (servidor y clientes).
        // El proyectil no tiene NetworkTransform, asi que si solo se movia en
        // el servidor, los clientes lo veian quieto en el punto de disparo.
        // La direccion ya llega con el spawn (Quaternion.LookRotation al
        // instanciar), por lo que cada cliente simula el mismo recorrido.
        // El dano, el lifetime y el despawn siguen siendo solo del servidor.
        transform.position += transform.forward * speed * Time.deltaTime;

        if (!IsServer) return;

        timer += Time.deltaTime;
        if (timer >= lifetime)
        {
            Debug.Log("[Projectile] Lifetime expirado");
            Despawn();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;

        Debug.Log($"[Projectile] Colision con {other.name}");

        var target = other.GetComponentInParent<Character>();

        if (target == null)
        {
            Debug.Log("[Projectile] Impacto sin Character");
            return;
        }

        if (target == owner)
        {
            Debug.Log("[Projectile] Ignorado (self hit)");
            return;
        }

        var receiver = target.GetComponent<DamageReceiver>();

        if (receiver == null)
        {
            Debug.LogError("[Projectile] Target sin DamageReceiver");
            return;
        }

        Debug.Log($"<color=red>[Projectile] HIT  {target.name} recibe {damage}</color>");

        receiver.TakeDamage(damage, owner);

        Despawn();
    }

    private void Despawn()
    {
        if (NetworkObject != null && NetworkObject.IsSpawned)
            NetworkObject.Despawn();
        else
            Destroy(gameObject);
    }
}