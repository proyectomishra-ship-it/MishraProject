using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Crea la barra de vida flotante del enemigo en cada cliente (ver EnemyOverheadBar).
/// La barra se arma por código: el campo "healthBarPrefab" quedó solo para no perder
/// la referencia de los prefabs y ya no se usa.
/// </summary>
public class EnemyHealthBarSpawner : NetworkBehaviour
{
    [Tooltip("OBSOLETO: la barra ahora se arma por código. Se puede ignorar.")]
    [SerializeField]
    private EnemyHealthBarController healthBarPrefab;

    private EnemyOverheadBar instance;

    public override void OnNetworkSpawn()
    {
        if (!IsClient)
            return;

        CharacterStatsSyncController sync =
            GetComponent<CharacterStatsSyncController>();

        if (sync == null)
        {
            Debug.LogError(
                $"[{name}] Falta CharacterStatsSyncController");
            return;
        }

        instance = EnemyOverheadBar.Create(sync, transform);
    }

    public override void OnNetworkDespawn()
    {
        DestroyBar();
    }

    public override void OnDestroy()
    {
        DestroyBar();
        base.OnDestroy();
    }

    private void DestroyBar()
    {
        if (instance != null)
            Destroy(instance.gameObject);

        instance = null;
    }
}
