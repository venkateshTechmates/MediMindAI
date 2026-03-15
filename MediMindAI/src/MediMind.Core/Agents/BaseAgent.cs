using System.Diagnostics;
using MediMind.Core.Interfaces;
using MediMind.Core.Models;
using Microsoft.Extensions.Logging;

namespace MediMind.Core.Agents;

/// <summary>
/// Base class for specialist agents providing common execution, timing, and trace capture logic.
/// </summary>
public abstract class BaseAgent
{
    private static readonly ActivitySource _activitySource = new("MediMind.Agents", "1.0.0");

    protected readonly ILLMClient LlmClient;
    protected readonly ILogger Logger;

    protected BaseAgent(ILLMClient llmClient, ILogger logger)
    {
        LlmClient = llmClient;
        Logger = logger;
    }

    public abstract string AgentName { get; }
    public abstract string SystemPrompt { get; }

    /// <summary>
    /// Execute the agent with timing, tracing, and structured result capture.
    /// </summary>
    protected async Task<AgentResult> ExecuteWithTracing(string input, Func<Task<string>> action)
    {
        using var activity = _activitySource.StartActivity($"Agent.{AgentName}.Execute", ActivityKind.Internal);
        activity?.SetTag("agent.name", AgentName);
        activity?.SetTag("agent.input_length", input.Length);

        var sw = Stopwatch.StartNew();
        try
        {
            Logger.LogInformation("[{Agent}] Starting execution. Input length: {Len}", AgentName, input.Length);
            var content = await action();
            sw.Stop();

            Logger.LogInformation("[{Agent}] Completed in {Ms}ms.", AgentName, sw.ElapsedMilliseconds);

            activity?.SetTag("agent.success", true);
            activity?.SetTag("agent.latency_ms", sw.ElapsedMilliseconds);
            activity?.SetTag("agent.response_length", content.Length);

            return new AgentResult
            {
                AgentName = AgentName,
                Success = true,
                Content = content,
                LatencyMs = sw.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            Logger.LogError(ex, "[{Agent}] Failed after {Ms}ms.", AgentName, sw.ElapsedMilliseconds);

            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.SetTag("agent.success", false);
            activity?.SetTag("agent.latency_ms", sw.ElapsedMilliseconds);
            activity?.SetTag("agent.error", ex.Message);

            return new AgentResult
            {
                AgentName = AgentName,
                Success = false,
                Content = string.Empty,
                ErrorMessage = ex.Message,
                LatencyMs = sw.ElapsedMilliseconds
            };
        }
    }
}
