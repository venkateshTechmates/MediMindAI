using MediMind.Core.Interfaces;
using MediMind.Infrastructure.Anthropic;
using MediMind.Infrastructure.Persistence;
using MediMind.Infrastructure.Persistence.Repositories;
using MediMind.Infrastructure.PiiScrubbing;
using MediMind.Infrastructure.Qdrant;
using MediMind.Infrastructure.Redis;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Qdrant.Client;
using StackExchange.Redis;

namespace MediMind.Infrastructure;

/// <summary>
/// Extension methods to register all infrastructure services in DI.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // ── EF Core + SQL Server ──
        services.AddDbContext<MediMindDbContext>((sp, options) =>
        {
            options.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection") ?? configuration.GetConnectionString("SqlServer"),
                sqlOptions =>
                {
                    sqlOptions.MigrationsAssembly(typeof(MediMindDbContext).Assembly.FullName);
                    sqlOptions.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(10), errorNumbersToAdd: null);
                });

            options.AddInterceptors(new AuditableEntityInterceptor());
        });

        // ── Repositories & UoW ──
        services.AddScoped<IPatientRepository, PatientRepository>();
        services.AddScoped<IEncounterRepository, EncounterRepository>();
        services.AddScoped<ILabResultRepository, LabResultRepository>();
        services.AddScoped<IMedicationRepository, MedicationRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // ── Qdrant ──
        var qdrantHost = configuration["Qdrant:Host"] ?? "localhost";
        var qdrantPort = int.Parse(configuration["Qdrant:Port"] ?? "6334");
        services.AddSingleton(new QdrantClient(qdrantHost, qdrantPort));
        services.AddSingleton<IVectorStore, QdrantVectorStore>();
        services.AddSingleton<QdrantCollectionSetup>();
        services.AddSingleton<QdrantHealthCheck>();

        // ── Anthropic / LLM ──
        services.Configure<AnthropicOptions>(configuration.GetSection(AnthropicOptions.SectionName));

        var useMock = configuration.GetValue<bool>("ANTHROPIC_USE_MOCK") ||
                      configuration.GetValue<bool>("Anthropic:UseMock");

        if (useMock)
        {
            services.AddSingleton<ILLMClient, MockAnthropicClient>();
        }
        else
        {
            services.AddHttpClient<ILLMClient, AnthropicClient>();
        }

        // ── Redis (with in-memory fallback) ──
        var redisConnection = configuration.GetConnectionString("Redis") ?? "localhost:6379";
        try
        {
            var redis = ConnectionMultiplexer.Connect(new ConfigurationOptions
            {
                EndPoints = { redisConnection },
                AbortOnConnectFail = false,
                ConnectTimeout = 3000
            });

            if (redis.IsConnected)
            {
                services.AddSingleton<IConnectionMultiplexer>(redis);
                services.AddSingleton<ISessionStore, RedisSessionStore>();
            }
            else
            {
                redis.Dispose();
                services.AddSingleton<ISessionStore, InMemorySessionStore>();
            }
        }
        catch
        {
            // Redis not available — use in-memory store
            services.AddSingleton<ISessionStore, InMemorySessionStore>();
        }

        // ── PII Scrubber ──
        services.AddSingleton<IPiiScrubber, RegexPiiScrubber>();

        // ── Data Seeder ──
        services.AddTransient<DataSeeder>();

        return services;
    }
}
