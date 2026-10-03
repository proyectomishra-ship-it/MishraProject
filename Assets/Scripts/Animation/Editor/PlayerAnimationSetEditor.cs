using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Inspector de PlayerAnimationSet con validaciones y el botón que genera
/// el Animator Controller completo (parámetros, blend tree, estados y transiciones).
/// </summary>
[CustomEditor(typeof(PlayerAnimationSet))]
public class PlayerAnimationSetEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var set = (PlayerAnimationSet)target;

        EditorGUILayout.Space(8);

        foreach (string msg in PlayerAnimatorBuilder.Validate(set))
            EditorGUILayout.HelpBox(msg, MessageType.Warning);

        using (new EditorGUI.DisabledScope(!PlayerAnimatorBuilder.HasRequiredClips(set)))
        {
            if (GUILayout.Button("Generar / Regenerar Animator Controller", GUILayout.Height(32)))
                PlayerAnimatorBuilder.Build(set);
        }

        EditorGUILayout.HelpBox(
            "Regenerar BORRA y recrea el controller. Si lo editaste a mano, esos cambios se pierden. " +
            "Los prefabs no se rompen: referencian este Set, no el controller.",
            MessageType.None);
    }
}

public static class PlayerAnimatorBuilder
{
    // ── Validación ───────────────────────────────────────────────────────────

    public static bool HasRequiredClips(PlayerAnimationSet set)
    {
        var c = set.baseClips;
        return c != null && c.idle != null && c.walk != null && c.run != null;
    }

    public static List<string> Validate(PlayerAnimationSet set)
    {
        var msgs = new List<string>();
        var c = set.baseClips;

        if (c == null || c.idle == null || c.walk == null || c.run == null)
            msgs.Add("Faltan clips obligatorios en el set base: idle, walk y run.");

        if (c != null)
        {
            // Un mismo clip en dos huecos rompe los overrides por arma
            // (el override se indexa por clip, no por hueco).
            var seen = new Dictionary<AnimationClip, PlayerAnimSlot>();
            foreach (PlayerAnimSlot slot in System.Enum.GetValues(typeof(PlayerAnimSlot)))
            {
                AnimationClip clip = c.Get(slot);
                if (clip == null) continue;

                if (seen.TryGetValue(clip, out PlayerAnimSlot other))
                    msgs.Add($"El clip '{clip.name}' está en {other} y en {slot}. Usá clips distintos " +
                             "(duplicá el clip si hace falta), o los overrides por arma van a fallar.");
                else
                    seen[clip] = slot;
            }

            WarnLoop(msgs, c.idle, true, "idle");
            WarnLoop(msgs, c.walk, true, "walk");
            WarnLoop(msgs, c.run, true, "run");
            WarnLoop(msgs, c.fall, true, "fall");
            WarnLoop(msgs, c.death, false, "death");
            WarnLoop(msgs, c.hit, false, "hit");
        }

        if (set.weaponOverrides != null)
        {
            var usedTypes = new HashSet<WeaponType>();
            foreach (var o in set.weaponOverrides)
            {
                if (o?.weaponTypes == null) continue;
                foreach (var t in o.weaponTypes)
                    if (!usedTypes.Add(t))
                        msgs.Add($"El tipo de arma {t} aparece en más de un override. Solo se usa el primero.");
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

    public static void Build(PlayerAnimationSet set)
    {
        if (!HasRequiredClips(set))
        {
            EditorUtility.DisplayDialog("Animator", "Faltan idle / walk / run.", "OK");
            return;
        }

        PlayerClipSet clips = set.baseClips;

        string setPath = AssetDatabase.GetAssetPath(set);
        string folder = Path.GetDirectoryName(setPath).Replace('\\', '/');
        string path = $"{folder}/{set.name}_Controller.controller";

        if (AssetDatabase.LoadAssetAtPath<Object>(path) != null)
            AssetDatabase.DeleteAsset(path);

        AnimatorController ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);

        // ── Parámetros ───────────────────────────────────────────────────────
        ctrl.AddParameter(PlayerAnimatorParams.Speed, AnimatorControllerParameterType.Float);
        ctrl.AddParameter(PlayerAnimatorParams.Grounded, AnimatorControllerParameterType.Bool);
        ctrl.AddParameter(PlayerAnimatorParams.VerticalVelocity, AnimatorControllerParameterType.Float);
        ctrl.AddParameter(PlayerAnimatorParams.IsDead, AnimatorControllerParameterType.Bool);
        ctrl.AddParameter(new AnimatorControllerParameter
        {
            name = PlayerAnimatorParams.AttackSpeed,
            type = AnimatorControllerParameterType.Float,
            defaultFloat = 1f
        });
        ctrl.AddParameter(PlayerAnimatorParams.AttackLight, AnimatorControllerParameterType.Trigger);
        ctrl.AddParameter(PlayerAnimatorParams.AttackHeavy, AnimatorControllerParameterType.Trigger);
        ctrl.AddParameter(PlayerAnimatorParams.AttackSpecial, AnimatorControllerParameterType.Trigger);
        ctrl.AddParameter(PlayerAnimatorParams.Hit, AnimatorControllerParameterType.Trigger);

        AnimatorStateMachine sm = ctrl.layers[0].stateMachine;

        // ── Locomotion (blend tree 0=idle, 1=walk, 2=run) ────────────────────
        var tree = new BlendTree
        {
            name = "Locomotion",
            blendType = BlendTreeType.Simple1D,
            blendParameter = PlayerAnimatorParams.Speed,
            useAutomaticThresholds = false
        };
        tree.AddChild(clips.idle, 0f);
        tree.AddChild(clips.walk, 1f);
        tree.AddChild(clips.run, 2f);
        AssetDatabase.AddObjectToAsset(tree, ctrl);

        AnimatorState loco = sm.AddState("Locomotion", new Vector3(300, 0, 0));
        loco.motion = tree;
        loco.tag = PlayerAnimatorParams.TagLocomotion;
        sm.defaultState = loco;

        // ── Aire ─────────────────────────────────────────────────────────────
        AnimatorState jump = State(sm, "Jump", clips.jump, new Vector3(600, -120, 0));
        AnimatorState fall = State(sm, "Fall", clips.fall, new Vector3(600, 0, 0));
        AnimatorState land = State(sm, "Land", clips.land, new Vector3(600, 120, 0));

        // Sin 'fall' el clip de salto cubre todo el aire.
        AnimatorState airFromGround = jump ?? fall;

        if (airFromGround != null)
        {
            if (jump != null && fall != null)
            {
                Link(loco, jump, 0.08f)
                    .Cond(PlayerAnimatorParams.Grounded, false)
                    .Cond(PlayerAnimatorParams.VerticalVelocity, AnimatorConditionMode.Greater, 0.5f);

                Link(loco, fall, 0.12f)
                    .Cond(PlayerAnimatorParams.Grounded, false)
                    .Cond(PlayerAnimatorParams.VerticalVelocity, AnimatorConditionMode.Less, 0.5f);

                Link(jump, fall, 0.15f)
                    .Cond(PlayerAnimatorParams.Grounded, false)
                    .Cond(PlayerAnimatorParams.VerticalVelocity, AnimatorConditionMode.Less, 0f);
            }
            else
            {
                Link(loco, airFromGround, 0.1f).Cond(PlayerAnimatorParams.Grounded, false);
            }

            AnimatorState afterAir = land ?? loco;
            if (jump != null) Link(jump, afterAir, 0.1f).Cond(PlayerAnimatorParams.Grounded, true);
            if (fall != null) Link(fall, afterAir, 0.1f).Cond(PlayerAnimatorParams.Grounded, true);

            if (land != null)
                Link(land, loco, 0.1f).ExitTime(0.8f);
        }

        // ── Muerte (Any State, máxima prioridad: se agrega primero) ──────────
        AnimatorState death = State(sm, PlayerAnimatorParams.StateDeath, clips.death, new Vector3(300, 300, 0));
        AnimatorState revive = State(sm, "Revive", clips.revive, new Vector3(600, 300, 0));

        if (death != null)
        {
            AnyLink(sm, death, 0.1f, false).Cond(PlayerAnimatorParams.IsDead, true);

            if (revive != null)
            {
                Link(death, revive, 0.05f).Cond(PlayerAnimatorParams.IsDead, false);
                Link(revive, loco, 0.15f).ExitTime(0.9f);
            }
            else
            {
                Link(death, loco, 0.2f).Cond(PlayerAnimatorParams.IsDead, false);
            }
        }

        // ── Ataques (Any State → ataque → vuelve a Locomotion) ───────────────
        BuildAttack(sm, loco, "AttackSpecial", clips.attackSpecial, PlayerAnimatorParams.AttackSpecial, new Vector3(0, -180, 0));
        BuildAttack(sm, loco, "AttackHeavy", clips.attackHeavy, PlayerAnimatorParams.AttackHeavy, new Vector3(0, -60, 0));
        BuildAttack(sm, loco, "AttackLight", clips.attackLight, PlayerAnimatorParams.AttackLight, new Vector3(0, 60, 0));

        // ── Golpe recibido (solo desde Locomotion: los ataques no se interrumpen) ──
        AnimatorState hit = State(sm, "Hit", clips.hit, new Vector3(0, 180, 0));
        if (hit != null)
        {
            Link(loco, hit, 0.05f).Trigger(PlayerAnimatorParams.Hit);
            Link(hit, loco, 0.1f).ExitTime(0.9f);
        }

        // ── Guardar y enlazar ────────────────────────────────────────────────
        EditorUtility.SetDirty(ctrl);
        set.controller = ctrl;
        EditorUtility.SetDirty(set);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[PlayerAnimatorBuilder] Controller generado: {path}", ctrl);
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

        s.tag = PlayerAnimatorParams.TagAction;
        s.speedParameterActive = true;
        s.speedParameter = PlayerAnimatorParams.AttackSpeed;

        // canTransitionToSelf = true → un segundo click reinicia el ataque.
        AnyLink(sm, s, 0.05f, true)
            .Trigger(trigger)
            .Cond(PlayerAnimatorParams.IsDead, false);

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
