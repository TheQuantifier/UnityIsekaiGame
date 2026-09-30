namespace UnityIsekaiGame.Gameplay
{
    /// <summary>
    /// Declares which process is allowed to advance and persist authoritative simulation state.
    /// Network clients are replicas: they may apply server projections, but never advance or save
    /// the world independently.
    /// </summary>
    public enum SimulationRuntimeRole
    {
        Auto = 0,
        StandaloneAuthoritative = 1,
        DedicatedServerAuthoritative = 2,
        NetworkClientReplica = 3
    }
}
