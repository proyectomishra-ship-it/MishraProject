using UnityEngine;
using Unity.Netcode;

public class TargetingController : NetworkBehaviour
{
    private Character character;
    private Camera mainCamera;

    [SerializeField] private LayerMask enemyLayer;

  
    private Character currentTarget;

    public Character CurrentTarget => currentTarget;

    public void Initialize(Character character)
    {
        this.character = character;
    }

    public override void OnNetworkSpawn()
    {
        
        if (IsOwner)
            mainCamera = Camera.main;
    }

    public void UpdateTarget()
    {
        if (IsOwner)
            UpdateTargetAsOwner();
    }

    
    private void UpdateTargetAsOwner()
    {
        if (mainCamera == null) return;

        float range = character.GetStats().AttackRange.Value;
        Ray ray = mainCamera.ScreenPointToRay(
            new Vector3(Screen.width / 2f, Screen.height / 2f));

        if (Physics.Raycast(ray, out RaycastHit hit, range, enemyLayer))
        {
            Character target = hit.collider.GetComponent<Character>();

            if (target != null && target.NetworkObject != null)
            {
                
                currentTarget = target;

                
                SetTargetServerRpc(target.NetworkObject.NetworkObjectId);

                Debug.DrawRay(ray.origin, ray.direction * hit.distance, Color.red, 0.5f);
                Debug.DrawRay(hit.point, Vector3.up * 0.5f, Color.red, 0.5f);
            }
        }
        else
        {
            currentTarget = null;
            SetTargetServerRpc(ulong.MaxValue); 
            Debug.DrawRay(ray.origin, ray.direction * range, Color.green, 0.1f);
        }
    }

   
    [ServerRpc]
    private void SetTargetServerRpc(ulong targetNetworkObjectId)
    {
        
        if (targetNetworkObjectId == ulong.MaxValue)
        {
            currentTarget = null;
            return;
        }

   
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects
            .TryGetValue(targetNetworkObjectId, out NetworkObject networkObject))
        {
            Character target = networkObject.GetComponent<Character>();

            if (target == null)
            {
                currentTarget = null;
                return;
            }

           
            float maxRange = character.GetStats().AttackRange.Value;
            float distance = Vector3.Distance(
                character.transform.position,
                target.transform.position);

            if (distance <= maxRange)
                currentTarget = target;
            else
            {
                currentTarget = null;
                Debug.LogWarning($"[TargetingController] Target rechazado: " +
                                 $"distancia {distance:F1} supera el rango {maxRange:F1}");
            }
        }
        else
        {
            currentTarget = null;
        }
    }
}