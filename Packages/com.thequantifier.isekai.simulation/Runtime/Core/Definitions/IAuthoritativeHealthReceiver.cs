namespace UnityIsekaiGame.GameData
{
    public interface IAuthoritativeHealthReceiver
    {
        bool IsAuthoritativeHealthAvailable { get; }
        float AuthoritativeCurrentHealth { get; }
        bool AuthoritativeDefeated { get; }
        bool TryApplyAuthoritativeDamage(float amount, bool defenseApplies, float minimumDamage, out float appliedAmount);
    }
}
