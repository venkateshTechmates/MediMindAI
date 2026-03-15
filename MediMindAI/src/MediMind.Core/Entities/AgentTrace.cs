namespace MediMind.Core.Entities;

/// <summary>
/// Records the execution trace of a Semantic Kernel agent for audit and debugging.
/// </summary>
public class AgentTrace : BaseEntity
{
    public Guid SessionId { get; set; }
    public string? UserId { get; set; }
    
    /// <summary>
    /// JSON-serialized orchestrator execution plan.
    /// </summary>
    public string? OrchestratorPlan { get; set; }
    
    public string AgentName { get; set; } = string.Empty;
    public string? AgentInput { get; set; }
    public string? AgentOutput { get; set; }
    public int TokensUsed { get; set; }
    public long LatencyMs { get; set; }
}
