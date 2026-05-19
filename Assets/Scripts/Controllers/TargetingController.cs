using UnityEngine;
using Unity.Netcode;

public class TargetingController : NetworkBehaviour
{
    private Character character;
    private Camera mainCamera;

    [SerializeField] private LayerMask enemyLayer;
    [SerializeField] private float updateInterval = 0.05f;

    [Header("Targeting")]
    [SerializeField] private float sphereRadius = 0.6f;
    [SerializeField] private float loseTargetDelay = 0.3f;

    private float updateTimer;
    private float loseTargetTimer;

    private Character currentTarget;
    public Character CurrentTarget => currentTarget;

    private ulong lastSentTargetId = ulong.MaxValue;

    public void Initialize(Character character)
    {
        this.character = character;
    }

    public override void OnNetworkSpawn()
    {
        if (!IsOwner) return;

        mainCamera = Camera.main;
        TryAutoAssignCharacter();
    }

    private void TryAutoAssignCharacter()
    {
        if (character != null) return;
        character = GetComponent<Character>();
    }

    private void Update()
    {
        if (!IsOwner) return;

        if (character == null)
        {
            TryAutoAssignCharacter();
            return;
        }

        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            return;
        }

        updateTimer += Time.deltaTime;

        if (updateTimer >= updateInterval)
        {
            updateTimer = 0f;
            UpdateTargetAsOwner();
        }
    }

    private void UpdateTargetAsOwner()
    {
        var stats = character.GetStats();
        if (stats == null) return;

        float range = stats.AttackRange.Value;
        if (range <= 0f) return;

        Vector3 origin = character.transform.position + Vector3.up * 1.5f;
        Vector3 direction = mainCamera.transform.forward;

        Ray ray = new Ray(origin, direction);

        bool isMelee = range <= 3f;

        bool hitSomething = false;
        Character detectedTarget = null;

        if (isMelee)
        {
            if (Physics.SphereCast(ray, sphereRadius, out RaycastHit hit, range, enemyLayer))
            {
                detectedTarget = hit.collider.GetComponent<Character>();
                hitSomething = detectedTarget != null;
            }
        }
        else
        {
            if (Physics.Raycast(ray, out RaycastHit hit, range, enemyLayer))
            {
                detectedTarget = hit.collider.GetComponent<Character>();
                hitSomething = detectedTarget != null;
            }
            else
            {
                // Aim assist leve
                if (Physics.SphereCast(ray, sphereRadius * 0.5f, out RaycastHit sHit, range, enemyLayer))
                {
                    detectedTarget = sHit.collider.GetComponent<Character>();
                    hitSomething = detectedTarget != null;
                }
            }
        }

        if (hitSomething && detectedTarget != null && detectedTarget.NetworkObject != null)
        {
            loseTargetTimer = 0f;

            ulong targetId = detectedTarget.NetworkObject.NetworkObjectId;
            currentTarget = detectedTarget;

            if (targetId != lastSentTargetId)
            {
                lastSentTargetId = targetId;
                SetTargetServerRpc(targetId);
            }

            return;
        }

        // Delay antes de perder target
        loseTargetTimer += updateInterval;

        if (loseTargetTimer >= loseTargetDelay)
        {
            currentTarget = null;

            if (lastSentTargetId != ulong.MaxValue)
            {
                lastSentTargetId = ulong.MaxValue;
                SetTargetServerRpc(ulong.MaxValue);
            }
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

        var spawnManager = NetworkManager.Singleton.SpawnManager;

        if (spawnManager.SpawnedObjects
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

            currentTarget = distance <= maxRange ? target : null;
        }
        else
        {
            currentTarget = null;
        }
    }

    public ulong GetCurrentTargetId()
    {
        if (currentTarget == null || currentTarget.NetworkObject == null)
            return ulong.MaxValue;

        return currentTarget.NetworkObject.NetworkObjectId;
    }

    public void ForceTarget(Character target)
    {
        currentTarget = target;
    }
}