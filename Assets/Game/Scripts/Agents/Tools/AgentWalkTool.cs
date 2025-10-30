using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using OpenAI;
using UnityEngine;
using Synty.AnimationBaseLocomotion.Samples;

/// <summary>
/// Tool wrapper that directs an AgentHarness to walk toward a named target.
/// Prepares for future LLM tool invocations.
/// </summary>
public sealed class AgentWalkTool
{
    readonly AgentHarness harness;

    public AgentWalkTool(AgentHarness harness)
    {
        this.harness = harness ? harness : throw new ArgumentNullException(nameof(harness));
    }

    public static Tool BuildWalkTool(IEnumerable<IDescribable> describables)
    {
        var names = (describables ?? Enumerable.Empty<IDescribable>())
            .Where(desc => !string.IsNullOrWhiteSpace(desc.Name))
            .Select(desc => desc.Name)
            .Distinct()
            .ToArray();

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

        var function = new Function(
            "select_destination",
            "Selects the next navigation target for the character and states the reasoning.",
            parameters,
            strict: true);

        return new Tool(function);
    }

    public void WalkTo(string targetName)
    {
        if (string.IsNullOrWhiteSpace(targetName))
        {
            Debug.LogWarning("AgentWalkTool: Target name was null or empty.");
            return;
        }

        harness.WalkTo(GameObject.Find(targetName.Trim()));
    }
}
