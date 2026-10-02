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

        // Crear la barra como hija del Player del enemigo.
        instance = Instantiate(
            healthBarPrefab,
            transform
        );

        instance.Initialize(sync);

        EnemyHealthBarFollow follow =
            instance.GetComponent<EnemyHealthBarFollow>();

        if (follow != null)
        {
            follow.SetTarget(transform);
        }
        else
        {
            Debug.LogError(
                $"[{name}] Falta EnemyHealthBarFollow en la barra");
        }
    }

    public override void OnNetworkDespawn()
    {
        if (instance != null)
        {
            Destroy(instance.gameObject);
        }
    }


}
