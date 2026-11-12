using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Agregar al NPC de la escena. Cuando el jugador local se acerca muestra el
/// aviso "Presioná [E]"; al interactuar abre QuestGiverUI con una misión
/// secundaria al azar entre las disponibles.
///
/// No necesita collider ni NetworkObject: usa la distancia al jugador local.
/// </summary>
public class QuestGiverNPC : MonoBehaviour
{
    [SerializeField] private string npcName = "Misionero";

    [Tooltip("Distancia (en metros) a la que el jugador puede interactuar.")]
    [SerializeField] private float interactionRange = 3f;

    [SerializeField] private Key interactKey = Key.E;

    public string NpcName => npcName;

    private void Update()
    {
        QuestGiverUI ui = QuestGiverUI.Instance;

        if (ui == null)
            return;

        Player player = QuestGiverUI.GetLocalPlayer();

        if (player == null || player.IsDead())
        {
            ui.ClearPrompt(this);
            return;
        }

        bool inRange = IsInRange(player);

        // Interfaz abierta: cerrarla si el jugador se aleja de ESTE npc.
        if (ui.IsOpen)
        {
            if (ui.CurrentGiver == this && !inRange)
                ui.Close();

            ui.ClearPrompt(this);
            return;
        }

        // Inventario u otra interfaz abierta: no permitir interactuar.
        var input = player.GetComponent<PlayerInputController>();

        if (!inRange || (input != null && input.IsInputBlocked))
        {
            ui.ClearPrompt(this);
            return;
        }

        ui.SetPrompt(this, $"Presioná [{interactKey}] para hablar con {npcName}");

        if (Keyboard.current != null &&
            Keyboard.current[interactKey].wasPressedThisFrame)
        {
            ui.Open(this);
        }
    }

    private void OnDisable()
    {
        if (QuestGiverUI.Instance != null)
            QuestGiverUI.Instance.ClearPrompt(this);
    }

    private bool IsInRange(Player player)
    {
        Vector3 a = transform.position;
        Vector3 b = player.transform.position;

        a.y = 0f;
        b.y = 0f;

        return Vector3.Distance(a, b) <= interactionRange;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, interactionRange);
    }
}
