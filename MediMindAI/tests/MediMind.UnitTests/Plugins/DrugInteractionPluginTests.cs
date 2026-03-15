using FluentAssertions;
using MediMind.Core.Interfaces;
using MediMind.Core.Plugins;
using Microsoft.Extensions.Logging;
using Moq;

namespace MediMind.UnitTests.Plugins;

public class DrugInteractionPluginTests
{
    private readonly Mock<IVectorStore> _vectorStore = new();
    private readonly Mock<ILLMClient> _llmClient = new();
    private readonly Mock<ILogger<DrugInteractionPlugin>> _logger = new();
    private readonly DrugInteractionPlugin _plugin;

    public DrugInteractionPluginTests()
    {
        _plugin = new DrugInteractionPlugin(_vectorStore.Object, _llmClient.Object, _logger.Object);
    }

    [Fact]
    public async Task CheckInteractions_WithKnownDrugs_ShouldReturnInteractionInfo()
    {
        // Arrange
        var drugs = "metformin, lisinopril";

        _llmClient.Setup(l => l.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LLMResponse { Content = "Metformin and lisinopril have no significant interactions." });

        // Act
        var result = await _plugin.CheckInteractionsAsync(drugs);

        // Assert
        result.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task LookupDosage_ShouldReturnDosageInfo()
    {
        // Arrange
        var drugName = "metformin";

        _llmClient.Setup(l => l.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LLMResponse { Content = "Metformin dosage: 500-2000mg daily." });

        // Act
        var result = await _plugin.LookupDosageAsync(drugName);

        // Assert
        result.Should().NotBeNullOrEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CheckInteractions_WithEmptyInput_ShouldHandleGracefully(string drugs)
    {
        // Act
        var result = await _plugin.CheckInteractionsAsync(drugs);

        // Assert
        result.Should().NotBeNull();
    }
}
