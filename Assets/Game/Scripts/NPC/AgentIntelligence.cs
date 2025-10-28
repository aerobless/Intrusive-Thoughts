using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenAI;
using OpenAI.Responses;
using UnityEngine;
using Synty.AnimationBaseLocomotion.Samples;
using ResponseMessage = OpenAI.Responses.Message;
using ResponseTextContent = OpenAI.Responses.TextContent;

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
    const string SpeakFunctionName = "speak";

    AgentHarness harness;
    AgentPerception agentPerception;
    OpenAIClient client;
    bool decisionInProgress;
    CancellationTokenSource decisionLoopCts;
    Task decisionLoopTask;
    bool missingKeyLogged;
    AgentWalkTool walkToTool;

    PromptBuilder promptBuilder = new();
    readonly List<ResponseMessage> conversationHistory = new();
    const int MaxHistoryEntries = 3;
    string pendingArrivalTarget;
    readonly List<LocationDescription> cachedLocations = new();

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
        CacheKnownLocations();
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

        RecordArrivalIfNeeded();

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

            var locations = CollectLocations();

            var request = promptBuilder.BuildResponseRequest(targets, locations, characterPrompt, conversationHistory);
            var response = await client.ResponsesEndpoint.CreateModelResponseAsync(request);

            var speechOutputs = new List<string>();
            var decision = ExtractDecision(response, speechOutputs);

            if (speechOutputs.Count > 0)
            {
                foreach (var speech in speechOutputs)
                {
                    DisplaySpeech(speech);
                    RecordHistory(speech);
                }
            }

            if (decision == null)
            {
                var fallback = ExtractContent(response);
                if (!TryParseDecision(fallback, out decision))
                {
                    Debug.LogWarning("AgentIntelligence could not determine a destination from the model response.");
                    return;
                }
            }

            if (decision == null)
                return;

            if (speechOutputs.Count == 0)
            {
                DisplaySpeech(decision.thoughts);
            }

            RecordHistory(decision.thoughts);
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

    void RecordArrivalIfNeeded()
    {
        if (string.IsNullOrEmpty(pendingArrivalTarget))
            return;
        if (harness == null)
            return;
        if (!harness.IsIdle)
            return;

        var message = $"You have reached \"{pendingArrivalTarget}\".";
        RecordSystemHistory(message);
        pendingArrivalTarget = null;
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

    void CacheKnownLocations()
    {
        cachedLocations.Clear();
        cachedLocations.AddRange(FindObjectsByType<LocationDescription>(FindObjectsInactive.Exclude, FindObjectsSortMode.None));
        cachedLocations.RemoveAll(location => location == null);
        cachedLocations.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));
    }

    List<LocationDescription> CollectLocations()
    {
        CacheKnownLocations();
        return new List<LocationDescription>(cachedLocations);
    }

    string ExtractContent(Response response)
    {
        if (response == null)
            return string.Empty;

        if (!string.IsNullOrWhiteSpace(response.OutputText))
            return response.OutputText.Trim();

        if (response.Output == null)
            return string.Empty;

        foreach (var item in response.Output)
        {
            if (item is not ResponseMessage message || message.Role != Role.Assistant || message.Content == null)
                continue;

            foreach (var content in message.Content)
            {
                if (content is ResponseTextContent textContent)
                {
                    var text = textContent.ToString();
                    if (!string.IsNullOrWhiteSpace(text))
                        return text.Trim();
                }
            }
        }

        return string.Empty;
    }

    NavigationDecision ExtractDecision(Response response, List<string> speechOutputs)
    {
        if (response?.Output == null)
            return null;

        NavigationDecision decision = null;

        foreach (var item in response.Output)
        {
            if (item is not FunctionToolCall toolCall)
                continue;

            var arguments = NormalizeArguments(toolCall.Arguments);

            if (string.Equals(toolCall.Name, SpeakFunctionName, StringComparison.OrdinalIgnoreCase))
            {
                var text = arguments?.Value<string>("text");
                if (!string.IsNullOrWhiteSpace(text))
                {
                    speechOutputs?.Add(text.Trim());
                }

                continue;
            }

            if (!string.Equals(toolCall.Name, SelectDestinationFunctionName, StringComparison.OrdinalIgnoreCase))
                continue;

            if (arguments == null)
                continue;

            var target = arguments.Value<string>("target");
            if (string.IsNullOrWhiteSpace(target))
                continue;

            var thoughts = arguments.Value<string>("thoughts");
            if (decision == null)
            {
                decision = new NavigationDecision
                {
                    target = target.Trim(),
                    thoughts = string.IsNullOrWhiteSpace(thoughts)
                        ? $"Heading to {target.Trim()}."
                        : thoughts.Trim()
                };
            }
        }

        return decision;
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

    void DisplaySpeech(string message)
    {
        if (string.IsNullOrEmpty(message))
            return;

        Debug.Log("AgentIntelligence speech: " + message);
        var textOutput = harness?.TextOutput;
        if (textOutput != null)
            textOutput.ShowSpeech(message);
    }

    void DriveAgent(string targetName)
    {
        if (string.IsNullOrWhiteSpace(targetName))
        {
            Debug.LogWarning("AgentIntelligence received an empty target name.");
            return;
        }

        string trimmedTarget = targetName.Trim();
        walkToTool.WalkTo(trimmedTarget);

        pendingArrivalTarget = harness?.CurrentTarget != null ? trimmedTarget : null;
    }

    void RecordHistory(string assistantThoughts)
    {
        if (string.IsNullOrWhiteSpace(assistantThoughts))
            return;

        var assistantMessage = new ResponseMessage(
            Role.Assistant,
            new IResponseContent[]
            {
                new AssistantOutputContent(assistantThoughts.Trim())
            });

        conversationHistory.Add(assistantMessage);

        TrimHistory();
    }

    class AssistantOutputContent : BaseResponse, IResponseContent
    {
        [JsonProperty("type", DefaultValueHandling = DefaultValueHandling.Include)]
        public ResponseContentType Type => ResponseContentType.OutputText;

        [JsonProperty("text", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string Text { get; }

        public AssistantOutputContent(string text)
        {
            Text = text;
        }

        public string Object => Type.ToString();

        public override string ToString() => Text ?? string.Empty;
    }

    void RecordSystemHistory(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        conversationHistory.Add(new ResponseMessage(Role.System, message.Trim()));
        TrimHistory();
    }

    void TrimHistory()
    {
        int excess = conversationHistory.Count - MaxHistoryEntries;
        if (excess > 0)
            conversationHistory.RemoveRange(0, excess);
    }
}
