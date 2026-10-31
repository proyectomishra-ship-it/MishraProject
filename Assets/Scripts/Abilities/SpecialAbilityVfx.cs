using UnityEngine;

/// <summary>
/// Efectos visuales de las habilidades especiales. Corren en TODOS los clientes
/// (los dispara un ClientRpc de PlayerCombatController); no afectan al gameplay.
///
/// Los efectos se construyen por código con ParticleSystem, así funcionan sin
/// crear assets. Cada habilidad permite además asignar un prefab propio.
/// </summary>
public static class SpecialAbilityVfx
{
    private const float CustomPrefabLifetime = 5f;

    private static Texture2D softCircle;
    private static Material particleMaterial;

    // =========================================================
    // PREFAB PROPIO
    // =========================================================

    public static void PlayPrefab(GameObject prefab, Vector3 position, Vector3 direction)
    {
        Vector3 flat = SpecialAbilityEffects.Flatten(direction, Vector3.forward);

        GameObject instance = Object.Instantiate(
            prefab,
            position,
            Quaternion.LookRotation(flat));

        Object.Destroy(instance, CustomPrefabLifetime);
    }

    // =========================================================
    // MAGE — ONDA EXPANSIVA
    // =========================================================

    public static void PlayShockwave(Vector3 position, float radius, Color arcane)
    {
        GameObject root = new GameObject("VFX_MageShockwave");
        root.transform.position = position + Vector3.up * 0.15f;

        // 1) Disco de energía que se expande por el suelo hasta el radio completo.
        ParticleSystem disc = CreateSystem(
            root.transform, "Disc", Quaternion.identity,
            ParticleSystemRenderMode.HorizontalBillboard);

        ParticleSystem.MainModule discMain = disc.main;
        discMain.duration = 0.5f;
        discMain.startLifetime = 0.45f;
        discMain.startSpeed = 0f;
        discMain.startSize = radius * 2f;
        discMain.startColor = new Color(arcane.r, arcane.g, arcane.b, 0.55f);
        discMain.maxParticles = 4;

        SetBurst(disc, 1);
        DisableShape(disc);
        FadeOut(disc, arcane);

        ParticleSystem.SizeOverLifetimeModule discSize = disc.sizeOverLifetime;
        discSize.enabled = true;
        discSize.size = new ParticleSystem.MinMaxCurve(
            1f, AnimationCurve.EaseInOut(0f, 0.05f, 1f, 1f));

        // 2) Anillo de partículas que sale disparado hacia afuera.
        ParticleSystem ring = CreateSystem(
            root.transform, "Ring", Quaternion.Euler(90f, 0f, 0f),
            ParticleSystemRenderMode.Billboard);

        float ringLife = 0.55f;

        ParticleSystem.MainModule ringMain = ring.main;
        ringMain.duration = 0.5f;
        ringMain.startLifetime = ringLife;
        // Velocidad tal que al final de su vida la partícula llega al borde del radio.
        ringMain.startSpeed = radius / ringLife;
        ringMain.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.65f);
        ringMain.startColor = Color.white;
        ringMain.maxParticles = 400;

        SetBurst(ring, 200);

        ParticleSystem.ShapeModule ringShape = ring.shape;
        ringShape.enabled = true;
        ringShape.shapeType = ParticleSystemShapeType.Circle;
        ringShape.radius = 0.3f;
        ringShape.radiusThickness = 0f; // emite solo desde el borde → anillo limpio

        FadeOut(ring, arcane);
        ShrinkOverLifetime(ring, 0.25f);

        // 3) Chispas que suben desde todo el disco.
        ParticleSystem sparks = CreateSystem(
            root.transform, "Sparks", Quaternion.Euler(90f, 0f, 0f),
            ParticleSystemRenderMode.Billboard);

        ParticleSystem.MainModule sparksMain = sparks.main;
        sparksMain.duration = 0.5f;
        sparksMain.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.0f);
        sparksMain.startSpeed = 0f;
        sparksMain.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.25f);
        sparksMain.startColor = Color.white;
        sparksMain.maxParticles = 200;

        SetBurst(sparks, 100);

        ParticleSystem.ShapeModule sparksShape = sparks.shape;
        sparksShape.enabled = true;
        sparksShape.shapeType = ParticleSystemShapeType.Circle;
        sparksShape.radius = radius * 0.7f;
        sparksShape.radiusThickness = 1f;   // rellena el disco entero

        ParticleSystem.VelocityOverLifetimeModule rise = sparks.velocityOverLifetime;
        rise.enabled = true;
        rise.space = ParticleSystemSimulationSpace.World;
        // Unity exige que los tres ejes usen el mismo modo de curva
        // (acá "dos constantes"), si no tira error al reproducir.
        rise.x = new ParticleSystem.MinMaxCurve(0f, 0f);
        rise.y = new ParticleSystem.MinMaxCurve(1.5f, 3.5f);
        rise.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        FadeOut(sparks, Color.Lerp(arcane, Color.white, 0.6f));
        ShrinkOverLifetime(sparks, 0.1f);

        disc.Play();
        ring.Play();
        sparks.Play();

        Object.Destroy(root, 3f);
    }

    // =========================================================
    // WARRIOR — AURA DEL ARMA
    // =========================================================

    /// <summary>
    /// Envuelve el arma equipada en partículas y luz del color dado.
    /// Si el personaje no tiene visual de arma, no hace nada.
    /// </summary>
    public static void PlayWeaponAura(Player caster, Color color, float duration)
    {
        if (caster == null)
            return;

        WeaponVisualController visual = caster.GetComponent<WeaponVisualController>();
        Transform socket = visual != null ? visual.WeaponSocket : null;

        if (socket == null)
            return;

        // Tamaño y centro reales del arma, tomados de sus renderers visibles.
        Bounds bounds = default;
        bool hasBounds = false;

        foreach (Renderer r in socket.GetComponentsInChildren<Renderer>())
        {
            if (r is ParticleSystemRenderer || !r.enabled || !r.gameObject.activeInHierarchy)
                continue;

            if (!hasBounds)
            {
                bounds = r.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(r.bounds);
            }
        }

        Vector3 center = hasBounds ? bounds.center : socket.position;
        float size = hasBounds ? Mathf.Clamp(bounds.extents.magnitude, 0.3f, 2.5f) : 0.5f;

        GameObject root = new GameObject("VFX_WeaponAura");
        root.transform.SetParent(socket, true); // pegado al arma: la sigue en cada animación
        root.transform.position = center;

        // Luz de color: tiñe el entorno cercano del arma.
        Light glow = root.AddComponent<Light>();
        glow.type = LightType.Point;
        glow.color = color;
        glow.range = size * 4f;
        glow.intensity = 4f;
        glow.shadows = LightShadows.None;
        Object.Destroy(glow, duration);

        // Partículas que flotan alrededor del arma (en espacio mundo: dejan estela al moverse).
        ParticleSystem aura = CreateSystem(
            root.transform, "Aura", Quaternion.identity,
            ParticleSystemRenderMode.Billboard);

        ParticleSystem.MainModule main = aura.main;
        main.duration = duration;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.8f);
        main.startSize = new ParticleSystem.MinMaxCurve(size * 0.12f, size * 0.3f);
        main.startColor = Color.white;
        main.gravityModifier = -0.1f;   // flotan hacia arriba
        main.maxParticles = 300;

        ParticleSystem.EmissionModule emission = aura.emission;
        emission.rateOverTime = 80f;

        ParticleSystem.ShapeModule shape = aura.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = size * 0.5f;
        shape.radiusThickness = 1f;

        FadeOut(aura, color);
        ShrinkOverLifetime(aura, 0.2f);

        aura.Play();

        Object.Destroy(root, duration + 1f);
    }

    // =========================================================
    // HELPERS DE PARTÍCULAS (también los usa BladeSlashAnimator)
    // =========================================================

    internal static ParticleSystem CreateSystem(
        Transform parent,
        string name,
        Quaternion rotation,
        ParticleSystemRenderMode renderMode)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.rotation = rotation;

        ParticleSystem ps = go.AddComponent<ParticleSystem>();

        // AddComponent lo deja reproduciéndose; hay que frenarlo antes de
        // tocar duration y el resto de la configuración.
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = ps.main;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0f;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 0f;

        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = renderMode;
        renderer.sharedMaterial = GetParticleMaterial();

        return ps;
    }

    internal static void SetBurst(ParticleSystem ps, int count)
    {
        ParticleSystem.EmissionModule emission = ps.emission;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
    }

    internal static void DisableShape(ParticleSystem ps)
    {
        ParticleSystem.ShapeModule shape = ps.shape;
        shape.enabled = false;
    }

    /// <summary>Color que pasa de blanco al tono dado y se desvanece al final de la vida.</summary>
    internal static void FadeOut(ParticleSystem ps, Color tint)
    {
        ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
        col.enabled = true;

        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(tint, 0.4f),
                new GradientColorKey(tint, 1f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0.8f, 0.5f),
                new GradientAlphaKey(0f, 1f)
            });

        col.color = gradient;
    }

    internal static void ShrinkOverLifetime(ParticleSystem ps, float endScale)
    {
        ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(
            1f, AnimationCurve.Linear(0f, 1f, 1f, endScale));
    }

    // =========================================================
    // MATERIAL Y TEXTURA (cacheados)
    // =========================================================

    /// <summary>
    /// Usa "Sprites/Default" porque está en Always Included Shaders del proyecto,
    /// así que también existe en el build (a diferencia de los shaders de partículas de URP,
    /// que Unity quitaría si ningún material los referencia).
    /// </summary>
    internal static Material GetParticleMaterial()
    {
        if (particleMaterial != null)
            return particleMaterial;

        particleMaterial = new Material(Shader.Find("Sprites/Default"))
        {
            name = "SpecialAbilityVfx_Runtime",
            mainTexture = GetSoftCircle(),
            hideFlags = HideFlags.HideAndDontSave
        };

        return particleMaterial;
    }

    /// <summary>Círculo blanco con borde difuso, para que las partículas sean redondas y suaves.</summary>
    internal static Texture2D GetSoftCircle()
    {
        if (softCircle != null)
            return softCircle;

        const int size = 64;
        float center = (size - 1) * 0.5f;

        softCircle = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "SpecialAbilityVfx_SoftCircle",
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance =
                    Vector2.Distance(new Vector2(x, y), new Vector2(center, center)) / center;

                float alpha = Mathf.Clamp01(1f - distance);
                alpha *= alpha;

                softCircle.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        softCircle.Apply();

        return softCircle;
    }
}
