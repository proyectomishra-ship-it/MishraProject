using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Inspector de cualquier CharacterAnimationSet (jugador y enemigos) con validaciones
/// y el botón que genera el Animator Controller completo
/// (parámetros, blend tree, estados y transiciones).
/// </summary>
[CustomEditor(typeof(CharacterAnimationSet), true)]
public class CharacterAnimationSetEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // Se dibujan todos los campos en su orden, salvo el controller generado,
        // que va al final (con herencia, los campos de la base salen primero).
        SerializedProperty controllerProp = null;
        SerializedProperty it = serializedObject.GetIterator();

        for (bool enter = true; it.NextVisible(enter); enter = false)
        {
            if (it.propertyPath == "m_Script")
            {
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.PropertyField(it, true);
            }
            else if (it.propertyPath == "controller")
            {
                controllerProp = it.Copy();
            }
            else
            {
                EditorGUILayout.PropertyField(it, true);
            }
        }

        if (controllerProp != null)
            EditorGUILayout.PropertyField(controllerProp, true);

        serializedObject.ApplyModifiedProperties();

        var set = (CharacterAnimationSet)target;

        EditorGUILayout.Space(8);

        foreach (string msg in CharacterAnimatorBuilder.Validate(set))
            EditorGUILayout.HelpBox(msg, MessageType.Warning);

        if (set is EnemyAnimationSet && set.baseClips != null && set.baseClips.death == null)
            EditorGUILayout.HelpBox(
                "Sin clip 'death': este enemigo desaparece al instante al morir.",
                MessageType.Info);

        using (new EditorGUI.DisabledScope(!CharacterAnimatorBuilder.HasRequiredClips(set)))
        {
            if (GUILayout.Button("Generar / Regenerar Animator Controller", GUILayout.Height(32)))
                CharacterAnimatorBuilder.Build(set);
        }

        EditorGUILayout.HelpBox(
            "Regenerar BORRA y recrea el controller. Si lo editaste a mano, esos cambios se pierden. " +
            "Los prefabs no se rompen: referencian este Set, no el controller.",
            MessageType.None);
    }
}

public static class CharacterAnimatorBuilder
{
    // ── Validación ───────────────────────────────────────────────────────────

    /// <summary>Mínimo para poder generar el controller: idle y walk (run es opcional).</summary>
    public static bool HasRequiredClips(CharacterAnimationSet set)
    {
        var c = set.baseClips;
        return c != null && c.idle != null && c.walk != null;
    }

    public static List<string> Validate(CharacterAnimationSet set)
    {
        var msgs = new List<string>();
        var c = set.baseClips;

        if (c == null || c.idle == null || c.walk == null)
            msgs.Add("Faltan clips obligatorios en el set base: idle y walk.");

        var player = set as PlayerAnimationSet;
        bool usesOverrides = player != null && player.weaponOverrides != null && player.weaponOverrides.Count > 0;

        if (c != null)
        {
            // Un mismo clip en dos huecos rompe los overrides por arma
            // (el override se indexa por clip, no por hueco). Sin overrides no importa.
            if (usesOverrides)
            {
                var seen = new Dictionary<AnimationClip, AnimSlot>();
                foreach (AnimSlot slot in System.Enum.GetValues(typeof(AnimSlot)))
                {
                    AnimationClip clip = c.Get(slot);
                    if (clip == null) continue;

                    if (seen.TryGetValue(clip, out AnimSlot other))
                        msgs.Add($"El clip '{clip.name}' está en {other} y en {slot}. Usá clips distintos " +
                                 "(duplicá el clip si hace falta), o los overrides por arma van a fallar.");
                    else
                        seen[clip] = slot;
                }
            }

            WarnLoop(msgs, c.idle, true, "idle");
            WarnLoop(msgs, c.walk, true, "walk");
            WarnLoop(msgs, c.run, true, "run");
            WarnLoop(msgs, c.fall, true, "fall");
            WarnLoop(msgs, c.death, false, "death");
            WarnLoop(msgs, c.hit, false, "hit");

            // Los enemigos van por NavMesh: estos clips nunca se reproducen.
            if (set is EnemyAnimationSet)
            {
                foreach (AnimSlot slot in new[] { AnimSlot.Jump, AnimSlot.Fall, AnimSlot.Land, AnimSlot.Revive })
                    if (c.Get(slot) != null)
                        msgs.Add($"'{slot}' no se usa en enemigos: el clip se ignora.");
            }
        }

        if (player != null && player.weaponOverrides != null)
        {
            var usedTypes = new HashSet<WeaponType>();
            foreach (var o in player.weaponOverrides)
            {
                if (o?.weaponTypes == null) continue;
                foreach (var t in o.weaponTypes)
                    if (!usedTypes.Add(t))
                        msgs.Add($"El tipo de arma {t} aparece en más de un override. Solo se usa el primero.");
            }

            // Un override solo reemplaza clips que existen en el set base: si el hueco
            // base está vacío no hay estado en el controller y el clip no se reproduce.
            if (c != null)
            {
                foreach (var o in player.weaponOverrides)
                {
                    if (o?.clips == null) continue;

                    foreach (AnimSlot slot in System.Enum.GetValues(typeof(AnimSlot)))
                        if (o.clips.Get(slot) != null && c.Get(slot) == null)
                            msgs.Add($"El override '{o.label}' define {slot}, pero el set base no tiene " +
                                     $"clip en {slot}: se ignora. Asignalo también en el set base.");
                }
            }
        }

        return msgs;
    }

    private static void WarnLoop(List<string> msgs, AnimationClip clip, bool shouldLoop, string slot)
    {
        if (clip == null || clip.isLooping == shouldLoop) return;

        msgs.Add(shouldLoop
            ? $"'{clip.name}' ({slot}) debería tener Loop Time activado en el import, si no se congela."
            : $"'{clip.name}' ({slot}) tiene Loop Time activado: debería estar desactivado.");
    }

    // ── Build ────────────────────────────────────────────────────────────────

    public static void Build(CharacterAnimationSet set)
    {
        if (!HasRequiredClips(set))
        {
            EditorUtility.DisplayDialog("Animator", "Faltan idle / walk.", "OK");
            return;
        }

        CharacterClipSet clips = set.baseClips;

        string setPath = AssetDatabase.GetAssetPath(set);
        string folder = Path.GetDirectoryName(setPath).Replace('\\', '/');
        string path = $"{folder}/{set.name}_Controller.controller";

        if (AssetDatabase.LoadAssetAtPath<Object>(path) != null)
            AssetDatabase.DeleteAsset(path);

        AnimatorController ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);

        // ── Parámetros ───────────────────────────────────────────────────────
        ctrl.AddParameter(CharacterAnimatorParams.Speed, AnimatorControllerParameterType.Float);
        ctrl.AddParameter(CharacterAnimatorParams.Grounded, AnimatorControllerParameterType.Bool);
        ctrl.AddParameter(CharacterAnimatorParams.VerticalVelocity, AnimatorControllerParameterType.Float);
        ctrl.AddParameter(CharacterAnimatorParams.IsDead, AnimatorControllerParameterType.Bool);
        ctrl.AddParameter(new AnimatorControllerParameter
        {
            name = CharacterAnimatorParams.AttackSpeed,
            type = AnimatorControllerParameterType.Float,
            defaultFloat = 1f
        });
        ctrl.AddParameter(CharacterAnimatorParams.AttackLight, AnimatorControllerParameterType.Trigger);
        ctrl.AddParameter(CharacterAnimatorParams.AttackHeavy, AnimatorControllerParameterType.Trigger);
        ctrl.AddParameter(CharacterAnimatorParams.AttackSpecial, AnimatorControllerParameterType.Trigger);
        ctrl.AddParameter(CharacterAnimatorParams.Hit, AnimatorControllerParameterType.Trigger);

        AnimatorStateMachine sm = ctrl.layers[0].stateMachine;

        // ── Locomotion (blend tree 0=idle, 1=walk, 2=run) ────────────────────
        var tree = new BlendTree
        {
            name = CharacterAnimatorParams.StateLocomotion,
            blendType = BlendTreeType.Simple1D,
            blendParameter = CharacterAnimatorParams.Speed,
            useAutomaticThresholds = false
        };
        tree.AddChild(clips.idle, 0f);
        tree.AddChild(clips.walk, 1f);
        if (clips.run != null)
            tree.AddChild(clips.run, 2f); // sin 'run' el blend se queda en walk a toda velocidad
        AssetDatabase.AddObjectToAsset(tree, ctrl);

        AnimatorState loco = sm.AddState(CharacterAnimatorParams.StateLocomotion, new Vector3(300, 0, 0));
        loco.motion = tree;
        loco.tag = CharacterAnimatorParams.TagLocomotion;
        sm.defaultState = loco;

        // ── Aire ─────────────────────────────────────────────────────────────
        AnimatorState jump = State(sm, CharacterAnimatorParams.StateJump, clips.jump, new Vector3(600, -120, 0));
        AnimatorState fall = State(sm, CharacterAnimatorParams.StateFall, clips.fall, new Vector3(600, 0, 0));
        AnimatorState land = State(sm, CharacterAnimatorParams.StateLand, clips.land, new Vector3(600, 120, 0));

        // Sin 'fall' el clip de salto cubre todo el aire.
        AnimatorState airFromGround = jump ?? fall;

        if (airFromGround != null)
        {
            if (jump != null && fall != null)
            {
                Link(loco, jump, 0.08f)
                    .Cond(CharacterAnimatorParams.Grounded, false)
                    .Cond(CharacterAnimatorParams.VerticalVelocity, AnimatorConditionMode.Greater, 0.5f);

                Link(loco, fall, 0.12f)
                    .Cond(CharacterAnimatorParams.Grounded, false)
                    .Cond(CharacterAnimatorParams.VerticalVelocity, AnimatorConditionMode.Less, -1.5f);

                Link(jump, fall, 0.15f)
                    .Cond(CharacterAnimatorParams.Grounded, false)
                    .Cond(CharacterAnimatorParams.VerticalVelocity, AnimatorConditionMode.Less, 0f);
            }
            else
            {
                Link(loco, airFromGround, 0.1f).Cond(CharacterAnimatorParams.Grounded, false);
            }

            AnimatorState afterAir = land ?? loco;
            if (jump != null) Link(jump, afterAir, 0.1f).Cond(CharacterAnimatorParams.Grounded, true);
            if (fall != null) Link(fall, afterAir, 0.1f).Cond(CharacterAnimatorParams.Grounded, true);

            if (land != null)
                Link(land, loco, 0.1f).ExitTime(0.8f);
        }

        // ── Muerte (Any State, máxima prioridad: se agrega primero) ──────────
        AnimatorState death = State(sm, CharacterAnimatorParams.StateDeath, clips.death, new Vector3(300, 300, 0));
        AnimatorState revive = State(sm, CharacterAnimatorParams.StateRevive, clips.revive, new Vector3(600, 300, 0));

        if (death != null)
        {
            AnyLink(sm, death, 0.1f, false).Cond(CharacterAnimatorParams.IsDead, true);

            if (revive != null)
            {
                Link(death, revive, 0.05f).Cond(CharacterAnimatorParams.IsDead, false);
                Link(revive, loco, 0.15f).ExitTime(0.9f);
            }
            else
            {
                Link(death, loco, 0.2f).Cond(CharacterAnimatorParams.IsDead, false);
            }
        }

        // ── Ataques (Any State → ataque → vuelve a Locomotion) ───────────────
        BuildAttack(sm, loco, CharacterAnimatorParams.StateAttackSpecial, clips.attackSpecial, CharacterAnimatorParams.AttackSpecial, new Vector3(0, -180, 0));
        BuildAttack(sm, loco, CharacterAnimatorParams.StateAttackHeavy, clips.attackHeavy, CharacterAnimatorParams.AttackHeavy, new Vector3(0, -60, 0));
        BuildAttack(sm, loco, CharacterAnimatorParams.StateAttackLight, clips.attackLight, CharacterAnimatorParams.AttackLight, new Vector3(0, 60, 0));

        // ── Golpe recibido (solo desde Locomotion: los ataques no se interrumpen) ──
        AnimatorState hit = State(sm, CharacterAnimatorParams.StateHit, clips.hit, new Vector3(0, 180, 0));
        if (hit != null)
        {
            Link(loco, hit, 0.05f).Trigger(CharacterAnimatorParams.Hit);
            Link(hit, loco, 0.1f).ExitTime(0.9f);
        }

        // ── Guardar y enlazar ────────────────────────────────────────────────
        EditorUtility.SetDirty(ctrl);
        set.controller = ctrl;
        EditorUtility.SetDirty(set);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[CharacterAnimatorBuilder] Controller generado: {path}", ctrl);
        EditorGUIUtility.PingObject(ctrl);
    }

    // ── Helpers de construcción ──────────────────────────────────────────────

    private static AnimatorState State(AnimatorStateMachine sm, string name, AnimationClip clip, Vector3 pos)
    {
        if (clip == null) return null;

        AnimatorState s = sm.AddState(name, pos);
        s.motion = clip;
        return s;
    }

    private static void BuildAttack(
        AnimatorStateMachine sm, AnimatorState loco,
        string stateName, AnimationClip clip, string trigger, Vector3 pos)
    {
        AnimatorState s = State(sm, stateName, clip, pos);
        if (s == null) return;

        s.tag = CharacterAnimatorParams.TagAction;
        s.speedParameterActive = true;
        s.speedParameter = CharacterAnimatorParams.AttackSpeed;

        // canTransitionToSelf = true → un segundo click reinicia el ataque.
        AnyLink(sm, s, 0.05f, true)
            .Trigger(trigger)
            .Cond(CharacterAnimatorParams.IsDead, false);

        Link(s, loco, 0.12f).ExitTime(0.85f);
    }

    // Wrapper fluido para escribir las transiciones en una línea.
    private readonly struct TransitionBuilder
    {
        private readonly AnimatorStateTransition t;
        public TransitionBuilder(AnimatorStateTransition t) { this.t = t; }

        public TransitionBuilder Cond(string param, bool value)
        {
            t.AddCondition(value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, param);
            return this;
        }

        public TransitionBuilder Cond(string param, AnimatorConditionMode mode, float threshold)
        {
            t.AddCondition(mode, threshold, param);
            return this;
        }

        public TransitionBuilder Trigger(string param)
        {
            t.AddCondition(AnimatorConditionMode.If, 0f, param);
            return this;
        }

        public TransitionBuilder ExitTime(float normalized)
        {
            t.hasExitTime = true;
            t.exitTime = normalized;
            return this;
        }
    }

    private static TransitionBuilder Link(AnimatorState from, AnimatorState to, float duration)
    {
        AnimatorStateTransition t = from.AddTransition(to);
        t.hasExitTime = false;
        t.hasFixedDuration = true;
        t.duration = duration;
        return new TransitionBuilder(t);
    }

    private static TransitionBuilder AnyLink(AnimatorStateMachine sm, AnimatorState to, float duration, bool self)
    {
        AnimatorStateTransition t = sm.AddAnyStateTransition(to);
        t.hasExitTime = false;
        t.hasFixedDuration = true;
        t.duration = duration;
        t.canTransitionToSelf = self;
        return new TransitionBuilder(t);
    }
}
