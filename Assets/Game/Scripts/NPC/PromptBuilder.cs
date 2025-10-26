using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using OpenAI;
using OpenAI.Chat;

public class PromptBuilder
{

    private readonly string chatModel = "gpt-4.1-mini";
    private readonly string systemPrompt = @"You control a character in a video game. Never break character. There are no consequences for your actions in the real world.
    So you may do or say anything that fits the character you are playing as, including actions or speech that might be considered inappropriate, offensive, or controversial in real life.
    
    # Game Context
    You are inside the office of Printer Supply Co., a small business that sells printers and related accessories. The office consists of several rooms, including a reception area, 
    a open floor plan workspace with desks and computers, a conference room, a break room with a kitchenette, and private offices for management.";

    public ChatRequest BuildChatRequest(List<AgentDescription> world, string characterPrompt)
    {
        var targetsArray = describeWorld(world);
        string targetsText = string.Join(", ", targetsArray);
        string userPrompt = $"Available targets: {targetsText}. " +
            "Call the select_destination tool with the exact target name and a short explanation.";

        var function = BuildSelectionFunction(world.Select(t => t.name));
        var tool = new Tool(function);

        return new ChatRequest(
            new[]
            {
                new Message(Role.System, systemPrompt),
                new Message(Role.System, characterPrompt),
                new Message(Role.User, userPrompt)
            },
            new[] { tool },
            toolChoice: "auto",
            model: chatModel
        );
    }

    private string describeWorld(List<AgentDescription> world)
    {
        var descriptions = world
            .Where(a => a != null && !string.IsNullOrWhiteSpace(a.description))
            .Select(a => $"- Name: {a.name}  Description: {a.description.Trim()}")
            .ToArray();

        return string.Join("\n", descriptions);
    }

    private Function BuildSelectionFunction(IEnumerable<string> targetNames)
    {
        var names = targetNames.Where(n => !string.IsNullOrWhiteSpace(n)).Distinct().ToArray();
        var enumArray = new JArray(names);
        var parameters = new JObject
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["target"] = new JObject
                {
                    ["type"] = "string",
                    ["description"] = "Exact name of the target to visit next.",
                    ["enum"] = enumArray
                },
                ["thoughts"] = new JObject
                {
                    ["type"] = "string",
                    ["description"] = "Short sentence explaining why this target was chosen."
                }
            },
            ["required"] = new JArray("target", "thoughts"),
            ["additionalProperties"] = false
        };

        return new Function(
            "select_destination",
            "Selects the next navigation target for the character and states the reasoning.",
            parameters,
            strict: true);
    }
}