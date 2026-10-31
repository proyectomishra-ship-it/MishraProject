using UnityEngine;

/// <summary>
/// Hoja de energía gigante del Warrior: aparece pequeña, se agranda y barre un
/// arco al frente dejando una estela de partículas. Se arma y se anima por código;
/// no usa ningún asset. Solo visual: el daño lo aplica el servidor.
/// </summary>
public class BladeSlashAnimator : MonoBehaviour
{
    private const float ChestHeight = 1f;

    private static Mesh bladeMesh;
    private static Mesh glowMesh;

    private Transform follow;
    private Transform pivot;
    private GameObject bladeObject;
    private GameObject glowObject;
    private Material coreMaterial;
    private Material glowMaterial;
    private Color coreColor;
    private Color glowColor;

    // Nuevo: desplazamiento visual relativo al jugador.
    private Vector3 followOffset;

    private float duration;
    private float startYaw;
    private float endYaw;
    private float timer;
    private bool finished;

    // =========================================================
    // CREACIÓN
    // =========================================================

    /// <param name="caster">Sigue al jugador mientras dura el efecto.</param>
    /// <param name="localOffset">Desplazamiento visual relativo al jugador.</param>
    /// <param name="length">Largo de la hoja (m).</param>
    /// <param name="width">Ancho de la hoja (m).</param>
    /// <param name="sweepAngle">Apertura del barrido (grados).</param>
    public static void Spawn(
        Transform caster,
        Vector3 localOffset,
        Vector3 aimDirection,
        float length,
        float width,
        float sweepAngle,
        float duration,
        Color color)
    {
        GameObject root = new GameObject("VFX_WarriorBlade");
        BladeSlashAnimator animator = root.AddComponent<BladeSlashAnimator>();

        animator.Build(
            caster,
            localOffset,
            aimDirection,
            length,
            width,
            sweepAngle,
            duration,
            color);
    }

    private void Build(
        Transform caster,
        Vector3 localOffset,
        Vector3 aimDirection,
        float length,
        float width,
        float sweepAngle,
        float slashDuration,
        Color color)
    {
        follow = caster;
        followOffset = localOffset;

        duration = Mathf.Max(0.05f, slashDuration);

        transform.position = GetFollowPosition();

        // Un golpe de espada de diestro: arranca a la derecha y termina a la izquierda.
        Vector3 flat = SpecialAbilityEffects.Flatten(aimDirection, caster.forward);
        float aimYaw = Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg;
        startYaw = aimYaw + sweepAngle * 0.5f;
        endYaw = aimYaw - sweepAngle * 0.5f;

        coreColor = Color.Lerp(color, Color.white, 0.55f); // núcleo casi blanco
        glowColor = color;

        Shader shader = Shader.Find("Sprites/Default");

        coreMaterial = new Material(shader) { mainTexture = Texture2D.whiteTexture };
        glowMaterial = new Material(shader) { mainTexture = SpecialAbilityVfx.GetSoftCircle() };

        pivot = new GameObject("Pivot").transform;
        pivot.SetParent(transform, false);
        pivot.rotation = Quaternion.Euler(0f, startYaw, 0f);

        // Resplandor suave por detrás y alrededor de la hoja.
        glowObject = CreateMeshObject(
            "Glow", GetGlowMesh(), glowMaterial,
            new Vector3(0f, 0f, -length * 0.15f),
            new Vector3(width * 2.2f, 1f, length * 1.35f));

        // Hoja nítida: base ancha y punta afilada.
        bladeObject = CreateMeshObject(
            "Blade", GetBladeMesh(), coreMaterial,
            Vector3.zero,
            new Vector3(width, 1f, length));

        CreateTrail(length, width);

        ApplyFrame(0f);
    }

    // Calcula la posición del efecto manteniendo el offset relativo al jugador.
    private Vector3 GetFollowPosition()
    {
        if (follow == null)
            return transform.position;

        return follow.position
            + Vector3.up * ChestHeight
            + follow.right * followOffset.x
            + Vector3.up * followOffset.y
            + follow.forward * followOffset.z;
    }

    private GameObject CreateMeshObject(
        string objectName,
        Mesh mesh,
        Material material,
        Vector3 localPosition,
        Vector3 localScale)
    {
        GameObject go = new GameObject(objectName);
        go.transform.SetParent(pivot, false);
        go.transform.localPosition = localPosition;
        go.transform.localScale = localScale;

        go.AddComponent<MeshFilter>().sharedMesh = mesh;

        MeshRenderer meshRenderer = go.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = material;
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        meshRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        meshRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

        return go;
    }

    /// <summary>
    /// Partículas que nacen a lo largo de toda la hoja. Como se simulan en
    /// espacio mundo, quedan atrás mientras la hoja gira y forman la estela del barrido.
    /// </summary>
    private void CreateTrail(float length, float width)
    {
        ParticleSystem trail = SpecialAbilityVfx.CreateSystem(
            pivot, "Trail", pivot.rotation,
            ParticleSystemRenderMode.Billboard);

        trail.transform.localPosition = new Vector3(0f, 0f, length * 0.5f);

        ParticleSystem.MainModule main = trail.main;
        main.duration = duration;
        main.startLifetime = 0.35f;
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 1.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.55f);
        main.startColor = Color.white;
        main.maxParticles = 600;
        // La caja de emisión debe crecer junto con la hoja.
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;

        ParticleSystem.EmissionModule emission = trail.emission;
        emission.rateOverTime = 220f;

        ParticleSystem.ShapeModule shape = trail.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(width * 0.8f, 0.05f, length);

        SpecialAbilityVfx.FadeOut(trail, glowColor);
        SpecialAbilityVfx.ShrinkOverLifetime(trail, 0.2f);

        trail.Play();
    }

    // =========================================================
    // ANIMACIÓN
    // =========================================================

    private void Update()
    {
        if (finished)
            return;

        timer += Time.deltaTime;

        // Sigue la posición del jugador respetando el offset visual.
        if (follow != null)
            transform.position = GetFollowPosition();

        float t = Mathf.Clamp01(timer / duration);
        ApplyFrame(t);

        if (t >= 1f)
            Finish();
    }

    private void ApplyFrame(float t)
    {
        // Barrido rápido al comienzo y que frena al final.
        float swing = 1f - Mathf.Pow(1f - t, 3f);
        float yaw = Mathf.Lerp(startYaw, endYaw, swing);
        pivot.rotation = Quaternion.Euler(0f, yaw, 0f);

        // La hoja "se agranda": arranca al 30 % de su tamaño y llega al 100 % en el primer tercio.
        float grow = Mathf.SmoothStep(0.3f, 1f, Mathf.Clamp01(t / 0.3f));
        pivot.localScale = Vector3.one * grow;

        // Aparece rápido, se mantiene y se desvanece en el último tramo.
        float alpha = Mathf.Min(Mathf.Clamp01(t / 0.12f), Mathf.Clamp01((1f - t) / 0.4f));

        coreMaterial.color = new Color(coreColor.r, coreColor.g, coreColor.b, 0.9f * alpha);
        glowMaterial.color = new Color(glowColor.r, glowColor.g, glowColor.b, 0.6f * alpha);
    }

    private void Finish()
    {
        finished = true;

        // Se ocultan las mallas, pero se deja vivir un instante al sistema de partículas
        // para que la estela se disipe sola en vez de cortarse de golpe.
        bladeObject.SetActive(false);
        glowObject.SetActive(false);

        Destroy(gameObject, 0.6f);
    }

    private void OnDestroy()
    {
        if (coreMaterial != null)
            Destroy(coreMaterial);

        if (glowMaterial != null)
            Destroy(glowMaterial);
    }

    // =========================================================
    // MALLAS (cacheadas, de tamaño unitario: se escalan por instancia)
    // =========================================================

    /// <summary>Hoja de largo 1 sobre +Z y ancho 1 sobre X, plana, con la punta afilada.</summary>
    private static Mesh GetBladeMesh()
    {
        if (bladeMesh != null)
            return bladeMesh;

        bladeMesh = new Mesh
        {
            name = "BladeSlash_Blade",
            hideFlags = HideFlags.HideAndDontSave
        };

        bladeMesh.vertices = new[]
        {
            new Vector3(-0.5f, 0f, 0f),   // 0 base izquierda
            new Vector3( 0.5f, 0f, 0f),   // 1 base derecha
            new Vector3(-0.5f, 0f, 0.8f), // 2 hombro izquierdo
            new Vector3( 0.5f, 0f, 0.8f), // 3 hombro derecho
            new Vector3( 0f,   0f, 1f)    // 4 punta
        };

        // La base es más transparente: la energía se concentra hacia la punta.
        bladeMesh.colors = new[]
        {
            new Color(1f, 1f, 1f, 0.25f),
            new Color(1f, 1f, 1f, 0.25f),
            Color.white,
            Color.white,
            Color.white
        };

        bladeMesh.uv = new[]
        {
            new Vector2(0f, 0f), new Vector2(1f, 0f),
            new Vector2(0f, 0.8f), new Vector2(1f, 0.8f),
            new Vector2(0.5f, 1f)
        };

        bladeMesh.triangles = new[] { 0, 2, 3, 0, 3, 1, 2, 4, 3 };
        bladeMesh.RecalculateBounds();

        return bladeMesh;
    }

    /// <summary>Rectángulo de 1x1 sobre el plano XZ, para el resplandor.</summary>
    private static Mesh GetGlowMesh()
    {
        if (glowMesh != null)
            return glowMesh;

        glowMesh = new Mesh
        {
            name = "BladeSlash_Glow",
            hideFlags = HideFlags.HideAndDontSave
        };

        glowMesh.vertices = new[]
        {
            new Vector3(-0.5f, 0f, 0f),
            new Vector3( 0.5f, 0f, 0f),
            new Vector3(-0.5f, 0f, 1f),
            new Vector3( 0.5f, 0f, 1f)
        };

        glowMesh.colors = new[] { Color.white, Color.white, Color.white, Color.white };

        glowMesh.uv = new[]
        {
            new Vector2(0f, 0f), new Vector2(1f, 0f),
            new Vector2(0f, 1f), new Vector2(1f, 1f)
        };

        glowMesh.triangles = new[] { 0, 2, 3, 0, 3, 1 };
        glowMesh.RecalculateBounds();

        return glowMesh;
    }
}