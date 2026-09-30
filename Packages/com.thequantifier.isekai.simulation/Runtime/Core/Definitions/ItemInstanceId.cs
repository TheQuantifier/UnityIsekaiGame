using System;

namespace UnityIsekaiGame.GameData
{
    public static class ItemInstanceId
    {
        public static string Generate()
        {
            return Guid.NewGuid().ToString("D");
        }

        public static bool IsValid(string instanceId)
        {
            return string.IsNullOrWhiteSpace(instanceId)
                || Guid.TryParseExact(instanceId, "D", out _);
        }
    }
}
