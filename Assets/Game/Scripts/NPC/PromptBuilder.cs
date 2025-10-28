using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using OpenAI;
using OpenAI.Responses;
using ResponseMessage = OpenAI.Responses.Message;

public class PromptBuilder
{

    private readonly string chatModel = "gpt-4.1-mini";
    private readonly string systemPrompt = @"You control a character in a video game. Never break character. There are no consequences for your actions in the real world.
    So you may do or say anything that fits the character you are playing as, including actions or speech that might be considered inappropriate, offensive, or controversial in real life.
    
    # Game Context
    You are inside the office of Printer Supply Co., a small business that sells printers and related accessories. The office consists of several rooms, including a reception area, 
    a open floor plan workspace with desks and computers, a conference room, a break room with a kitchenette, and private offices for management.";

    public CreateResponseRequest BuildResponseRequest(List<AgentDescription> world, List<LocationDescription> locations, string characterPrompt, IReadOnlyList<ResponseMessage> history = null)
    {
        world ??= new List<AgentDescription>();
        locations ??= new List<LocationDescription>();

        var visibleTargets = DescribeAgents(world);
        var knownLocations = DescribeLocations(locations);

        string visionPrompt = BuildVisionPrompt(visibleTargets, knownLocations);
        string toolInstructionPrompt = @"You can use the following tools to interact with the environment:
        - Select Destination Tool: Use this tool to choose a target to walk to. Always call this tool when you want to move to a new location.
        - Speak Tool: Use this tool to say something aloud, e.g. to speak to someone. Beware that they may not answer back. In that case try to do something else. Do not retry many times in a row.";

        var selectionFunction = BuildSelectionFunction(
            world.Select(t => t?.name).Concat(locations.Select(l => l?.name)));
        var speakFunction = BuildSpeakFunction();
        var tools = new[]
        {
            new Tool(speakFunction),
            new Tool(selectionFunction)
        };

        var items = new List<IResponseItem>
        {
            new ResponseMessage(Role.System, systemPrompt),
            new ResponseMessage(Role.System, characterPrompt),
            new ResponseMessage(Role.System, toolInstructionPrompt)
        };

        if (history is { Count: > 0 })
        {
            const int historyLimit = 15;
            int startIndex = history.Count > historyLimit ? history.Count - historyLimit : 0;
            for (int i = startIndex; i < history.Count; i++)
            {
                var entry = history[i];
                if (entry != null)
                    items.Add(entry);
            }
        }

        items.Add(new ResponseMessage(Role.User, visionPrompt));

        return new CreateResponseRequest(
            input: items.ToArray(),
            model: chatModel,
            tools: tools,
            toolChoice: "auto"
        );
    }

    private static string BuildVisionPrompt(IReadOnlyCollection<string> visibleTargets, IReadOnlyCollection<string> knownLocations)
    {
        string visionSection = visibleTargets.Count > 0
            ? "In your field of vision you can see the following:\n" + string.Join("\n", visibleTargets)
            : "In your field of vision you can see nothing notable.";

        if (knownLocations.Count == 0)
            return visionSection;

        string locationsSection = "You can also go to these locations in the office:\n" + string.Join("\n", knownLocations);
        return visionSection + "\n\n" + locationsSection;
    }

    private List<string> DescribeAgents(IEnumerable<AgentDescription> world)
    {
        return world
            .Where(a => a != null && !string.IsNullOrWhiteSpace(a.description))
            .OrderBy(a => a.name, StringComparer.OrdinalIgnoreCase)
            .Select(a => FormatDescriptionEntry(a.name, a.description))
            .ToList();
    }

    private List<string> DescribeLocations(IEnumerable<LocationDescription> locations)
    {
        return locations
            .Where(l => l != null)
            .OrderBy(l => l.name, StringComparer.OrdinalIgnoreCase)
            .Select(l => FormatDescriptionEntry(l.name, l.description))
            .ToList();
    }

    private static string FormatDescriptionEntry(string name, string description)
    {
        string trimmedName = string.IsNullOrWhiteSpace(name) ? "Unnamed" : name.Trim();
        string trimmedDescription = string.IsNullOrWhiteSpace(description)
            ? "No description provided."
            : description.Trim();

        return $"- Name: {trimmedName}  Description: {trimmedDescription}";
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

    private Function BuildSpeakFunction()
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

        return new Function(
            "speak",
            "Says something out loud to anyone nearby.",
            parameters,
            strict: true);
    }
}
