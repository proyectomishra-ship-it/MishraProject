using UnityEngine;
using Unity.Netcode;

public class EnemyAIController : NetworkBehaviour
{
    private Enemy enemy;

    public void Initialize(Enemy enemy)
    {
        this.enemy = enemy;
    }

    private void Update()
    {
        
        if (!IsServer) return;

        // IA futura
    }
}