namespace EldritchFarm.Crops
{
    /// <summary>
    /// Universal crop state vocabulary. Not every crop uses every state.
    /// Add states here as new behaviors emerge — keep it shared so the
    /// event bus and visual system speak one language.
    /// </summary>
    public enum CropState
    {
        Idle,           // Default — passive, growing, doing crop things
        Alert,          // Something noticed (player nearby, neighbor screamed)
        Reacting,       // Actively expressing behavior (screaming, fleeing, winding up)
        Striking,       // Mid-attack — the actual moment of impact (for aggressive crops)
        Distressed,     // Over-stimulated, may chain-react further
        Harvestable,    // Ready to be collected
        Spent           // Post-harvest / dormant
    }
}