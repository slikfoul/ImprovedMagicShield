using System;
using System.Collections.Generic;
using UnityEngine;

namespace ImprovedMagicShield.Visuals
{
    // Only components in a catalogued shield effect instance are captured.
    // Never change root transforms, collider meshes, physics, sound, lifetimes or gameplay scripts.
    public sealed class VisualState : IDisposable
    {
        public readonly EffectStage Stage;
        private readonly List<ParticleState> particles = new List<ParticleState>();
        private readonly List<LightState> lights = new List<LightState>();
        private readonly List<RendererState> renderers = new List<RendererState>();
        private readonly List<MeshState> meshes = new List<MeshState>();
        private bool disposed;
        public bool Empty { get { return particles.Count + lights.Count + renderers.Count == 0; } }

        public VisualState(GameObject root, EffectStage stage)
        {
            Stage = stage;
            try
            {
                foreach (var component in root.GetComponentsInChildren<ParticleSystem>(true)) particles.Add(new ParticleState(component));
                foreach (var component in root.GetComponentsInChildren<Light>(true)) lights.Add(new LightState(component));
                foreach (var component in root.GetComponentsInChildren<Renderer>(true))
                {
                    renderers.Add(new RendererState(component));
                    if (component is MeshRenderer)
                    {
                        var filter = component.GetComponent<MeshFilter>();
                        if (filter && filter.sharedMesh && filter.sharedMesh.isReadable) meshes.Add(new MeshState(filter, stage == EffectStage.Shield && string.Equals(filter.name, "Sphere", StringComparison.OrdinalIgnoreCase)));
                    }
                }
            }
            catch { Dispose(); throw; }
        }

        public void Apply(VisualOptions options)
        {
            if (disposed) return;
            foreach (var state in lights) state.Apply(options.Light);
            foreach (var state in particles) state.Apply(options);
            foreach (var state in renderers) state.Apply(options);
            foreach (var state in meshes) state.Apply(options);
        }

        // EffectsControl may retain an inactive shield state and still sample LightFlicker.
        // Return only our light writes before its LateUpdate, avoiding a scaled baseline
        // being mistaken for native intensity when switching between the two mods.
        public void ReleaseLightWrites()
        {
            foreach (var state in lights) state.Restore();
        }

        public void Dispose()
        {
            if (disposed) return;
            // Keep cleanup independent: a disappearing component must not leak other clones.
            foreach (var state in particles) { try { state.Restore(); } catch { } }
            foreach (var state in lights) { try { state.Restore(); } catch { } }
            foreach (var state in renderers) { try { state.Dispose(); } catch { } }
            foreach (var state in meshes) { try { state.Dispose(); } catch { } }
            disposed = true;
        }
    }

    internal sealed class LightState
    {
        private readonly Light light;
        private float native, lastWritten;
        private bool wrote;
        public LightState(Light light) { this.light = light; native = light.intensity; }
        public void Apply(float factor)
        {
            if (!light) return;
            // Native LightFlicker writes its fading intensity in Update. Preserve that animated
            // value each time it writes, while avoiding scaling our own output again.
            if (!wrote || light.intensity != lastWritten) native = light.intensity;
            lastWritten = native * factor;
            light.intensity = lastWritten; wrote = true;
        }
        public void Restore() { if (light && wrote && light.intensity == lastWritten) light.intensity = native; wrote = false; }
    }

    internal static class CurveScale
    {
        public static ParticleSystem.MinMaxCurve Multiply(ParticleSystem.MinMaxCurve original, float factor)
        {
            switch (original.mode)
            {
                case ParticleSystemCurveMode.Constant: original.constant *= factor; break;
                case ParticleSystemCurveMode.TwoConstants: original.constantMin *= factor; original.constantMax *= factor; break;
                default: original.curveMultiplier *= factor; break;
            }
            return original;
        }
    }

    internal sealed class ParticleState
    {
        private readonly ParticleSystem system;
        private readonly ParticleSystem.MinMaxCurve timeRate, distanceRate;
        private readonly ParticleSystem.Burst[] bursts;
        private readonly Dictionary<uint, List<NativeParticle>> live = new Dictionary<uint, List<NativeParticle>>();
        private readonly List<uint> emptySeeds = new List<uint>();
        private ParticleSystem.Particle[] buffer = new ParticleSystem.Particle[64];
        private float density = -1;
        private int ticket;

        private sealed class NativeParticle
        {
            public Color32 Color;
            public Vector3 Size;
            public float Remaining, Lifetime;
            public int Ticket;
        }

        public ParticleState(ParticleSystem system)
        {
            this.system = system;
            var emission = system.emission;
            timeRate = emission.rateOverTime; distanceRate = emission.rateOverDistance;
            bursts = new ParticleSystem.Burst[emission.burstCount]; emission.GetBursts(bursts);
        }

        public void Apply(VisualOptions options)
        {
            if (!system) return;
            if (density != options.Density)
            {
                var emission = system.emission;
                emission.rateOverTime = CurveScale.Multiply(timeRate, options.Density);
                emission.rateOverDistance = CurveScale.Multiply(distanceRate, options.Density);
                var adjusted = new ParticleSystem.Burst[bursts.Length];
                for (int i = 0; i < bursts.Length; i++)
                {
                    adjusted[i] = bursts[i]; adjusted[i].count = CurveScale.Multiply(bursts[i].count, options.Density);
                }
                emission.SetBursts(adjusted); density = options.Density;
            }
            ApplyLive(options);
        }

        private void ApplyLive(VisualOptions options)
        {
            if (!system || !system.gameObject.activeInHierarchy) return;
            int count = system.particleCount;
            if (count > buffer.Length) buffer = new ParticleSystem.Particle[Mathf.NextPowerOfTwo(count)];
            count = system.GetParticles(buffer);
            ticket++;
            for (int i = 0; i < count; i++)
            {
                var particle = buffer[i];
                List<NativeParticle> candidates;
                if (!live.TryGetValue(particle.randomSeed, out candidates))
                {
                    candidates = new List<NativeParticle>(); live.Add(particle.randomSeed, candidates);
                }
                NativeParticle native = null;
                // Duplicate random seeds are possible. Match each living particle once using
                // remaining lifetime, so array reordering and paused simulation do not compound.
                float closest = float.MaxValue;
                foreach (var candidate in candidates)
                {
                    float difference = candidate.Remaining - particle.remainingLifetime;
                    if (candidate.Ticket == ticket || candidate.Lifetime != particle.startLifetime || difference < -0.0001f) continue;
                    if (difference < closest) { native = candidate; closest = difference; }
                }
                if (native == null)
                {
                    native = new NativeParticle { Color = particle.startColor, Size = particle.startSize3D, Lifetime = particle.startLifetime };
                    candidates.Add(native);
                }
                native.Ticket = ticket; native.Remaining = particle.remainingLifetime;
                Color color = native.Color;
                if (options.TintEnabled) { color.r = options.Tint.r; color.g = options.Tint.g; color.b = options.Tint.b; }
                color.r *= options.Glow; color.g *= options.Glow; color.b *= options.Glow; color.a *= options.Opacity;
                particle.startColor = color;
                particle.startSize3D = native.Size * options.Size;
                buffer[i] = particle;
            }
            // No Clear/Stop/Play: lifetime, velocity, collision/trigger modules stay unchanged.
            system.SetParticles(buffer, count);
            emptySeeds.Clear();
            foreach (var pair in live)
            {
                pair.Value.RemoveAll(p => p.Ticket != ticket);
                if (pair.Value.Count == 0) emptySeeds.Add(pair.Key);
            }
            foreach (var seed in emptySeeds) live.Remove(seed);
        }

        public void Restore()
        {
            if (!system) return;
            var emission = system.emission;
            emission.rateOverTime = timeRate; emission.rateOverDistance = distanceRate; emission.SetBursts(bursts);
            ApplyLive(VisualOptions.Original()); live.Clear();
        }
    }

    internal sealed class RendererState : IDisposable
    {
        private readonly Renderer renderer;
        private readonly bool forceOff;
        private readonly List<MaterialState> materials = new List<MaterialState>();
        private readonly Material[] originals, clones;
        private readonly float width;
        private readonly bool particle;
        public RendererState(Renderer renderer)
        {
            this.renderer = renderer; forceOff = renderer.forceRenderingOff;
            particle = renderer is ParticleSystemRenderer;
            var trail = renderer as TrailRenderer; var line = renderer as LineRenderer;
            width = trail ? trail.widthMultiplier : line ? line.widthMultiplier : 1;
            originals = renderer.sharedMaterials; clones = new Material[originals.Length];
            try
            {
                for (int i = 0; i < originals.Length; i++)
                {
                    if (!originals[i]) continue;
                    clones[i] = new Material(originals[i]);
                    materials.Add(new MaterialState(clones[i], particle));
                }
                renderer.sharedMaterials = clones;
            }
            catch { Dispose(); throw; }
        }

        public void Apply(VisualOptions options)
        {
            if (!renderer) return;
            renderer.forceRenderingOff = forceOff || options.Opacity <= 0 || options.Size <= 0 || (particle && options.Density <= 0);
            foreach (var material in materials) material.Apply(options);
            var trail = renderer as TrailRenderer; if (trail) trail.widthMultiplier = width * options.Size;
            var line = renderer as LineRenderer; if (line) line.widthMultiplier = width * options.Size;
        }

        public void Dispose()
        {
            if (renderer)
            {
                renderer.forceRenderingOff = forceOff;
                var current = renderer.sharedMaterials;
                // Restore only slots still owned by this state, respecting native replacements.
                for (int i = 0; i < Math.Min(current.Length, originals.Length); i++) if (current[i] == clones[i]) current[i] = originals[i];
                renderer.sharedMaterials = current;
                var trail = renderer as TrailRenderer; if (trail) trail.widthMultiplier = width;
                var line = renderer as LineRenderer; if (line) line.widthMultiplier = width;
            }
            foreach (var clone in clones) if (clone) ReleaseVisual.Destroy(clone);
        }
    }

    internal sealed class MaterialState
    {
        private readonly Material material;
        private readonly bool particle;
        private readonly List<ColorProperty> colors = new List<ColorProperty>();
        private sealed class ColorProperty { public string Name; public Color Native, Last; public bool Written, Emission; }
        public MaterialState(Material material, bool particle)
        {
            this.material = material; this.particle = particle;
            foreach (string name in new[] { "_Color", "_BaseColor", "_TintColor", "_EmissionColor", "_EmissiveColor", "_EmisColor" })
                if (material.HasProperty(name)) colors.Add(new ColorProperty { Name = name, Native = material.GetColor(name), Emission = name.IndexOf("Emis", StringComparison.Ordinal) >= 0 });
        }
        public void Apply(VisualOptions options)
        {
            if (!material) return;
            foreach (var property in colors)
            {
                Color current = material.GetColor(property.Name);
                if (property.Written && current != property.Last) property.Native = current;
                Color value = property.Native;
                if (options.TintEnabled) { value.r = options.Tint.r; value.g = options.Tint.g; value.b = options.Tint.b; }
                // Particle start color already controls RGB/alpha. Material emission still needs
                // its own multiplier; do not multiply particle base alpha a second time.
                if (property.Emission || !particle) { value.r *= options.Glow; value.g *= options.Glow; value.b *= options.Glow; }
                if (!particle) value.a *= options.Opacity;
                material.SetColor(property.Name, value); property.Last = value; property.Written = true;
            }
        }
    }

    internal sealed class MeshState : IDisposable
    {
        private readonly MeshFilter filter;
        private readonly Mesh original, clone;
        private readonly Vector3[] vertices, adjusted;
        private readonly Vector3[] normals, adjustedNormals;
        private readonly Vector4[] tangents, adjustedTangents;
        private readonly Vector3 center;
        private readonly bool shieldBubble;
        private float size = -1;
        private float ovality = -1;
        public MeshState(MeshFilter filter, bool shieldBubble)
        {
            this.filter = filter; this.shieldBubble = shieldBubble; original = filter.sharedMesh;
            clone = UnityEngine.Object.Instantiate(original); vertices = original.vertices;
            normals = original.normals; adjustedNormals = new Vector3[normals.Length];
            tangents = original.tangents; adjustedTangents = new Vector4[tangents.Length];
            adjusted = new Vector3[vertices.Length]; center = original.bounds.center;
            filter.sharedMesh = clone;
        }
        public void Apply(VisualOptions options)
        {
            if (!filter || filter.sharedMesh != clone) return;
            float nextOvality = shieldBubble ? Mathf.Clamp01(options.ShieldOvality) : 0;
            if (options.Size == size && nextOvality == ovality) return;
            float horizontal = 1 - nextOvality * 0.5f;
            var scale = new Vector3(horizontal, 1, horizontal) * options.Size;
            for (int i = 0; i < vertices.Length; i++) adjusted[i] = center + Vector3.Scale(vertices[i] - center, scale);
            clone.vertices = adjusted;
            for (int i = 0; i < normals.Length; i++) adjustedNormals[i] = nextOvality == 0 ? normals[i] : new Vector3(normals[i].x / horizontal, normals[i].y, normals[i].z / horizontal).normalized;
            for (int i = 0; i < tangents.Length; i++)
            {
                if (nextOvality == 0) { adjustedTangents[i] = tangents[i]; continue; }
                Vector3 tangent = new Vector3(tangents[i].x * horizontal, tangents[i].y, tangents[i].z * horizontal);
                if (i < adjustedNormals.Length) tangent -= adjustedNormals[i] * Vector3.Dot(adjustedNormals[i], tangent);
                tangent.Normalize(); adjustedTangents[i] = new Vector4(tangent.x, tangent.y, tangent.z, tangents[i].w);
            }
            clone.normals = adjustedNormals; clone.tangents = adjustedTangents;
            clone.RecalculateBounds(); size = options.Size; ovality = nextOvality;
        }
        public void Dispose()
        {
            if (filter && filter.sharedMesh == clone) filter.sharedMesh = original;
            if (clone) ReleaseVisual.Destroy(clone);
        }
    }

    internal static class ReleaseVisual
    {
        public static void Destroy(UnityEngine.Object resource)
        {
            if (Application.isPlaying) UnityEngine.Object.Destroy(resource);
            else UnityEngine.Object.DestroyImmediate(resource);
        }
    }
}
