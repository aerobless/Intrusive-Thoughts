using Newtonsoft.Json.Linq;
using OpenAI;

/// <summary>
/// Helper for constructing the speak tool definition offered to the language model.
/// </summary>
public static class AgentSpeakTool
{
    public static Tool BuildSpeakTool()
    {
        var parameters = new JObject
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["text"] = new JObject
                {
                    ["type"] = "string",
                    ["description"] = "What the character says out loud."
                }
            },
            ["required"] = new JArray("text"),
            ["additionalProperties"] = false
        };

        var function = new Function(
            "speak",
            "Says something out loud to anyone nearby.",
            parameters,
            strict: true);

        return new Tool(function);
    }
}
