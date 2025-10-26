using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenAI;
using OpenAI.Chat;
using UnityEngine;
using Synty.AnimationBaseLocomotion.Samples;

[RequireComponent(typeof(AgentHarness))]
[RequireComponent(typeof(AgentPerception))]
public class AgentIntelligence : MonoBehaviour
{
    [Header("Agent Details")]

    [SerializeField, TextArea(5, 20)]
    public string characterPrompt;

    [SerializeField, Min(0.1f)] float decisionCheckInterval = 0.75f;

    [Header("Navigation targets")]

    const string SelectDestinationFunctionName = "select_destination";

    AgentHarness harness;
    AgentPerception agentPerception;
    OpenAIClient client;
    bool decisionInProgress;
    CancellationTokenSource decisionLoopCts;
    Task decisionLoopTask;
    bool missingKeyLogged;
    AgentWalkTool walkToTool;

    PromptBuilder promptBuilder = new();

    [Serializable]
    class NavigationDecision
    {
        public string thoughts;
        public string target;
    }

    void Awake()
    {
        harness = GetComponent<AgentHarness>();
        agentPerception = GetComponent<AgentPerception>(); 
        walkToTool = new AgentWalkTool(harness);
    }

    async void Start()
    {
        StartDecisionLoop();
    }

    void OnEnable()
    {
        StartDecisionLoop();
    }

    void OnDisable()
    {
        StopDecisionLoop();
    }

    void OnDestroy()
    {
        StopDecisionLoop();
    }

    public async Task DecideOnNextAction()
    {
        if (decisionInProgress || !CanRequestDecision())
            return;

        decisionInProgress = true;

        try
        {
            if (!await EnsureClientAsync())
                return;

            if (harness == null)
            {
                Debug.LogError("AgentIntelligence requires an AgentHarness component.");
                return;
            }

            var targets = CollectTargets();
            if (targets.Count == 0)
            {
                Debug.LogWarning("AgentIntelligence has no targets to offer the language model.");
                return;
            }

            var request = promptBuilder.BuildChatRequest(targets, characterPrompt);
            var response = await client.ChatEndpoint.GetCompletionAsync(request);
            var decision = ExtractDecision(response);
            if (decision == null)
            {
                var fallback = ExtractContent(response);
                if (!TryParseDecision(fallback, out decision))
                {
                    Debug.LogWarning("AgentIntelligence could not determine a destination from the model response.");
                    return;
                }
            }

            PresentThoughts(decision.thoughts);
            DriveAgent(decision.target);
        }
        catch (Exception ex)
        {
            Debug.LogError("AgentIntelligence failed to decide on a destination: " + ex.Message);
        }
        finally
        {
            decisionInProgress = false;
        }
    }

    async Task<bool> EnsureClientAsync()
    {
        if (client != null)
            return true;

        var key = OpenAiConfig.GetApiKey();
        if (string.IsNullOrEmpty(key))
        {
            if (!missingKeyLogged)
            {
                Debug.LogError("AgentIntelligence has no OpenAI API key available.");
                missingKeyLogged = true;
            }
            return false;
        }

        missingKeyLogged = false;
        client = new OpenAIClient(new OpenAIAuthentication(key));
        return true;
    }

    void StartDecisionLoop()
    {
        if (!Application.isPlaying)
            return;

        if (decisionLoopCts != null)
            return;

        decisionLoopCts = new CancellationTokenSource();
        decisionLoopTask = DecisionLoopAsync(decisionLoopCts.Token);
    }

    void StopDecisionLoop()
    {
        if (decisionLoopCts == null)
            return;

        decisionLoopCts.Cancel();
        decisionLoopCts.Dispose();
        decisionLoopCts = null;
        decisionLoopTask = null;
    }

    async Task DecisionLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            if (CanRequestDecision())
            {
                try
                {
                    await DecideOnNextAction();
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }

            try
            {
                float interval = Mathf.Max(0.1f, decisionCheckInterval);
                await Task.Delay(TimeSpan.FromSeconds(interval), token);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }

    bool CanRequestDecision()
    {
        if (decisionInProgress)
            return false;
        if (harness == null)
            return false;
        if (!harness.IsIdle)
            return false;
        return true;
    }

    void OnValidate()
    {
        decisionCheckInterval = Mathf.Max(0.1f, decisionCheckInterval);
    }

    List<AgentDescription> CollectTargets()
    {
        var visibleTargets = agentPerception.VisibleTargets.ToList();
        Debug.Log("Visible targets: " + string.Join(", ", visibleTargets.Select(t => t.name)));
        return visibleTargets;
    }

    string ExtractContent(ChatResponse response)
    {
        if (response == null || response.FirstChoice == null)
            return string.Empty;

        var message = response.FirstChoice.Message;
        if (message.Content is string s)
            return s.Trim();

        return message.Content?.ToString().Trim() ?? string.Empty;
    }

    NavigationDecision ExtractDecision(ChatResponse response)
    {
        var toolCalls = response?.FirstChoice?.Message?.ToolCalls;
        if (toolCalls == null || toolCalls.Count == 0)
            return null;

        foreach (var toolCall in toolCalls)
        {
            if (!string.Equals(toolCall.Name, SelectDestinationFunctionName, StringComparison.OrdinalIgnoreCase))
                continue;

            var arguments = NormalizeArguments(toolCall.Arguments);
            if (arguments == null)
                continue;

            var target = arguments.Value<string>("target");
            if (string.IsNullOrWhiteSpace(target))
                continue;

            var thoughts = arguments.Value<string>("thoughts");
            return new NavigationDecision
            {
                target = target.Trim(),
                thoughts = string.IsNullOrWhiteSpace(thoughts)
                    ? $"Heading to {target.Trim()}."
                    : thoughts.Trim()
            };
        }

        return null;
    }

    JToken NormalizeArguments(JToken arguments)
    {
        if (arguments == null)
            return null;

        if (arguments is JValue value && value.Type == JTokenType.String)
        {
            string raw = value.Value<string>();
            if (string.IsNullOrWhiteSpace(raw))
                return null;

            try
            {
                return JToken.Parse(raw);
            }
            catch (JsonException ex)
            {
                Debug.LogWarning("AgentIntelligence failed to parse tool call arguments: " + ex.Message);
                return null;
            }
        }

        return arguments;
    }

    bool TryParseDecision(string raw, out NavigationDecision decision)
    {
        decision = null;
        if (string.IsNullOrEmpty(raw))
            return false;

        string candidate = raw.Trim();

        int jsonStart = candidate.IndexOf('{');
        int jsonEnd = candidate.LastIndexOf('}');
        if (jsonStart >= 0 && jsonEnd >= jsonStart)
            candidate = candidate.Substring(jsonStart, jsonEnd - jsonStart + 1);

        try
        {
            decision = JsonUtility.FromJson<NavigationDecision>(candidate);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("AgentIntelligence JSON parse error: " + ex.Message);
            return false;
        }

        if (decision == null || string.IsNullOrEmpty(decision.target))
            return false;

        decision.thoughts = string.IsNullOrEmpty(decision.thoughts)
            ? $"Heading to {decision.target}."
            : decision.thoughts.Trim();

        return true;
    }

    void PresentThoughts(string thoughts)
    {
        if (string.IsNullOrEmpty(thoughts))
            return;

        Debug.Log("AgentIntelligence thoughts: " + thoughts);
        var textOutput = harness?.TextOutput;
        if (textOutput != null)
            textOutput.ShowSpeech(thoughts);
    }

    void DriveAgent(string targetName)
    {
        if (string.IsNullOrWhiteSpace(targetName))
        {
            Debug.LogWarning("AgentIntelligence received an empty target name.");
            return;
        }

        walkToTool.WalkTo(targetName.Trim());
    }
}
