using UnityEngine;

public enum GameLogCategory
{
    General,
    Data,
    Network,
    Performance,
}

public static class GameLog
{
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    public static void Debug(GameLogCategory category, string message)
    {
        UnityEngine.Debug.Log("U2_" + category.ToString().ToUpperInvariant() + "_DEBUG: " + message);
    }

    public static void Warning(GameLogCategory category, string code, string message)
    {
        UnityEngine.Debug.LogWarning("U2_" + category.ToString().ToUpperInvariant() + "_" + code + ": " + message);
    }

    public static void Error(GameLogCategory category, string code, string message)
    {
        UnityEngine.Debug.LogError("U2_" + category.ToString().ToUpperInvariant() + "_" + code + ": " + message);
    }
}
