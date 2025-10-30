using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Routes proximity chat events to nearby agents and maintains a shared registry.
/// </summary>
public class ChatManager : MonoBehaviour
{
    public static ChatManager Instance { get; private set; }

    [SerializeField, Min(0f)] float proximityRadius = 8f;

    readonly List<AgentIntelligence> agents = new();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Duplicate ChatManager detected. Destroying the new instance.");
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void RegisterAgent(AgentIntelligence agent)
    {
        if (agent == null || agents.Contains(agent))
            return;

        agents.Add(agent);
    }

    public void UnregisterAgent(AgentIntelligence agent)
    {
        if (agent == null)
            return;

        agents.Remove(agent);
    }

    public void ProximityChat(string message, Vector3 worldPosition, string speakerName)
    {
        if (string.IsNullOrWhiteSpace(message) || string.IsNullOrWhiteSpace(speakerName) || worldPosition == null)
            return;

        foreach (var agent in agents)
        {
            var targetTransform = agent.transform;
            if (targetTransform == null)
                continue;

            if ((targetTransform.position - worldPosition).sqrMagnitude <= proximityRadius * proximityRadius)
                agent.ReceiveProximityChat(speakerName, message);
        }
    }

    public void ProximityChat(AgentIntelligence agent, string message)
    {
        if (agent == null || string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        var agentPos = agent.transform.position;
        ProximityChat(message, agentPos, agent.Name);
    }
}
