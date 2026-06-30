using FluentAssertions;
using IntegratoR.Abstractions.Common.Results;
using IntegratoR.OData.Common.Services;
using IntegratoR.TestKit.Assertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace IntegratoR.OData.Tests.Common.Services;

// ODataMetadataProvider is [Obsolete] (unused at runtime, removed next MAJOR). These tests are
// retained for coverage until the type is removed; suppress CS0618 for the type references here.
#pragma warning disable CS0618 // Type or member is obsolete

/// <summary>
/// Tests for <see cref="ODataMetadataProvider"/> using real temp files.
/// </summary>
public class ODataMetadataProviderTests : IDisposable
{
    private readonly ILogger<ODataMetadataProvider> _logger;
    private readonly ODataMetadataProvider _sut;
    private readonly List<string> _tempFiles = new();

    /// <summary>
    /// Initialises a new instance with a mock logger and fresh provider instance.
    /// </summary>
    public ODataMetadataProviderTests()
    {
        _logger = Substitute.For<ILogger<ODataMetadataProvider>>();
        _sut = new ODataMetadataProvider(_logger);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        foreach (var file in _tempFiles)
        {
            if (File.Exists(file))
                File.Delete(file);
        }
    }

    private string CreateTempXmlFile(string xmlContent)
    {
        var path = Path.GetTempFileName();
        _tempFiles.Add(path);
        File.WriteAllText(path, xmlContent);
        return path;
    }

    /// <summary>
    /// Verifies that a valid XML file is loaded and returned successfully.
    /// </summary>
    [Fact]
    public void LoadMetadata_ValidXmlFile_ReturnsSuccessWithContent()
    {
        // Arrange
        const string xmlContent = "<?xml version=\"1.0\" encoding=\"utf-8\"?><root><child>value</child></root>";
        var path = CreateTempXmlFile(xmlContent);

        // Act
        var result = _sut.LoadMetadata(path);

        // Assert
        result.Should().BeSuccessful();
        result.Value.Should().Contain("<root>");
    }

    /// <summary>
    /// Verifies that a non-existent file returns an error with code ODataMetadata.FileNotFound and type NotFound.
    /// </summary>
    [Fact]
    public void LoadMetadata_FileNotFound_ReturnsError()
    {
        // Arrange
        var nonExistentPath = Path.Combine(Path.GetTempPath(), $"nonexistent_{Guid.NewGuid()}.xml");

        // Act
        var result = _sut.LoadMetadata(nonExistentPath);

        // Assert
        result.Should().BeFailed();
        result.Should().HaveErrorCode("ODataMetadata.FileNotFound");
        result.Should().HaveErrorType(ErrorType.NotFound);
    }

    /// <summary>
    /// Verifies that malformed XML content returns an error with code ODataMetadata.ValidationFailed and type Validation.
    /// </summary>
    [Fact]
    public void LoadMetadata_InvalidXml_ReturnsValidationError()
    {
        // Arrange
        var path = CreateTempXmlFile("this is not xml <<< &&&");

        // Act
        var result = _sut.LoadMetadata(path);

        // Assert
        result.Should().BeFailed();
        result.Should().HaveErrorCode("ODataMetadata.ValidationFailed");
        result.Should().HaveErrorType(ErrorType.Validation);
    }

    /// <summary>
    /// Verifies that DTD declarations are removed from the loaded XML content.
    /// </summary>
    [Fact]
    public void LoadMetadata_XmlWithDtd_RemovesDtdDeclarations()
    {
        // Arrange
        const string xmlWithDtd =
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
            "<!DOCTYPE root PUBLIC \"-//Test//EN\" \"http://example.com/test.dtd\">" +
            "<root><child>value</child></root>";
        var path = CreateTempXmlFile(xmlWithDtd);

        // Act
        var result = _sut.LoadMetadata(path);

        // Assert
        result.Should().BeSuccessful();
        result.Value.Should().NotContain("<!DOCTYPE");
    }

    /// <summary>
    /// Verifies that a second call returns the cached result even if the file is deleted.
    /// </summary>
    [Fact]
    public void LoadMetadata_CalledTwice_ReturnsCachedResult()
    {
        // Arrange
        const string xmlContent = "<?xml version=\"1.0\" encoding=\"utf-8\"?><root/>";
        var path = CreateTempXmlFile(xmlContent);

        // Act - first load to populate cache
        var firstResult = _sut.LoadMetadata(path);

        // Delete the temp file before the second call
        File.Delete(path);
        _tempFiles.Remove(path);

        var secondResult = _sut.LoadMetadata(path);

        // Assert
        firstResult.Should().BeSuccessful();
        secondResult.Should().BeSuccessful("the result should come from cache after file deletion");
        secondResult.Value.Should().Be(firstResult.Value);
    }

    /// <summary>
    /// Verifies that clearing the cache forces a reload, which fails if the file was deleted.
    /// </summary>
    [Fact]
    public void ClearCache_AfterLoad_ForcesReloadOnNextAccess()
    {
        // Arrange
        const string xmlContent = "<?xml version=\"1.0\" encoding=\"utf-8\"?><root/>";
        var path = CreateTempXmlFile(xmlContent);

        // Load once to populate cache
        var firstResult = _sut.LoadMetadata(path);
        firstResult.Should().BeSuccessful();

        // Clear cache and delete the file
        _sut.ClearCache();
        File.Delete(path);
        _tempFiles.Remove(path);

        // Act - second load after cache clear should fail since file is gone
        var secondResult = _sut.LoadMetadata(path);

        // Assert
        secondResult.Should().BeFailed("the cache was cleared and the file was deleted");
    }
}

#pragma warning restore CS0618 // Type or member is obsolete
