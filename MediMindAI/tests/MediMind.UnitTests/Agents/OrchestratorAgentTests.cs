using FluentAssertions;
using MediMind.Core.Agents;
using MediMind.Core.Interfaces;
using MediMind.Core.Models;
using Microsoft.Extensions.Logging;
using Moq;

namespace MediMind.UnitTests.Agents;

public class OrchestratorAgentTests
{
    private readonly Mock<DrugAgent> _drugAgent;
    private readonly Mock<DiagnosisAgent> _diagnosisAgent;
    private readonly Mock<EhrAgent> _ehrAgent;
    private readonly Mock<LabAgent> _labAgent;
    private readonly Mock<DischargeAgent> _dischargeAgent;
    private readonly Mock<ILLMClient> _llmClient = new();
    private readonly Mock<IRagPipeline> _ragPipeline = new();
    private readonly Mock<ILogger<OrchestratorAgent>> _logger = new();

    public OrchestratorAgentTests()
    {
        // Create mocks for specialist agents (using Mock behavior Loose so constructors don't matter)
        _drugAgent = new Mock<DrugAgent>(MockBehavior.Loose,
            _llmClient.Object, null!, Mock.Of<ILogger<DrugAgent>>());
        _diagnosisAgent = new Mock<DiagnosisAgent>(MockBehavior.Loose,
            _llmClient.Object, null!, Mock.Of<ILogger<DiagnosisAgent>>());
        _ehrAgent = new Mock<EhrAgent>(MockBehavior.Loose,
            _llmClient.Object, null!, Mock.Of<ILogger<EhrAgent>>());
        _labAgent = new Mock<LabAgent>(MockBehavior.Loose,
            _llmClient.Object, null!, Mock.Of<ILogger<LabAgent>>());
        _dischargeAgent = new Mock<DischargeAgent>(MockBehavior.Loose,
            _llmClient.Object, Mock.Of<IVectorStore>(), Mock.Of<IUnitOfWork>(), Mock.Of<ILogger<DischargeAgent>>());
    }

    private OrchestratorAgent CreateAgent() => new(
        _drugAgent.Object, _diagnosisAgent.Object, _ehrAgent.Object,
        _labAgent.Object, _dischargeAgent.Object,
        _llmClient.Object, _ragPipeline.Object, _logger.Object);

    [Fact]
    public async Task ProcessQueryAsync_ShouldReturnClinicalResponse()
    {
        // Arrange
        var query = new ClinicalQuery
        {
            QueryText = "What are the side effects of metformin?",
            SessionId = Guid.NewGuid(),
            UserId = "test-user"
        };

        var ragContext = new RagContext
        {
            AugmentedContext = "Metformin may cause gastrointestinal side effects.",
            RetrievedChunks = new List<DocumentChunk>
            {
                new() { Content = "Metformin side effects include nausea.", DocumentName = "DrugDB", Metadata = new DocumentChunkMetadata { Source = "DrugDB" } }
            }
        };

        _ragPipeline.Setup(r => r.ExecuteAsync(It.IsAny<ClinicalQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ragContext);

        // Classification call returns JSON routing to "drug" agent
        _llmClient.Setup(l => l.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LLMResponse { Content = """{"agents":["drug"],"is_out_of_scope":false}""" });

        var agent = CreateAgent();

        // Act
        var result = await agent.ProcessQueryAsync(query);

        // Assert
        result.Should().NotBeNull();
        result.Content.Should().NotBeNull();
        result.SessionId.Should().Be(query.SessionId);
    }

    [Fact]
    public async Task ProcessQueryAsync_WithPatientId_ShouldReturnResponse()
    {
        // Arrange
        var patientId = Guid.NewGuid();
        var query = new ClinicalQuery
        {
            QueryText = "Check drug interactions",
            SessionId = Guid.NewGuid(),
            UserId = "test-user",
            PatientId = patientId
        };

        var ragContext = new RagContext
        {
            AugmentedContext = "Drug interaction context.",
            RetrievedChunks = new List<DocumentChunk>()
        };

        _ragPipeline.Setup(r => r.ExecuteAsync(It.IsAny<ClinicalQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ragContext);

        _llmClient.Setup(l => l.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LLMResponse { Content = """{"agents":["drug"],"is_out_of_scope":false}""" });

        var agent = CreateAgent();

        // Act
        var result = await agent.ProcessQueryAsync(query);

        // Assert
        result.Should().NotBeNull();
        result.QueryId.Should().Be(query.QueryId);
    }

    [Fact]
    public async Task StreamQueryAsync_ShouldYieldTokens()
    {
        // Arrange
        var query = new ClinicalQuery
        {
            QueryText = "Explain diabetes management",
            SessionId = Guid.NewGuid(),
            UserId = "test-user"
        };

        var ragContext = new RagContext
        {
            AugmentedContext = "Diabetes management context.",
            RetrievedChunks = new List<DocumentChunk>()
        };

        _ragPipeline.Setup(r => r.ExecuteAsync(It.IsAny<ClinicalQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ragContext);

        var tokens = new[] { "Diabetes ", "management ", "involves ", "lifestyle changes." };
        _llmClient.Setup(l => l.StreamAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable(tokens));

        var agent = CreateAgent();

        // Act
        var receivedTokens = new List<string>();
        await foreach (var token in agent.StreamQueryAsync(query))
        {
            receivedTokens.Add(token);
        }

        // Assert
        receivedTokens.Should().NotBeEmpty();
    }

    private static async IAsyncEnumerable<string> ToAsyncEnumerable(IEnumerable<string> items)
    {
        foreach (var item in items)
        {
            yield return item;
            await Task.CompletedTask;
        }
    }
}
