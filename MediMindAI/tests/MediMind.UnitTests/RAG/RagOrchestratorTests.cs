using FluentAssertions;
using MediMind.Core.Interfaces;
using MediMind.Core.Models;
using MediMind.Core.RAG;
using Microsoft.Extensions.Logging;
using Moq;

namespace MediMind.UnitTests.RAG;

public class RagOrchestratorTests
{
    private readonly Mock<IQueryEmbedder> _embedder = new();
    private readonly Mock<IVectorStore> _vectorStore = new();
    private readonly Mock<IReranker> _reranker = new();
    private readonly Mock<IContextBuilder> _contextBuilder = new();
    private readonly Mock<ILogger<RagOrchestrator>> _logger = new();
    private readonly RagOrchestrator _orchestrator;

    public RagOrchestratorTests()
    {
        _orchestrator = new RagOrchestrator(
            _embedder.Object, _vectorStore.Object,
            _reranker.Object, _contextBuilder.Object,
            _logger.Object);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnRagContext()
    {
        // Arrange
        var query = new ClinicalQuery
        {
            QueryText = "What is the treatment for hypertension?",
            SessionId = Guid.NewGuid(),
            UserId = "test-user"
        };

        var embedding = new float[1024];
        var chunks = new List<DocumentChunk>
        {
            new() { Content = "Hypertension treatment includes ACE inhibitors.", DocumentName = "Guidelines", Metadata = new DocumentChunkMetadata { Source = "Guidelines", Category = "Cardiology" } },
            new() { Content = "Lifestyle modifications help control blood pressure.", DocumentName = "Research", Metadata = new DocumentChunkMetadata { Source = "Research", Category = "Cardiology" } }
        };

        _embedder.Setup(e => e.EmbedQueryAsync(query.QueryText, It.IsAny<CancellationToken>()))
            .ReturnsAsync(embedding);

        _vectorStore.Setup(v => v.SearchAsync(It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<Dictionary<string, string>?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(chunks);

        _reranker.Setup(r => r.RerankAsync(query.QueryText, It.IsAny<IReadOnlyList<DocumentChunk>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(chunks);

        _contextBuilder.Setup(c => c.BuildContext(It.IsAny<IReadOnlyList<DocumentChunk>>(), It.IsAny<string?>()))
            .Returns("Combined context text.");

        // Act
        var result = await _orchestrator.ExecuteAsync(query);

        // Assert
        result.Should().NotBeNull();
        result.AugmentedContext.Should().NotBeNullOrEmpty();
        result.RetrievedChunks.Should().HaveCount(2);
    }

    [Fact]
    public async Task ExecuteAsync_WithNoResults_ShouldReturnEmptyContext()
    {
        // Arrange
        var query = new ClinicalQuery
        {
            QueryText = "Some obscure query",
            SessionId = Guid.NewGuid(),
            UserId = "test-user"
        };
        var embedding = new float[1024];

        _embedder.Setup(e => e.EmbedQueryAsync(query.QueryText, It.IsAny<CancellationToken>()))
            .ReturnsAsync(embedding);

        _vectorStore.Setup(v => v.SearchAsync(It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<Dictionary<string, string>?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DocumentChunk>());

        _reranker.Setup(r => r.RerankAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<DocumentChunk>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DocumentChunk>());

        _contextBuilder.Setup(c => c.BuildContext(It.IsAny<IReadOnlyList<DocumentChunk>>(), It.IsAny<string?>()))
            .Returns(string.Empty);

        // Act
        var result = await _orchestrator.ExecuteAsync(query);

        // Assert
        result.Should().NotBeNull();
        result.RetrievedChunks.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldCallEmbedderWithQueryText()
    {
        // Arrange
        var query = new ClinicalQuery
        {
            QueryText = "test query",
            SessionId = Guid.NewGuid(),
            UserId = "test-user"
        };
        var embedding = new float[1024];

        _embedder.Setup(e => e.EmbedQueryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(embedding);

        _vectorStore.Setup(v => v.SearchAsync(It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<Dictionary<string, string>?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DocumentChunk>());

        _reranker.Setup(r => r.RerankAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<DocumentChunk>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DocumentChunk>());

        _contextBuilder.Setup(c => c.BuildContext(It.IsAny<IReadOnlyList<DocumentChunk>>(), It.IsAny<string?>()))
            .Returns(string.Empty);

        // Act
        await _orchestrator.ExecuteAsync(query);

        // Assert
        _embedder.Verify(e => e.EmbedQueryAsync("test query", It.IsAny<CancellationToken>()), Times.Once);
    }
}
