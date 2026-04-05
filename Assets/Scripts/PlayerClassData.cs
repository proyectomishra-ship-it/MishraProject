using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "PlayerClassData", menuName = "RPG/Player Class Data")]
public class PlayerClassData : ScriptableObject
{
    [Serializable]
    public struct StatModifier
    {
        public StatType stat;
        public float value;
    }

    [Header("Level Scaling")]
    [SerializeField] private List<StatModifier> levelScaling;

    [Header("Multipliers")]
    [SerializeField] private List<StatModifier> multipliers;

    public List<StatModifier> LevelScaling => levelScaling;
    public List<StatModifier> Multipliers => multipliers;
}