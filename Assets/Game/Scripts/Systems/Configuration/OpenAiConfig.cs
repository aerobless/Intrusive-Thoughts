using UnityEngine;
using System.IO;

public static class OpenAiConfig
{
    public static string GetApiKey()
    {
        var envPath = Path.Combine(Application.dataPath, "../.env");
        if (!File.Exists(envPath)) return null;

        foreach (var line in File.ReadAllLines(envPath))
        {
            if (line.StartsWith("OPENAI_API_KEY="))
                return line.Substring("OPENAI_API_KEY=".Length).Trim();
        }

        Debug.LogWarning("OPENAI_API_KEY not found in .env file!");
        return null;
    }
}
