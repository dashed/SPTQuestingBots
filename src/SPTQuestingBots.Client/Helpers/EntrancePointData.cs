namespace SPTQuestingBots.Helpers
{
    /// <summary>
    /// Data for a building/zone entrance point from BSG's <see cref="BotZoneEntrance"/>.
    /// </summary>
    public struct EntrancePointData
    {
        /// <summary>Position outside the entrance.</summary>
        public float OutsideX,
            OutsideY,
            OutsideZ;

        /// <summary>Position inside the entrance.</summary>
        public float InsideX,
            InsideY,
            InsideZ;

        /// <summary>Center of the entrance opening.</summary>
        public float CenterX,
            CenterY,
            CenterZ;

        /// <summary>Area ID this entrance connects to.</summary>
        public int ConnectedAreaId;

        /// <summary>Entrance ID.</summary>
        public int Id;
    }
}
