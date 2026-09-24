namespace UnityIsekaiGame.Stats
{
    public interface IRuntimeCalculatedStatReceiver
    {
        bool HasCalculatedStat(string statId);
        float GetCalculatedStatValue(string statId);
        bool AddCalculatedStatContribution(RuntimeCalculatedStatContribution contribution);
        bool RemoveCalculatedStatContributions(StatModifierSource source);
    }
}
