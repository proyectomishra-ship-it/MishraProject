using UnityEngine;
using Unity.Netcode;

public class EnemyHealthBarSpawner : NetworkBehaviour
{
    [SerializeField]
    private EnemyHealthBarController healthBarPrefab;

    private EnemyHealthBarController instance;

    public override void OnNetworkSpawn()
    {
        if (!IsClient)
            return;

        CreateBar();
    }

    private void CreateBar()
    {
        CharacterStatsSyncController sync =
            GetComponent<CharacterStatsSyncController>();

        if (sync == null)
        {
            Debug.LogError(
                $"[{name}] Falta CharacterStatsSyncController");
            return;
        }

        instance = Instantiate(healthBarPrefab);

        instance.Initialize(sync);

        EnemyHealthBarFollow follow =
            instance.GetComponent<EnemyHealthBarFollow>();

        follow.SetTarget(transform);
    }

    public override void OnNetworkDespawn()
    {
        if (instance != null)
        {
            Destroy(instance.gameObject);
        }
    }
}