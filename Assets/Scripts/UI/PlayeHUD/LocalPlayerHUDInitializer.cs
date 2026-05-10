using UnityEngine;
using Unity.Netcode;

public class LocalPlayerHUDInitializer : NetworkBehaviour
{
    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
            return;

        PlayerHUD hud = FindFirstObjectByType<PlayerHUD>();

        if (hud == null)
        {
            Debug.LogError("[HUD] No se encontró PlayerHUD en escena");
            return;
        }

        CharacterStatsSyncController sync =
            GetComponent<CharacterStatsSyncController>();

        if (sync == null)
        {
            Debug.LogError("[HUD] Falta CharacterStatsSyncController");
            return;
        }

        hud.Initialize(sync);
    }
}