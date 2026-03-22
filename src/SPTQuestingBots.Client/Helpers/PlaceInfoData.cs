namespace SPTQuestingBots.Helpers
{
    /// <summary>
    /// Environment info for a position, derived from BSG's <see cref="AIPlaceInfo"/> system.
    /// </summary>
    public struct PlaceInfoData
    {
        /// <summary>Whether the position is inside a building/room.</summary>
        public bool IsInside;

        /// <summary>Whether the position is in a dark area.</summary>
        public bool IsDark;

        /// <summary>The area ID grouping cover points in this zone.</summary>
        public int AreaId;

        /// <summary>Whether a matching AIPlaceInfo was found.</summary>
        public bool IsValid;
    }
}
