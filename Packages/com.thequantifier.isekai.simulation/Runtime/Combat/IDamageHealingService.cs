namespace UnityIsekaiGame.Combat
{
    public interface IDamageHealingService
    {
        DamageApplicationResult PreviewDamage(DamageApplicationRequest request);
        DamageApplicationResult ApplyDamage(DamageApplicationRequest request);
        HealingApplicationResult PreviewHealing(HealingApplicationRequest request);
        HealingApplicationResult ApplyHealing(HealingApplicationRequest request);
    }
}
