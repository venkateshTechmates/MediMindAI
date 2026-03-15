using MediMind.Core.Agents;
using MediMind.Core.Interfaces;
using MediMind.Core.Plugins;
using MediMind.Core.RAG;
using Microsoft.Extensions.DependencyInjection;

namespace MediMind.Core;

/// <summary>
/// Extension methods to register all Core services (agents, plugins, RAG pipeline) in DI.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddCore(this IServiceCollection services)
    {
        // ── RAG Pipeline ──
        services.AddScoped<IQueryEmbedder, QueryEmbedder>();
        services.AddSingleton<IReranker, Reranker>();
        services.AddSingleton<IContextBuilder, ContextBuilder>();
        services.AddScoped<IRagPipeline, RagOrchestrator>();

        // ── SK Plugins ──
        services.AddScoped<DrugInteractionPlugin>();
        services.AddScoped<ClinicalGuidelinePlugin>();
        services.AddScoped<PatientRecordPlugin>();
        services.AddScoped<LabResultPlugin>();

        // ── Specialist Agents ──
        services.AddScoped<DrugAgent>();
        services.AddScoped<DiagnosisAgent>();
        services.AddScoped<EhrAgent>();
        services.AddScoped<LabAgent>();
        services.AddScoped<DischargeAgent>();

        // ── Orchestrator ──
        services.AddScoped<OrchestratorAgent>();
        services.AddScoped<IAgentOrchestrator, OrchestratorAgent>();

        return services;
    }
}
