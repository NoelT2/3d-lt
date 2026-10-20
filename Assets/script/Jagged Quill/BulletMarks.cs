using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Small crater left by each bullet that hits the environment, in the surface's own colour:
///  - a dug-up patch + darker pit (flat quads on the surface)
///  - a few tiny chunks thrown out that fall and rest nearby
///  - a small dust puff
/// Three shared particle systems (one draw call each), created automatically on the first hit.
/// Called from BulletDamage; all sizes/lifetimes are set on each bullet prefab.
/// </summary>
public static class BulletMarks
{
    private const int MaxMarks = 1500;
    private const int MaxChunks = 600;
    private const int MaxDust = 300;

    private static ParticleSystem craters;
    private static ParticleSystem chunks;
    private static ParticleSystem dust;

    private static Mesh quadMesh;
    private static Mesh cubeMesh;
    private static Texture2D craterTexture;
    private static Texture2D softTexture;
    private static Material craterMaterial;
    private static Material dustMaterial;
    private static Material chunkMaterial;

    public static void Spawn(Vector3 point, Vector3 normal, Color surface, float size, float markLifetime,
                             int chunkCount, float chunkSize, float chunkLifetime, float dustSize)
    {
        if (craters == null) BuildCraters(markLifetime);
        if (chunks == null && chunkCount > 0) BuildChunks(chunkLifetime);
        if (dust == null && dustSize > 0f) BuildDust();

        // ---- Crater: wide dug-up patch + smaller, darker pit (same colour family as the surface) ----
        if (craters != null)
        {
            EmitMark(point, normal, size, Shade(surface, 0.6f, 0.8f), markLifetime);
            EmitMark(point, normal, size * 0.55f, Shade(surface, 0.35f, 0.95f), markLifetime);
        }

        // ---- Chunks thrown out of the crater ----
        if (chunks != null)
        {
            for (int i = 0; i < chunkCount; i++)
            {
                Vector3 dir = RandomTangent(normal) * 0.7f + normal * Random.Range(0.5f, 1f);
                float v = Random.Range(0.5f, 0.9f);

                ParticleSystem.EmitParams p = new ParticleSystem.EmitParams
                {
                    position = point + normal * 0.05f,
                    velocity = dir.normalized * Random.Range(2f, 6f),
                    startColor = new Color(surface.r * v, surface.g * v, surface.b * v, 1f),
                    startSize = chunkSize * Random.Range(0.5f, 1.2f),
                    startLifetime = chunkLifetime * Random.Range(0.9f, 1.1f),
                    angularVelocity3D = Random.insideUnitSphere * 360f
                };
                chunks.Emit(p, 1);
            }
        }

        // ---- Dust puff ----
        if (dust != null && dustSize > 0f)
        {
            Color c = Color.Lerp(surface, Color.white, 0.35f);
            c.a = 0.4f;

            ParticleSystem.EmitParams p = new ParticleSystem.EmitParams
            {
                position = point + normal * 0.1f,
                velocity = normal * Random.Range(0.4f, 1.2f) + RandomTangent(normal) * Random.Range(0.2f, 0.8f),
                startColor = c,
                startSize = dustSize * Random.Range(0.7f, 1.2f),
                startLifetime = Random.Range(0.7f, 1.1f)
            };
            dust.Emit(p, 1);
        }
    }

    private static void EmitMark(Vector3 point, Vector3 normal, float size, Color color, float lifetime)
    {
        Vector3 up = RandomTangent(normal);

        ParticleSystem.EmitParams p = new ParticleSystem.EmitParams
        {
            position = point + normal * Random.Range(0.02f, 0.045f),   // tiny offset so it sits on the surface
            rotation3D = Quaternion.LookRotation(-normal, up).eulerAngles,
            startSize3D = new Vector3(size, size * Random.Range(0.75f, 1f), 1f),   // a bit irregular
            startColor = color,
            startLifetime = lifetime,
            velocity = Vector3.zero
        };
        craters.Emit(p, 1);
    }

    private static Color Shade(Color c, float k, float alpha)
    {
        return new Color(c.r * k, c.g * k, c.b * k, alpha);
    }

    private static Vector3 RandomTangent(Vector3 normal)
    {
        Vector3 r = Vector3.ProjectOnPlane(Random.onUnitSphere, normal);
        if (r.sqrMagnitude < 0.001f)
            r = Vector3.Cross(normal, Mathf.Abs(normal.y) > 0.9f ? Vector3.right : Vector3.up);
        return r.normalized;
    }

    // ================= Particle system construction =================

    private static ParticleSystem CreateSystem(string name, int max, float gravity, float lifetime, out ParticleSystemRenderer renderer)
    {
        GameObject go = new GameObject(name);
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = ps.main;
        main.loop = true;                       // kept alive; we emit manually
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = max;
        main.gravityModifier = gravity;
        main.startLifetime = lifetime;
        main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.enabled = false;

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.enabled = false;

        renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return ps;
    }

    private static Gradient FadeOutGradient(float fadeStart)
    {
        Gradient g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, fadeStart), new GradientAlphaKey(0f, 1f) });
        return g;
    }

    private static void BuildCraters(float lifetime)
    {
        ParticleSystemRenderer renderer;
        craters = CreateSystem("_BulletCraters", MaxMarks, 0f, lifetime, out renderer);

        ParticleSystem.MainModule main = craters.main;
        main.startRotation3D = true;
        main.startSize3D = true;

        ParticleSystem.ColorOverLifetimeModule col = craters.colorOverLifetime;
        col.enabled = true;
        col.color = new ParticleSystem.MinMaxGradient(FadeOutGradient(0.7f));

        renderer.renderMode = ParticleSystemRenderMode.Mesh;
        renderer.mesh = GetQuad();
        renderer.alignment = ParticleSystemRenderSpace.World;
        renderer.sortMode = ParticleSystemSortMode.YoungestInFront;
        renderer.sharedMaterial = GetSpriteMaterial(GetCraterTexture(), ref craterMaterial);

        craters.Play();
    }

    private static void BuildChunks(float lifetime)
    {
        ParticleSystemRenderer renderer;
        chunks = CreateSystem("_BulletChunks", MaxChunks, 2.5f, lifetime, out renderer);

        ParticleSystem.MainModule main = chunks.main;
        main.startRotation3D = true;

        // Full size, then shrink away at the end of life.
        ParticleSystem.SizeOverLifetimeModule size = chunks.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f,
            new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.8f, 1f), new Keyframe(1f, 0f)));

        // Chunks land and rest on the ground instead of falling through it.
        ParticleSystem.CollisionModule collision = chunks.collision;
        collision.enabled = true;
        collision.type = ParticleSystemCollisionType.World;
        collision.mode = ParticleSystemCollisionMode.Collision3D;
        collision.bounce = 0.2f;
        collision.dampen = 0.5f;
        collision.lifetimeLoss = 0f;
        collision.quality = ParticleSystemCollisionQuality.Low;
        collision.radiusScale = 0.5f;

        renderer.renderMode = ParticleSystemRenderMode.Mesh;
        renderer.mesh = GetCube();
        renderer.alignment = ParticleSystemRenderSpace.World;
        renderer.sharedMaterial = GetChunkMaterial();

        chunks.Play();
    }

    private static void BuildDust()
    {
        ParticleSystemRenderer renderer;
        dust = CreateSystem("_BulletDust", MaxDust, -0.05f, 1f, out renderer);

        ParticleSystem.SizeOverLifetimeModule size = dust.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f,
            new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(1f, 1.25f)));

        ParticleSystem.ColorOverLifetimeModule col = dust.colorOverLifetime;
        col.enabled = true;
        col.color = new ParticleSystem.MinMaxGradient(FadeOutGradient(0f));

        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sharedMaterial = GetSpriteMaterial(GetSoftTexture(), ref dustMaterial);

        dust.Play();
    }

    // ================= Shared assets (built once) =================

    private static Mesh GetQuad()
    {
        if (quadMesh != null) return quadMesh;

        quadMesh = new Mesh { name = "BulletMarkQuad" };
        quadMesh.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
            new Vector3(-0.5f,  0.5f, 0f), new Vector3(0.5f,  0.5f, 0f)
        };
        quadMesh.normals = new[] { -Vector3.forward, -Vector3.forward, -Vector3.forward, -Vector3.forward };
        quadMesh.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
        quadMesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
        return quadMesh;
    }

    private static Mesh GetCube()
    {
        if (cubeMesh != null) return cubeMesh;

        GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cubeMesh = temp.GetComponent<MeshFilter>().sharedMesh;
        temp.GetComponent<Collider>().enabled = false;
        Object.Destroy(temp);
        return cubeMesh;
    }

    // Irregular blob with a noisy edge (white; the particle colour tints it).
    private static Texture2D GetCraterTexture()
    {
        if (craterTexture != null) return craterTexture;

        const int n = 64;
        craterTexture = new Texture2D(n, n, TextureFormat.RGBA32, false);
        craterTexture.wrapMode = TextureWrapMode.Clamp;
        craterTexture.filterMode = FilterMode.Bilinear;

        float seed = Random.value * 100f;
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                Vector2 p = new Vector2((x + 0.5f) / n - 0.5f, (y + 0.5f) / n - 0.5f) * 2f;
                float d = p.magnitude;

                float noise = Mathf.PerlinNoise(x * 0.1f + seed, y * 0.1f + seed);
                float fine = Mathf.PerlinNoise(x * 0.3f + seed * 2f, y * 0.3f + seed * 2f);
                float edge = 0.72f + (noise - 0.5f) * 0.5f + (fine - 0.5f) * 0.12f;

                float a = Mathf.Clamp01((edge - d) / 0.22f);
                a = a * a * (3f - 2f * a);

                craterTexture.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }

        craterTexture.Apply();
        return craterTexture;
    }

    // Soft round puff.
    private static Texture2D GetSoftTexture()
    {
        if (softTexture != null) return softTexture;

        const int n = 64;
        softTexture = new Texture2D(n, n, TextureFormat.RGBA32, false);
        softTexture.wrapMode = TextureWrapMode.Clamp;
        softTexture.filterMode = FilterMode.Bilinear;

        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                Vector2 p = new Vector2((x + 0.5f) / n - 0.5f, (y + 0.5f) / n - 0.5f) * 2f;
                float a = Mathf.Clamp01(1f - p.magnitude);
                softTexture.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
            }
        }

        softTexture.Apply();
        return softTexture;
    }

    // Sprites/Default: transparent, double-sided, uses particle colour, always included in builds.
    private static Material GetSpriteMaterial(Texture2D texture, ref Material cache)
    {
        if (cache != null) return cache;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) return null;

        cache = new Material(shader);
        cache.mainTexture = texture;
        return cache;
    }

    private static Material GetChunkMaterial()
    {
        if (chunkMaterial != null) return chunkMaterial;

        string[] shaderNames =
        {
            "Universal Render Pipeline/Particles/Simple Lit",   // lit, so chunks have shading
            "Universal Render Pipeline/Particles/Unlit",
            "Particles/Standard Unlit",
            "Sprites/Default"
        };

        foreach (string shaderName in shaderNames)
        {
            Shader shader = Shader.Find(shaderName);
            if (shader != null)
            {
                chunkMaterial = new Material(shader);
                return chunkMaterial;
            }
        }
        return null;
    }
}