namespace UnityIsekaiGame.People
{
    public interface IContractProvider : IPersonCapability
    {
        string ContractProviderId { get; }
    }
}
