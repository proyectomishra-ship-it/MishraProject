using UnityEngine;

/// <summary>
/// Animation Set de un tipo de enemigo (Goblin, Orc, Slime...).
///
/// Mínimo: idle + walk. Todo lo demás es opcional:
///   • run          → si falta, usa walk a toda velocidad.
///   • attackHeavy / attackSpecial → si faltan, usa el ataque más parecido
///                     (Special → Heavy → Light).
///   • hit          → si falta, no reacciona al recibir golpes.
///   • death        → si falta, el enemigo desaparece al instante (como antes).
///                     Si existe, el cuerpo se queda el tiempo del clip y recién
///                     ahí se despawnea.
///
/// Los enemigos se mueven por NavMesh, así que los clips de aire (jump/fall/land)
/// y revive no se usan.
/// </summary>
[CreateAssetMenu(menuName = "Mishra/Animation/Enemy Animation Set", fileName = "EnemyAnimationSet")]
public class EnemyAnimationSet : CharacterAnimationSet
{
}
