using UnityEngine;
using Unity.Netcode;

public class TargetingController : NetworkBehaviour
{
    private Character character;

    private Camera mainCamera;

    [SerializeField]
    private LayerMask enemyLayer;

    [SerializeField]
    private float updateInterval = 0.05f;

    [Header("Targeting")]
    [SerializeField]
    private float sphereRadius = 0.6f;

    [SerializeField]
    private float loseTargetDelay = 0.3f;

    private float updateTimer;

    private float loseTargetTimer;

    private Character currentTarget;

    public Character CurrentTarget => currentTarget;

    private ulong lastSentTargetId =
        ulong.MaxValue;

    public void Initialize(Character character)
    {
        this.character = character;
    }

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
            return;

        mainCamera = Camera.main;

        TryAssignCharacter();
    }

    private void TryAssignCharacter()
    {
        if (character != null)
            return;

        character = GetComponent<Character>();
    }

    private void Update()
    {
        if (!IsOwner)
            return;

        if (character == null)
        {
            TryAssignCharacter();
            return;
        }

        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            return;
        }

        updateTimer += Time.deltaTime;

        if (updateTimer < updateInterval)
            return;

        updateTimer = 0f;

        UpdateTarget();
    }

    private void UpdateTarget()
    {
        Character detected =
            FindTarget();

        if (detected != null)
        {
            loseTargetTimer = 0f;

            currentTarget = detected;

            ulong targetId =
                detected.NetworkObjectId;

            if (targetId != lastSentTargetId)
            {
                lastSentTargetId = targetId;

                SetTargetServerRpc(targetId);
            }

            return;
        }

        loseTargetTimer += updateInterval;

        if (loseTargetTimer < loseTargetDelay)
            return;

        currentTarget = null;

        if (lastSentTargetId == ulong.MaxValue)
            return;

        lastSentTargetId = ulong.MaxValue;

        SetTargetServerRpc(ulong.MaxValue);
    }

    private Character FindTarget()
    {
        CharacterStats stats =
            character.GetStats();

        if (stats == null)
            return null;

        float range =
            stats.AttackRange.Value;

        Vector3 origin =
            character.transform.position +
            Vector3.up * 1.5f;

        Vector3 direction =
            mainCamera.transform.forward;

        Ray ray =
            new Ray(origin, direction);

        bool isMelee =
            range <= 3f;

        RaycastHit hit;

        bool success;

        if (isMelee)
        {
            success =
                Physics.SphereCast(
                    ray,
                    sphereRadius,
                    out hit,
                    range,
                    enemyLayer);
        }
        else
        {
            success =
                Physics.Raycast(
                    ray,
                    out hit,
                    range,
                    enemyLayer);

            if (!success)
            {
                success =
                    Physics.SphereCast(
                        ray,
                        sphereRadius * 0.5f,
                        out hit,
                        range,
                        enemyLayer);
            }
        }

        if (!success)
            return null;

        return hit.collider
            .GetComponentInParent<Character>();
    }

    [ServerRpc]
    private void SetTargetServerRpc(
        ulong targetNetworkObjectId)
    {
        if (targetNetworkObjectId ==
            ulong.MaxValue)
        {
            currentTarget = null;
            return;
        }

        if (!NetworkManager.Singleton.SpawnManager
            .SpawnedObjects.TryGetValue(
                targetNetworkObjectId,
                out NetworkObject networkObject))
        {
            currentTarget = null;
            return;
        }

        Character target =
            networkObject
            .GetComponent<Character>();

        if (target == null)
        {
            currentTarget = null;
            return;
        }

        float maxRange =
            character.GetStats()
            .AttackRange.Value;

        float distance =
            Vector3.Distance(
                character.transform.position,
                target.transform.position);

        currentTarget =
            distance <= maxRange
                ? target
                : null;
    }

    public ulong GetCurrentTargetId()
    {
        if (currentTarget == null)
            return ulong.MaxValue;

        return currentTarget.NetworkObjectId;
    }

    public void ForceTarget(Character target)
    {
        currentTarget = target;
    }
}