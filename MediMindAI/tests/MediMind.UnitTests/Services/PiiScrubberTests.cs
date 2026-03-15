using FluentAssertions;
using MediMind.Infrastructure.PiiScrubbing;
using Microsoft.Extensions.Logging.Abstractions;

namespace MediMind.UnitTests.Services;

public class PiiScrubberTests
{
    private readonly RegexPiiScrubber _scrubber = new(NullLogger<RegexPiiScrubber>.Instance);

    [Fact]
    public async Task ScrubAsync_WithSSN_ShouldRedact()
    {
        var input = "Patient SSN is 123-45-6789";
        var result = await _scrubber.ScrubAsync(input);

        result.EntitiesDetected.Should().BeGreaterThan(0);
        result.ScrubedText.Should().NotContain("123-45-6789");
        result.DetectedEntities.Should().Contain(e => e.Type == "SSN");
    }

    [Fact]
    public async Task ScrubAsync_WithEmail_ShouldRedact()
    {
        var input = "Contact: john.doe@hospital.com";
        var result = await _scrubber.ScrubAsync(input);

        result.EntitiesDetected.Should().BeGreaterThan(0);
        result.ScrubedText.Should().NotContain("john.doe@hospital.com");
        result.DetectedEntities.Should().Contain(e => e.Type == "Email");
    }

    [Fact]
    public async Task ScrubAsync_WithPhoneNumber_ShouldRedact()
    {
        var input = "Call me at (555) 123-4567";
        var result = await _scrubber.ScrubAsync(input);

        result.EntitiesDetected.Should().BeGreaterThan(0);
        result.ScrubedText.Should().NotContain("(555) 123-4567");
        result.DetectedEntities.Should().Contain(e => e.Type == "Phone");
    }

    [Fact]
    public async Task ScrubAsync_WithNoPII_ShouldNotModify()
    {
        var input = "What are the side effects of aspirin?";
        var result = await _scrubber.ScrubAsync(input);

        result.EntitiesDetected.Should().Be(0);
        result.ScrubedText.Should().Be(input);
        result.DetectedEntities.Should().BeEmpty();
    }

    [Fact]
    public async Task ScrubAsync_WithMultiplePII_ShouldRedactAll()
    {
        var input = "Patient john.doe@test.com, SSN 123-45-6789, phone (555) 111-2222";
        var result = await _scrubber.ScrubAsync(input);

        result.EntitiesDetected.Should().BeGreaterThanOrEqualTo(3);
        result.ScrubedText.Should().NotContain("123-45-6789");
        result.ScrubedText.Should().NotContain("john.doe@test.com");
    }

    [Fact]
    public async Task ScrubAsync_WithMRN_ShouldRedact()
    {
        var input = "Patient MRN: MRN-12345678";
        var result = await _scrubber.ScrubAsync(input);

        result.EntitiesDetected.Should().BeGreaterThan(0);
        result.DetectedEntities.Should().Contain(e => e.Type == "MRN");
    }
}
