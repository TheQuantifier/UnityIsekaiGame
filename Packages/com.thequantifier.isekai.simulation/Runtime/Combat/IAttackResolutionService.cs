namespace UnityIsekaiGame.Combat
{
    public interface IAttackResolutionService
    {
        AttackResolutionResult PreviewAttack(AttackResolutionRequest request);
        AttackResolutionResult ExecuteAttack(AttackResolutionRequest request);
    }
}
