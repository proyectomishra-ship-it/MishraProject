using UnityEngine;

/// <summary>
/// DIAGNÓSTICO TEMPORAL — no es parte del sistema final del juego.
///
/// USO:
///   1. Arrastrá este script como componente sobre Warrior(Clone)/Mage(Clone)
///      DURANTE Play mode (no hace falta tocar el prefab ni guardar nada).
///   2. Mirá la consola — cada ~1 segundo imprime la diferencia exacta,
///      en unidades de mundo, entre el fondo del Character Controller y
///      el punto más bajo de la malla visual (los pies).
///   3. Una vez que tengas el número, sacá el script (Remove Component)
///      y ajustá el Height del Character Controller con la fórmula de abajo.
///
/// FÓRMULA para el nuevo Height, a partir del log:
///   Si dice "LOS PIES ESTÁN POR DEBAJO" con diferencia = -0.15:
///     nuevo Height = Height actual + 2 × 0.15 = Height actual + 0.30
///   (el Center.y no hace falta tocarlo si ya está en 0 y el estiramiento
///   es simétrico, que es el caso acá)
/// </summary>
public class GroundAlignmentDebugger : MonoBehaviour
{
    private CharacterController controller;
    private Renderer bodyRenderer;

    private void Start()
    {
        controller = GetComponent<CharacterController>();

        if (controller == null)
            Debug.LogError($"[GroundAlign] {name}: no se encontró CharacterController en este GameObject.");

        // FIX: antes usaba GetComponentInChildren<Renderer>() sin especificar cuál,
        // y este personaje tiene VARIOS renderers (el cuerpo en "Capsule", pero
        // también el arma instanciada más abajo en la jerarquía). Si el buscador
        // encontraba primero el renderer del arma, medíamos dónde termina la
        // espada, no dónde terminan los pies — resultado "alineado" pero midiendo
        // lo incorrecto. Ahora se busca específicamente el objeto "Capsule".
        Transform capsuleTransform = FindInChildren(transform, "Capsule");
        if (capsuleTransform == null)
        {
            Debug.LogError($"[GroundAlign] {name}: no se encontró un hijo llamado 'Capsule'.");
            return;
        }

        bodyRenderer = capsuleTransform.GetComponent<Renderer>();
        if (bodyRenderer == null)
            Debug.LogError($"[GroundAlign] {name}: 'Capsule' no tiene un Renderer propio.");
        else
            Debug.Log($"[GroundAlign] {name}: usando Renderer de '{capsuleTransform.name}' para medir los pies.");
    }

    private void Update()
    {
        if (controller == null || bodyRenderer == null) return;

        // Solo cada 60 frames (~1 segundo) para no inundar la consola
        if (Time.frameCount % 60 != 0) return;

        float ccBottom = transform.position.y + controller.center.y - controller.height / 2f;
        float meshBottom = bodyRenderer.bounds.min.y;
        float diferencia = meshBottom - ccBottom;

        string veredicto = diferencia < -0.02f
            ? "→ LOS PIES ESTÁN POR DEBAJO DEL COLLIDER (se hunden)"
            : diferencia > 0.02f
                ? "→ el collider está por debajo de los pies (el personaje flota)"
                : "→ alineado correctamente";

        Debug.Log($"[GroundAlign] {name} — Height={controller.height:F3}, Center=({controller.center.x:F3}, {controller.center.y:F3}, {controller.center.z:F3}) | " +
                  $"CC bottom = {ccBottom:F3} | " +
                  $"Mesh bottom (pies, '{bodyRenderer.name}') = {meshBottom:F3} | " +
                  $"Diferencia = {diferencia:F3}  {veredicto}");
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