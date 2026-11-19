using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Movimientos del DemiGod. Todos reutilizan animaciones, proyectiles y partículas que ya existen.</summary>
public enum DemiGodMoveType
{
    // --- Postura a distancia ---
    Fireball,   // una bola de fuego (el hechizo de siempre)
    Orb,        // proyectil rápido (Special Prefab) con carril de aviso en el suelo
    Volley,     // abanico de bolas de fuego
    Meteors,    // zonas de impacto sobre tu posición, una tras otra
    Summon,     // invoca aliados (con tope de aliados vivos)

    // --- Postura cuerpo a cuerpo ---
    Strike,     // un golpe frontal con aviso (carril)
    Slam,       // golpe de área con aviso (círculo) que empuja al jugador

    // --- Transición ---
    Charge      // embestida con carril de aviso: lo acerca al jugador
}

/// <summary>Ajustes de UN movimiento. Se editan en el Inspector del DemiGodAIController > Combat > Moves.</summary>
[Serializable]
public class DemiGodMoveSettings
{
    public DemiGodMoveType type;

    public bool enabled = true;

    [Tooltip("Fase a partir de la cual el boss puede usarlo (1, 2 o 3).")]
    [Range(1, 3)] public int minPhase = 1;

    [Tooltip("Probabilidad relativa de ser elegido dentro de su postura. 0 = nunca sale al azar " +
             "(la embestida solo se usa como transición).")]
    [Min(0f)] public float weight = 1f;

    [Tooltip("Segundos hasta poder repetirlo (se acorta en las fases avanzadas).")]
    [Min(0f)] public float cooldown = 6f;

    [Tooltip("Distancia mínima al jugador para usarlo. 0 = sin mínimo.")]
    [Min(0f)] public float minDistance = 0f;

    [Tooltip("Distancia máxima al jugador para usarlo. 0 = sin máximo.")]
    [Min(0f)] public float maxDistance = 0f;

    [Tooltip("Segundos de aviso en el suelo antes de que haga daño. Es la ventana para esquivar.")]
    [Min(0f)] public float windup = 0.8f;

    [Tooltip("Multiplica el stat Attack del boss. Referencia: el hechizo de antes hacía 1x.")]
    [Min(0f)] public float damageMultiplier = 1f;

    [Tooltip("Radio de las zonas circulares (Slam, Meteors) o ANCHO de los carriles (Strike, Orb, Charge). " +
             "En Volley: grados de separación entre proyectiles.")]
    [Min(0f)] public float size = 3f;

    [Tooltip("LARGO de los carriles (Strike, Orb) o distancia máxima de la embestida (Charge).")]
    [Min(0f)] public float length = 6f;

    [Tooltip("Cantidad: proyectiles del abanico (Volley) o zonas seguidas (Meteors).")]
    [Min(1)] public int count = 1;

    [Tooltip("Animación que reproduce (las que ya tiene el boss).")]
    public AttackAnimKind animation = AttackAnimKind.Light;

    /// <summary>Momento (Time.time) a partir del cual vuelve a estar disponible. Solo en ejecución.</summary>
    [NonSerialized] public float nextReadyTime;

    public DemiGodMoveSettings() { }

    public DemiGodMoveSettings(DemiGodMoveType type) { this.type = type; }

    /// <summary>Valores iniciales: se usan mientras el prefab no tenga la lista guardada.</summary>
    public static List<DemiGodMoveSettings> CreateDefaults()
    {
        return new List<DemiGodMoveSettings>
        {
            // ---------------- A DISTANCIA ----------------
            new DemiGodMoveSettings(DemiGodMoveType.Fireball)
            { weight = 3f, cooldown = 1f, windup = 0.55f, damageMultiplier = 1f, animation = AttackAnimKind.Light },

            new DemiGodMoveSettings(DemiGodMoveType.Orb)
            { weight = 1.5f, cooldown = 7f, windup = 0.9f, damageMultiplier = 1.6f, size = 1.6f, length = 40f,
              minDistance = 6f, animation = AttackAnimKind.Special },

            new DemiGodMoveSettings(DemiGodMoveType.Volley)
            { minPhase = 2, weight = 1.5f, cooldown = 9f, windup = 0.7f, damageMultiplier = 0.8f, size = 14f,
              count = 3, animation = AttackAnimKind.Special },

            new DemiGodMoveSettings(DemiGodMoveType.Meteors)
            { minPhase = 2, weight = 1.5f, cooldown = 11f, windup = 1.1f, damageMultiplier = 1.5f, size = 3f,
              count = 3, minDistance = 4f, animation = AttackAnimKind.Special },

            new DemiGodMoveSettings(DemiGodMoveType.Summon)
            { minPhase = 2, weight = 1f, cooldown = 18f, windup = 1f, animation = AttackAnimKind.Special },

            // ---------------- CUERPO A CUERPO ----------------
            new DemiGodMoveSettings(DemiGodMoveType.Strike)
            { weight = 3f, cooldown = 2.5f, windup = 0.7f, damageMultiplier = 2.5f, size = 4.5f, length = 5.5f,
              maxDistance = 6.5f, animation = AttackAnimKind.Heavy },

            new DemiGodMoveSettings(DemiGodMoveType.Slam)
            { weight = 1.5f, cooldown = 8f, windup = 0.9f, damageMultiplier = 2f, size = 6f,
              maxDistance = 8f, animation = AttackAnimKind.Special },

            // ---------------- TRANSICIÓN ----------------
            new DemiGodMoveSettings(DemiGodMoveType.Charge)
            { weight = 0f, cooldown = 9f, windup = 0.9f, damageMultiplier = 2f, size = 3f, length = 22f,
              minDistance = 9f, animation = AttackAnimKind.Light },
        };
    }
}

/// <summary>Todo lo ajustable del combate del DemiGod. Visible en el Inspector como "Combat".</summary>
[Serializable]
public class DemiGodCombatSettings
{
    [Header("Posturas: alternancia distancia <-> cuerpo a cuerpo")]
    [Tooltip("Segundos que dura cada postura a distancia (mín, máx).")]
    public Vector2 rangedStanceSeconds = new Vector2(6f, 9f);

    [Tooltip("Segundos que dura cada postura cuerpo a cuerpo (mín, máx).")]
    public Vector2 meleeStanceSeconds = new Vector2(4f, 6f);

    [Tooltip("Pausa entre un movimiento y el siguiente (mín, máx).")]
    public Vector2 breathSeconds = new Vector2(0.8f, 1.5f);

    [Tooltip("Multiplica duraciones, pausas y recargas en la fase 1, 2 y 3. Menor = más rápido.")]
    public float[] phaseTempo = { 1f, 0.85f, 0.7f };

    [Header("Distancias (metros)")]
    [Tooltip("A distancia: si el jugador está más cerca que esto, el boss retrocede.")]
    public float rangedMinDistance = 6f;

    [Tooltip("A distancia: si el jugador está más lejos que esto, el boss se acerca.")]
    public float rangedMaxDistance = 15f;

    [Tooltip("Cuerpo a cuerpo: distancia a la que se planta frente al jugador.")]
    public float meleeDistance = 3.5f;

    [Tooltip("A distancia: si el jugador se pega a menos de esto, lo castiga con un golpe.")]
    public float panicDistance = 4.5f;

    [Tooltip("Cuerpo a cuerpo: si el jugador se aleja más de esto, el boss embiste o dispara.")]
    public float farDistance = 12f;

    [Tooltip("Distancia a la que el boss deja de perseguir y empieza a pelear (hechizos, embestidas, golpes). " + "Debe ser mayor que 'Ranged Max Distance' y menor que 'Leash Distance'.")]
    public float engageDistance = 22f;

    [Tooltip("Si el jugador se aleja más que esto, el boss vuelve a perseguirlo.")]
    public float leashDistance = 45f;

    [Header("Embestida")]
    public float chargeSpeed = 20f;

    [Tooltip("Segundos que queda quieto después de embestir: ventana para atacarlo.")]
    public float chargeRecovery = 1.3f;

    [Header("Empuje (Slam y embestida)")]
    public float knockbackDistance = 7f;
    public float knockbackDuration = 0.35f;

    [Header("Otros")]
    [Tooltip("Segundos antes del daño en que arranca la animación, para que el golpe y el daño coincidan.")]
    public float animationLead = 0.3f;

    [Tooltip("Máximo de aliados invocados vivos a la vez.")]
    [Min(0)] public int maxAliveAdds = 6;

    [Tooltip("En cooperativo: probabilidad de cambiar de objetivo al cambiar de postura.")]
    [Range(0f, 1f)] public float retargetChance = 0.4f;

    [Header("Movimientos")]
    public List<DemiGodMoveSettings> moves = DemiGodMoveSettings.CreateDefaults();

    public float Tempo(int phase)
    {
        if (phaseTempo == null || phaseTempo.Length == 0)
            return 1f;

        return Mathf.Max(0.1f, phaseTempo[Mathf.Clamp(phase - 1, 0, phaseTempo.Length - 1)]);
    }
}
