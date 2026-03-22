namespace SPTQuestingBots.Helpers
{
    /// <summary>
    /// Per-zone bot behavior parameters from BSG's <see cref="BotLocationModifier"/>.
    /// Pure C# struct for zero-allocation reads in scoring and behavior.
    /// </summary>
    public struct ZoneModifierData
    {
        /// <summary>Zone-specific visible distance multiplier (meters).</summary>
        public float VisibleDistance;

        /// <summary>Distance threshold for bot sleep/activation.</summary>
        public float DistToSleep;

        /// <summary>Distance threshold for bot activation from sleep.</summary>
        public float DistToActivate;

        /// <summary>Accuracy speed multiplier for this zone.</summary>
        public float AccuracySpeed;

        /// <summary>Sight gain multiplier for this zone.</summary>
        public float GainSight;

        /// <summary>Scattering multiplier for this zone.</summary>
        public float Scattering;

        /// <summary>Fog visibility distance coefficient (0-1, lower = more obscured).</summary>
        public float FogVisibilityDistanceCoef;

        /// <summary>Rain visibility distance coefficient (0-1, lower = more obscured).</summary>
        public float RainVisibilityDistanceCoef;

        /// <summary>Whether valid data was loaded.</summary>
        public bool IsValid;
    }
}
