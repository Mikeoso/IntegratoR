using System.Xml;
using FluentResults;
using IntegratoR.Abstractions.Common.Results;
using Microsoft.Extensions.Logging;

namespace IntegratoR.OData.Common.Services;

/// <summary>
/// Provides D365 F&amp;O OData metadata from a local XML file rather than fetching it over HTTP.
/// </summary>
[Obsolete("since v1.4.0; unused — D365 $metadata is not loaded at runtime; removed next MAJOR")]
public class ODataMetadataProvider
{
    private readonly ILogger<ODataMetadataProvider> _logger;
    private string? _cachedMetadata;

    /// <summary>
    /// Initializes a new instance of the <see cref="ODataMetadataProvider"/> class.
    /// </summary>
    /// <param name="logger">The logger for diagnostic output.</param>
    public ODataMetadataProvider(ILogger<ODataMetadataProvider> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Loads and sanitises the metadata XML from the specified file path, stripping DTD declarations.
    /// </summary>
    /// <param name="metadataFilePath">The relative or absolute path to the metadata XML file.</param>
    /// <returns>A successful <see cref="Result{T}"/> carrying the DTD-free metadata XML, or a failure carrying an <see cref="IntegrationError"/> when the file is missing or cannot be parsed.</returns>
    public Result<string> LoadMetadata(string metadataFilePath)
    {
        if (_cachedMetadata != null)
        {
            _logger.LogDebug("Returning cached metadata.");
            return Result.Ok(_cachedMetadata);
        }

        var resolvedPath = ResolveMetadataPath(metadataFilePath);

        if (!File.Exists(resolvedPath))
        {
            var error = new IntegrationError(
                "ODataMetadata.FileNotFound",
                $"Metadata file not found. Searched paths:\n" +
                $"  - Configured: {metadataFilePath}\n" +
                $"  - Resolved: {resolvedPath}\n" +
                $"  - Current Directory: {Directory.GetCurrentDirectory()}\n" +
                $"  - AppContext.BaseDirectory: {AppContext.BaseDirectory}",
                ErrorType.NotFound);

            _logger.LogError("Metadata file not found at resolved path: {ResolvedPath}", resolvedPath);
            return Result.Fail<string>(error);
        }

        _logger.LogInformation("Loading OData metadata from: {MetadataFilePath}", resolvedPath);

        try
        {
            var xmlContent = File.ReadAllText(resolvedPath);

            // Remove DTD declarations to prevent security issues
            xmlContent = RemoveDtdDeclaration(xmlContent);

            // Validate that it's proper XML
            var validationResult = ValidateXml(xmlContent);
            if (validationResult.IsFailed)
            {
                return Result.Fail<string>(validationResult.Errors);
            }

            _cachedMetadata = xmlContent;
            _logger.LogInformation("Successfully loaded and cached metadata ({Size} bytes)", xmlContent.Length);

            return Result.Ok(_cachedMetadata);
        }
        catch (Exception ex)
        {
            var error = new IntegrationError(
                "ODataMetadata.LoadFailed",
                $"Failed to load metadata from file: {ex.Message}",
                ErrorType.Failure,
                ex);

            _logger.LogError(ex, "Failed to load metadata from file: {MetadataFilePath}", resolvedPath);
            return Result.Fail<string>(error);
        }
    }

    /// <summary>
    /// Resolves the metadata file path, trying multiple locations.
    /// </summary>
    private string ResolveMetadataPath(string configuredPath)
    {
        if (Path.IsPathRooted(configuredPath))
        {
            _logger.LogDebug("Using absolute path: {Path}", configuredPath);
            return configuredPath;
        }

        // Try multiple base directories in order of likelihood
        var basePaths = new[]
        {
            // 1. Application base directory (bin/Debug/net10.0/ for Functions)
            AppContext.BaseDirectory,
            
            // 2. Current working directory
            Directory.GetCurrentDirectory(),
            
            // 3. Parent of base directory (sometimes needed for published apps)
            Path.GetDirectoryName(AppContext.BaseDirectory),
        };

        foreach (var basePath in basePaths.Where(p => !string.IsNullOrEmpty(p)))
        {
            var fullPath = Path.Combine(basePath!, configuredPath);

            if (File.Exists(fullPath))
            {
                _logger.LogDebug("Resolved metadata path: {BasePath} + {ConfiguredPath} = {FullPath}",
                    basePath, configuredPath, fullPath);
                return fullPath;
            }
        }

        // If not found, return path based on base directory for clearer error message
        var fallbackPath = Path.Combine(AppContext.BaseDirectory, configuredPath);
        _logger.LogWarning(
            "Metadata file not found in any standard location. Returning fallback path: {Path}",
            fallbackPath);

        return fallbackPath;
    }

    /// <summary>
    /// Removes DOCTYPE/DTD declarations from XML to prevent security issues.
    /// </summary>
    private string RemoveDtdDeclaration(string xmlContent)
    {
        // Remove DOCTYPE declarations
        // Pattern matches: <!DOCTYPE ... [ ... ]> or <!DOCTYPE ...>
        var dtdPattern = @"<!DOCTYPE[^>\[]*(\[[^\]]*\])?>";
        xmlContent = System.Text.RegularExpressions.Regex.Replace(
            xmlContent,
            dtdPattern,
            string.Empty,
            System.Text.RegularExpressions.RegexOptions.Singleline);

        _logger.LogDebug("Removed DTD declarations from metadata XML");
        return xmlContent;
    }

    /// <summary>
    /// Validates that the string is well-formed XML.
    /// </summary>
    /// <returns>
    /// A <see cref="Result"/> indicating success if the XML is valid,
    /// or a failure with details if validation fails.
    /// </returns>
    private Result ValidateXml(string xmlContent)
    {
        try
        {
            var settings = new XmlReaderSettings
            {
                // Parse (not Prohibit) because D365 F&O metadata may contain residual DTD
                // artifacts after stripping. XmlResolver = null prevents external entity resolution.
                DtdProcessing = DtdProcessing.Parse,
                XmlResolver = null
            };

            using var stringReader = new StringReader(xmlContent);
            using var xmlReader = XmlReader.Create(stringReader, settings);

            while (xmlReader.Read()) { }

            _logger.LogDebug("Metadata XML validation successful");
            return Result.Ok();
        }
        catch (Exception ex)
        {
            var error = new IntegrationError(
                "ODataMetadata.ValidationFailed",
                $"XML validation failed: {ex.Message}",
                ErrorType.Validation,
                ex);

            _logger.LogError(ex, "XML validation failed");
            return Result.Fail(error);
        }
    }

    /// <summary>
    /// Clears the cached metadata, forcing a reload on the next access.
    /// </summary>
    public void ClearCache()
    {
        _cachedMetadata = null;
        _logger.LogInformation("Metadata cache cleared");
    }
}
