#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// DEBUG, SOLO EDITOR. Mantener/pulsar Alt lanza el ataque especial de la clase ignorando
/// nivel de desbloqueo, recarga y mana (y el bloqueo por golpe en curso).
///
/// No hay que agregarlo a ningun prefab: PlayerCombatController lo suma solo al jugador local
/// en el editor. Todo el archivo esta dentro de #if UNITY_EDITOR, asi que no existe en un build.
///
/// Si Alt te da problemas (en Windows soltarlo puede enfocar la barra de menu del editor),
/// cambia HotKey por otra tecla, por ejemplo Key.F8.
/// </summary>
[DisallowMultipleComponent]
public class SpecialDebugHotkey : MonoBehaviour
{
    private static readonly Key HotKey = Key.LeftAlt;

    /// <summary>Con la tecla mantenida se repite el especial cada tantos segundos.</summary>
    private const float RepeatSeconds = 0.25f;

    private PlayerCombatController combat;
    private PlayerInputController input;
    private float nextRepeat;

    private void Awake()
    {
        combat = GetComponent<PlayerCombatController>();
        input = GetComponent<PlayerInputController>();
    }

    private void Update()
    {
        if (combat == null || !combat.IsSpawned || !combat.IsOwner)
            return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        // Con un menu / inventario abierto el jugador tiene el input bloqueado: no se dispara.
        if (input != null && input.IsInputBlocked)
            return;

        var key = keyboard[HotKey];

        if (key.wasPressedThisFrame)
        {
            nextRepeat = Time.time + RepeatSeconds;
            combat.DebugRequestUnlimitedSpecial();
        }
        else if (key.isPressed && Time.time >= nextRepeat)
        {
            nextRepeat = Time.time + RepeatSeconds;
            combat.DebugRequestUnlimitedSpecial();
        }
    }
}
#endif
