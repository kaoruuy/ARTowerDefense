using UnityEngine;

namespace ARTD
{
    /// <summary>Uniform size multiplier for gameplay content. Apply once to each independent root.</summary>
    public static class GameplayScale
    {
        public const float Factor = 1.5f;

        public static void Apply(Transform root)
        {
            root.localScale *= Factor;

            // Keep overhead UI at its authored size; its position follows the larger model.
            foreach (var bar in root.GetComponentsInChildren<HealthBar>(true))
                bar.transform.localScale /= Factor;

            foreach (var trail in root.GetComponentsInChildren<TrailRenderer>(true))
                trail.widthMultiplier *= Factor;

            ConfigureParticles(root);
        }

        // Attached effects already inherit their owner's scale; do not multiply their root again.
        public static void ConfigureParticles(Transform root)
        {
            foreach (var particles in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = particles.main;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            }
        }
    }
}
