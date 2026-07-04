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
/// </summary>
public class WeaponVisualController : MonoBehaviour
{
    [Header("Referencias — asignar en el Inspector")]
    [Tooltip("El objeto 'Cube' que ya existe en el prefab, hijo del Capsule.")]
    [SerializeField] private GameObject placeholderWeapon;

    [Tooltip("Multiplicador de tamaño del arma visible, además de compensar la " +
             "escala del Cube. El mesh original suele estar modelado chico " +
             "(convención típica de packs de props de bajo poly) — subí este " +
             "número hasta que se vea bien. Podés poner un valor distinto en " +
             "cada prefab (Warrior/Mage/Hunter) si cada arma necesita su propio tamaño.")]
    [SerializeField] private float visualScaleMultiplier = 4f;

    private EquipmentController equipmentController;
    private MeshRenderer placeholderRenderer;
    private GameObject spawnedVisual;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        // DIAGNÓSTICO: log incondicional, primera línea del método.
        // Si esto no aparece en consola, este componente NO está corriendo
        // en el objeto que estás probando — revisar en el Inspector del
        // GameObject 'Mage(Clone)'/'Warrior(Clone)' en tiempo de Play si el
        // componente dice "Missing (Mono Script)" en vez de mostrar sus campos.
        Debug.Log($"[WeaponVisual] >>> Awake() en '{gameObject.name}'");

        if (placeholderWeapon == null)
        {
            Transform found = FindInChildren(transform, "Cube");
            if (found != null)
            {
                placeholderWeapon = found.gameObject;
                Debug.Log($"[WeaponVisual] >>> Cube encontrado por búsqueda automática: '{placeholderWeapon.name}'.");
            }
            else
            {
                Debug.LogWarning($"[WeaponVisual] >>> {name}: no se encontró 'Cube'. " +
                                  "Asigná el campo 'Placeholder Weapon' en el Inspector.");
            }
        }
        else
        {
            Debug.Log($"[WeaponVisual] >>> placeholderWeapon ya asignado en Inspector: '{placeholderWeapon.name}'.");
        }

        if (placeholderWeapon != null)
            placeholderRenderer = placeholderWeapon.GetComponent<MeshRenderer>();

        equipmentController = GetComponent<EquipmentController>();
        if (equipmentController == null)
        {
            Debug.LogError($"[WeaponVisual] >>> ABORTA en '{name}': falta EquipmentController en este GameObject.");
            return;
        }

        equipmentController.OnSlotChanged += HandleSlotChanged;
        Debug.Log($"[WeaponVisual] >>> Suscripto a OnSlotChanged en '{gameObject.name}'. Awake() completo.");
    }

    private IEnumerator Start()
    {
        Debug.Log($"[WeaponVisual] >>> Start() INICIO en '{gameObject.name}'. Esperando 2 frames...");

        yield return null;
        yield return null;

        if (equipmentController == null)
        {
            Debug.LogError($"[WeaponVisual] >>> Start() ABORTA en '{gameObject.name}': " +
                           "equipmentController es null (Awake() no lo encontró).");
            yield break;
        }

        Debug.Log($"[WeaponVisual] >>> Start() en '{gameObject.name}': IsOwner = {equipmentController.IsOwner}");

        if (!equipmentController.IsOwner)
        {
            Debug.Log($"[WeaponVisual] >>> Start() ABORTA en '{gameObject.name}': " +
                      "no es el jugador local (IsOwner = false).");
            yield break;
        }

        var weapon = equipmentController.GetEquippedWeapon();

        Debug.Log($"[WeaponVisual] >>> {name} — refresh inicial: " +
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
        Debug.Log($"[WeaponVisual] >>> HandleSlotChanged() en '{gameObject.name}': " +
                  $"slot={slot}, item='{(item as ItemData)?.ItemName ?? "null"}'");

        if (slot != EquipmentSlot.Weapon)
        {
            Debug.Log($"[WeaponVisual] >>> Ignorado: slot '{slot}' no es Weapon.");
            return;
        }

        if (!equipmentController.IsOwner)
        {
            Debug.Log($"[WeaponVisual] >>> ABORTA en '{gameObject.name}': IsOwner = false.");
            return;
        }

        Debug.Log($"[WeaponVisual] {name} — OnSlotChanged: " +
                  $"arma = '{(item as WeaponData)?.ItemName ?? "ninguna"}'");

        RefreshWeaponVisual(item as WeaponData);
    }

    // ── Lógica principal ──────────────────────────────────────────────────────

    private void RefreshWeaponVisual(WeaponData weapon)
    {
        Debug.Log($"[WeaponVisual] >>> RefreshWeaponVisual('{weapon?.ItemName ?? "null"}') en '{gameObject.name}'. " +
                  $"placeholderWeapon = {(placeholderWeapon != null ? placeholderWeapon.name : "NULL")}");

        if (spawnedVisual != null)
        {
            Destroy(spawnedVisual);
            spawnedVisual = null;
        }

        if (weapon == null)
        {
            SetCubeActive(false);
            return;
        }

        if (placeholderWeapon == null)
        {
            Debug.LogError($"[WeaponVisual] >>> ABORTA en '{gameObject.name}': placeholderWeapon es NULL. " +
                           "No hay Cube asignado donde instanciar el arma.");
            return;
        }

        SetCubeActive(true);

        if (weapon.WeaponVisualPrefab != null)
        {
            SetCubeMeshVisible(false);

            spawnedVisual = Instantiate(
                weapon.WeaponVisualPrefab,
                placeholderWeapon.transform);

            spawnedVisual.transform.SetLocalPositionAndRotation(
                Vector3.zero,
                Quaternion.identity);

            // FIX: localScale = Vector3.one significaba escala MUNDIAL = escala
            // del Cube padre (en Warrior/Mage: ~0.33-0.55, un valor que quedó de
            // cuando el Cube se usaba con otro propósito, no pensado para esto).
            // Resultado: el arma nacía a un tercio de su tamaño real.
            //
            // Se compensa la escala del padre para que el arma nazca SIEMPRE a
            // su tamaño natural (escala mundial 1,1,1), sin importar qué escala
            // tenga el Cube — funciona igual aunque el Cube cambie de escala
            // más adelante (por ejemplo al ajustar la animación).
            Vector3 parentLossyScale = placeholderWeapon.transform.lossyScale;
            spawnedVisual.transform.localScale = new Vector3(
                (parentLossyScale.x != 0f ? 1f / parentLossyScale.x : 1f) * visualScaleMultiplier,
                (parentLossyScale.y != 0f ? 1f / parentLossyScale.y : 1f) * visualScaleMultiplier,
                (parentLossyScale.z != 0f ? 1f / parentLossyScale.z : 1f) * visualScaleMultiplier
            );

            Debug.Log($"[WeaponVisual] >>> Prefab '{weapon.WeaponVisualPrefab.name}' instanciado " +
                      $"como hijo de '{placeholderWeapon.name}' para '{weapon.ItemName}'. " +
                      $"Escala del Cube padre: {parentLossyScale}, multiplicador: {visualScaleMultiplier} " +
                      $"→ localScale final: {spawnedVisual.transform.localScale}. " +
                      $"Posición mundial resultante: {spawnedVisual.transform.position}, " +
                      $"activeInHierarchy = {spawnedVisual.activeInHierarchy}");
        }
        else
        {
            SetCubeMeshVisible(true);
            Debug.Log($"[WeaponVisual] >>> Cube placeholder activo para '{weapon.ItemName}' " +
                      "(sin WeaponVisualPrefab asignado).");
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void SetCubeActive(bool active)
    {
        if (placeholderWeapon == null)
        {
            Debug.LogError($"[WeaponVisual] >>> SetCubeActive({active}) ABORTA: placeholderWeapon es NULL.");
            return;
        }

        placeholderWeapon.SetActive(active);

        // DIAGNÓSTICO: activeSelf puede ser true mientras activeInHierarchy es false
        // si algún padre en la cadena (Capsule, root, etc.) está desactivado.
        // SetActive(true) no falla ni avisa en ese caso — el objeto queda
        // técnicamente "activo" pero invisible igual.
        Debug.Log($"[WeaponVisual] >>> SetCubeActive({active}) en '{placeholderWeapon.name}'. " +
                  $"activeSelf = {placeholderWeapon.activeSelf}, " +
                  $"activeInHierarchy = {placeholderWeapon.activeInHierarchy}");
    }

    private void SetCubeMeshVisible(bool visible)
    {
        if (placeholderRenderer != null)
            placeholderRenderer.enabled = visible;
        else
            Debug.LogWarning($"[WeaponVisual] >>> placeholderRenderer es NULL en '{name}' " +
                             "(el Cube no tiene MeshRenderer directo — normal si solo se usa como socket).");
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