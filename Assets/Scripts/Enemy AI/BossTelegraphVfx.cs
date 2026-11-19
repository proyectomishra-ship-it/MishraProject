using UnityEngine;

/// <summary>
/// Avisos en el suelo para los ataques del boss (sin arte nuevo: usa los mismos helpers de partículas
/// que las habilidades). Se dibuja en cada cliente; lo dispara un ClientRpc del DemiGodAIController.
///
///  - Círculo: un disco tenue marca el área y otro más claro crece por dentro: cuando llega al borde, golpea.
///  - Carril: una franja que se va intensificando hasta el golpe.
/// </summary>
public static class BossTelegraphVfx
{
    private static readonly Color Danger = new Color(1f, 0.22f, 0.08f, 1f);

    public static void PlayCircle(Vector3 center, float radius, float duration)
    {
        duration = Mathf.Max(0.1f, duration);

        GameObject root = new GameObject("VFX_BossTelegraphCircle");
        root.transform.position = center + Vector3.up * 0.12f;

        MakeDisc(root.transform, "Area", radius * 2f, duration, 0.20f, false);
        MakeDisc(root.transform, "Fill", radius * 2f, duration, 0.40f, true);

        Object.Destroy(root, duration + 0.5f);
    }

    public static void PlayLane(Vector3 origin, Vector3 direction, float length, float width, float duration)
    {
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.001f || length <= 0.1f)
            return;

        direction.Normalize();
        duration = Mathf.Max(0.1f, duration);
        width = Mathf.Max(0.5f, width);

        GameObject root = new GameObject("VFX_BossTelegraphLane");
        root.transform.position = origin + direction * (length * 0.5f) + Vector3.up * 0.12f;
        root.transform.rotation = Quaternion.LookRotation(direction);

        ParticleSystem lane = SpecialAbilityVfx.CreateSystem(
            root.transform, "Lane", root.transform.rotation, ParticleSystemRenderMode.HorizontalBillboard);

        ParticleSystem.MainModule main = lane.main;
        main.duration = 0.1f;
        main.startLifetime = duration;
        main.startSpeed = 0f;
        main.startSize = Mathf.Max(1f, width * 0.7f);
        main.startColor = new Color(Danger.r, Danger.g, Danger.b, 0.35f);
        main.maxParticles = 450;

        // Franja rectangular: las partículas nacen repartidas dentro de una caja del tamaño del carril.
        ParticleSystem.ShapeModule shape = lane.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.position = Vector3.zero;
        shape.scale = new Vector3(width, 0.05f, length);

        SpecialAbilityVfx.SetBurst(lane, Mathf.Clamp(Mathf.CeilToInt(length * width * 0.9f), 12, 400));

        // Se va intensificando hasta el golpe.
        ParticleSystem.ColorOverLifetimeModule color = lane.colorOverLifetime;
        color.enabled = true;

        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0.35f, 0f), new GradientAlphaKey(1f, 1f) });
        color.color = gradient;

        lane.Play();

        Object.Destroy(root, duration + 0.5f);
    }

    private static void MakeDisc(Transform parent, string name, float diameter, float duration, float alpha, bool grow)
    {
        ParticleSystem disc = SpecialAbilityVfx.CreateSystem(
            parent, name, Quaternion.identity, ParticleSystemRenderMode.HorizontalBillboard);

        ParticleSystem.MainModule main = disc.main;
        main.duration = 0.1f;
        main.startLifetime = duration;
        main.startSpeed = 0f;
        main.startSize = diameter;
        main.startColor = new Color(Danger.r, Danger.g, Danger.b, alpha);
        main.maxParticles = 2;

        SpecialAbilityVfx.SetBurst(disc, 1);
        SpecialAbilityVfx.DisableShape(disc);

        if (grow)
        {
            ParticleSystem.SizeOverLifetimeModule size = disc.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.05f, 1f, 1f));
        }

        disc.Play();
    }
}
