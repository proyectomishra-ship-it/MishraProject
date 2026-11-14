using System.Collections;
using UnityEngine;

/// <summary>
/// Efecto de subida de nivel: onda de choque en el suelo, aura de partículas
/// que sube alrededor del jugador, columna de luz y destello de luz.
/// Se arma por código (sin assets). Solo visual; lo dispara un ClientRpc de Player.
/// </summary>
public static class LevelUpVfx
{
    public static void Play(Player player, Color color, float shockwaveRadius, float auraDuration)
    {
        if (player == null)
            return;

        Vector3 feet = player.transform.position;

        // 1) Onda de choque: disco + anillo + chispas.
        SpecialAbilityVfx.PlayShockwave(feet, shockwaveRadius, color);

        // 2) Aura: partículas que suben pegadas al jugador.
        GameObject root = new GameObject("VFX_LevelUpAura");
        root.transform.SetParent(player.transform, false);
        root.transform.localPosition = Vector3.zero;

        ParticleSystem aura = SpecialAbilityVfx.CreateSystem(
            root.transform, "Aura", Quaternion.Euler(90f, 0f, 0f), ParticleSystemRenderMode.Billboard);

        ParticleSystem.MainModule main = aura.main;
        main.duration = auraDuration;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.2f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.3f);
        main.startColor = Color.white;
        main.maxParticles = 300;

        ParticleSystem.EmissionModule emission = aura.emission;
        emission.rateOverTime = 70f;

        ParticleSystem.ShapeModule shape = aura.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.7f;
        shape.radiusThickness = 0.15f;   // cerca del borde: forma de cilindro

        ParticleSystem.VelocityOverLifetimeModule rise = aura.velocityOverLifetime;
        rise.enabled = true;
        rise.space = ParticleSystemSimulationSpace.World;
        rise.x = new ParticleSystem.MinMaxCurve(0f);
        rise.y = new ParticleSystem.MinMaxCurve(2f);
        rise.z = new ParticleSystem.MinMaxCurve(0f);

        SpecialAbilityVfx.FadeOut(aura, color);
        SpecialAbilityVfx.ShrinkOverLifetime(aura, 0.2f);

        aura.Play();

        // 3) Columna de luz: una sola partícula alta y suave.
        ParticleSystem pillar = SpecialAbilityVfx.CreateSystem(
            root.transform, "Pillar", Quaternion.identity, ParticleSystemRenderMode.VerticalBillboard);

        ParticleSystem.MainModule pm = pillar.main;
        pm.duration = 0.8f;
        pm.simulationSpace = ParticleSystemSimulationSpace.Local;
        pm.startLifetime = 0.8f;
        pm.startSpeed = 0f;
        pm.startSize3D = true;
        pm.startSizeX = 1.8f;
        pm.startSizeY = 7f;
        pm.startSizeZ = 1f;
        pm.startColor = new Color(color.r, color.g, color.b, 0.6f);
        pm.maxParticles = 2;

        ParticleSystem.ShapeModule ps = pillar.shape;
        ps.enabled = true;
        ps.shapeType = ParticleSystemShapeType.Circle;
        ps.radius = 0.001f;
        pillar.transform.localPosition = new Vector3(0f, 2.5f, 0f);

        SpecialAbilityVfx.SetBurst(pillar, 1);
        SpecialAbilityVfx.FadeOut(pillar, color);

        pillar.Play();

        // 4) Destello de luz que se apaga.
        GameObject glowGo = new GameObject("Glow");
        glowGo.transform.SetParent(root.transform, false);
        glowGo.transform.localPosition = new Vector3(0f, 1f, 0f);

        Light glow = glowGo.AddComponent<Light>();
        glow.type = LightType.Point;
        glow.color = color;
        glow.range = shockwaveRadius;
        glow.intensity = 6f;
        glow.shadows = LightShadows.None;

        player.StartCoroutine(FadeLight(glow, 0.9f));

        Object.Destroy(root, auraDuration + 1.5f);
    }

    private static IEnumerator FadeLight(Light light, float duration)
    {
        float start = light != null ? light.intensity : 0f;
        float t = 0f;

        while (light != null && t < duration)
        {
            t += Time.deltaTime;
            light.intensity = Mathf.Lerp(start, 0f, t / duration);
            yield return null;
        }

        if (light != null)
            light.enabled = false;
    }
}
