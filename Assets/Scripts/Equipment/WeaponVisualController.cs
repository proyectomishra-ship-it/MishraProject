using System.Collections;
using UnityEngine;

/// <summary>
/// Muestra el visual del arma equipada en la mano del jugador.
///
/// SETUP (una vez, en los tres prefabs) — ya hecho:
///   1. Componente en el root del prefab (Warrior / Hunter / Mage).
///   2. "Placeholder Weapon" → el objeto "Cube" hijo del Capsule.
///      Si no se asigna, lo busca automáticamente por nombre.
///
/// El Cube actúa como socket:
///   • Sin prefab real  → se activa el Cube (placeholder visible).
///   • Con prefab real  → el Cube se usa solo como pivot;
///                        su MeshRenderer se oculta y el prefab
///                        se instancia dentro con local pos/rot cero.
///   • Sin arma         → el Cube se desactiva por completo.
///
/// NOTA SOBRE JUGADORES REMOTOS:
///   El visual SOLO se muestra para el jugador local (IsOwner = true).
///   OcultarRenderersRemotos() oculta el modelo completo de los otros
///   jugadores; cuando esa función sea actualizada para mostrar cuerpos,
///   basta con quitar los dos checks "if (!equipmentController.IsOwner)"
///   para que todos vean las armas de todos.
/// </summary>
public class WeaponVisualController : MonoBehaviour
{
    [Header("Referencias — asignar en el Inspector")]
    [Tooltip("El objeto 'Cube' que ya existe en el prefab, hijo del Capsule.")]
    [SerializeField] private GameObject placeholderWeapon;

    // ── Estado interno ────────────────────────────────────────────────────────

    private EquipmentController equipmentController;
    private MeshRenderer placeholderRenderer;
    private GameObject spawnedVisual;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        // Buscar el Cube lo antes posible (fallback si no fue asignado en Inspector)
        if (placeholderWeapon == null)
        {
            Transform found = FindInChildren(transform, "Cube");
            if (found != null)
                placeholderWeapon = found.gameObject;
            else
                Debug.LogWarning($"[WeaponVisual] {name}: no se encontró 'Cube'. " +
                                  "Asigná el campo 'Placeholder Weapon' en el Inspector.");
        }

        if (placeholderWeapon != null)
            placeholderRenderer = placeholderWeapon.GetComponent<MeshRenderer>();

        // Suscribir en Awake: Player es el último en la lista de componentes,
        // así que Character.Awake() (que inicializa equipmentController) ya corrió.
        equipmentController = GetComponent<EquipmentController>();
        if (equipmentController == null)
        {
            Debug.LogError($"[WeaponVisual] {name}: falta EquipmentController en el mismo GameObject.");
            return;
        }

        equipmentController.OnSlotChanged += HandleSlotChanged;
    }

    private IEnumerator Start()
    {
        // Esperar 2 frames:
        //   • OnNetworkSpawn de EquipmentController ya corrió (suscripción a NetworkVariable).
        //   • OnNetworkSpawn de Player ya corrió (EquiparArmaInicial en el servidor,
        //     OcultarRenderersRemotos para remotos).
        //   • La NetworkVariable ya tiene el valor inicial propagado.
        yield return null;
        yield return null;

        if (equipmentController == null) yield break;

        // FIX: Solo actualizar el visual para el jugador local.
        // Para remotos, OcultarRenderersRemotos() oculta todo el modelo.
        // Sin este check, SetCubeMeshVisible(true) re-habilitaría el renderer
        // del Cube después de que OcultarRenderersRemotos lo deshabilitó,
        // dejando el arma flotando en el aire sin cuerpo.
        if (!equipmentController.IsOwner) yield break;

        var weapon = equipmentController.GetEquippedWeapon();

        Debug.Log($"[WeaponVisual] {name} — refresh inicial: " +
                  $"arma = '{weapon?.ItemName ?? "ninguna"}'");

        RefreshWeaponVisual(weapon);
    }

    private void OnDestroy()
    {
        if (equipmentController != null)
            equipmentController.OnSlotChanged -= HandleSlotChanged;

        if (spawnedVisual != null)
            Destroy(spawnedVisual);
    }

    // ── Handlers ──────────────────────────────────────────────────────────────

    private void HandleSlotChanged(EquipmentSlot slot, IEquippable item)
    {
        if (slot != EquipmentSlot.Weapon) return;

        // FIX: misma razón que Start() — evitar el arma flotante en remotos.
        // HandleSlotChanged se llama en TODOS los clientes cuando la NetworkVariable
        // cambia. Sin este check, el Cube del jugador remoto se activaría
        // sobreescribiendo lo que hizo OcultarRenderersRemotos().
        if (!equipmentController.IsOwner) return;

        Debug.Log($"[WeaponVisual] {name} — OnSlotChanged: " +
                  $"arma = '{(item as WeaponData)?.ItemName ?? "ninguna"}'");

        RefreshWeaponVisual(item as WeaponData);
    }

    // ── Lógica principal ──────────────────────────────────────────────────────

    private void RefreshWeaponVisual(WeaponData weapon)
    {
        // Destruir prefab real anterior
        if (spawnedVisual != null)
        {
            Destroy(spawnedVisual);
            spawnedVisual = null;
        }

        // Sin arma → ocultar todo
        if (weapon == null)
        {
            SetCubeActive(false);
            return;
        }

        // Hay arma → activar el Cube (como socket o como placeholder)
        SetCubeActive(true);

        if (weapon.WeaponVisualPrefab != null)
        {
            // Prefab real: ocultar el mesh del Cube, instanciar el modelo
            SetCubeMeshVisible(false);

            spawnedVisual = Instantiate(
                weapon.WeaponVisualPrefab,
                placeholderWeapon.transform);

            // Posición/rotación cero → queda exactamente donde está el Cube
            spawnedVisual.transform.SetLocalPositionAndRotation(
                Vector3.zero,
                Quaternion.identity);

            spawnedVisual.transform.localScale = Vector3.one;

            Debug.Log($"[WeaponVisual] Prefab '{weapon.WeaponVisualPrefab.name}' instanciado " +
                       $"como hijo del Cube para '{weapon.ItemName}'");
        }
        else
        {
            // Sin prefab real → mostrar el Cube placeholder
            SetCubeMeshVisible(true);

            Debug.Log($"[WeaponVisual] Cube placeholder activo para '{weapon.ItemName}'");
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void SetCubeActive(bool active)
    {
        if (placeholderWeapon != null)
            placeholderWeapon.SetActive(active);
    }

    private void SetCubeMeshVisible(bool visible)
    {
        if (placeholderRenderer != null)
            placeholderRenderer.enabled = visible;
    }

    private static Transform FindInChildren(Transform parent, string targetName)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == targetName) return child;
            Transform found = FindInChildren(child, targetName);
            if (found != null) return found;
        }
        return null;
    }
}