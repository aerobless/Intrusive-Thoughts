using System;
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
