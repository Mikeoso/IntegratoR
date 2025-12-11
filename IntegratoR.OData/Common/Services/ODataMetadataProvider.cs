using Microsoft.Extensions.Logging;
using System.Xml;

namespace IntegratoR.OData.Common.Services;

/// <summary>
/// Provides D365 F&O metadata from a local XML file instead of fetching it via HTTP.
/// This approach offers several advantages:
/// - Avoids DTD processing security issues
/// - Faster application startup (no metadata download)
/// - Enables offline development
/// - Metadata changes are version-controlled
/// </summary>
public class ODataMetadataProvider
{
    private readonly ILogger<ODataMetadataProvider> _logger;
    private string? _cachedMetadata;

    public ODataMetadataProvider(ILogger<ODataMetadataProvider> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Loads and sanitizes the metadata XML from the specified file path.
    /// Removes DTD declarations to prevent security issues.
    /// </summary>
    /// <param name="metadataFilePath">Relative or absolute path to the metadata.xml file</param>
    /// <returns>Clean metadata XML string without DTD declarations</returns>
    public string LoadMetadata(string metadataFilePath)
    {
        if (_cachedMetadata != null)
        {
            _logger.LogDebug("Returning cached metadata.");
            return _cachedMetadata;
        }

        // Resolve the full path
        var resolvedPath = ResolveMetadataPath(metadataFilePath);

        if (!File.Exists(resolvedPath))
        {
            throw new FileNotFoundException(
                $"Metadata file not found. Searched paths:\n" +
                $"  - Configured: {metadataFilePath}\n" +
                $"  - Resolved: {resolvedPath}\n" +
                $"  - Current Directory: {Directory.GetCurrentDirectory()}\n" +
                $"  - AppContext.BaseDirectory: {AppContext.BaseDirectory}",
                resolvedPath);
        }

        _logger.LogInformation("Loading OData metadata from: {MetadataFilePath}", resolvedPath);

        try
        {
            // Read the file content
            var xmlContent = File.ReadAllText(resolvedPath);

            // Remove DTD declarations to prevent security issues
            xmlContent = RemoveDtdDeclaration(xmlContent);

            // Validate that it's proper XML
            ValidateXml(xmlContent);

            _cachedMetadata = xmlContent;
            _logger.LogInformation("Successfully loaded and cached metadata ({Size} bytes)", xmlContent.Length);

            return _cachedMetadata;
        }
        catch (Exception ex) when (ex is not FileNotFoundException)
        {
            _logger.LogError(ex, "Failed to load metadata from file: {MetadataFilePath}", resolvedPath);
            throw;
        }
    }

    /// <summary>
    /// Resolves the metadata file path, trying multiple locations.
    /// </summary>
    private string ResolveMetadataPath(string configuredPath)
    {
        // If absolute path, use directly
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
    /// Removes DOCTYPE/DTD declarations from XML to prevent security issues
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
    /// Validates that the string is well-formed XML
    /// </summary>
    private void ValidateXml(string xmlContent)
    {
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit, // Extra safety
            XmlResolver = null // No external entity resolution
        };

        using var stringReader = new StringReader(xmlContent);
        using var xmlReader = XmlReader.Create(stringReader, settings);

        // Just parse through it to validate
        while (xmlReader.Read()) { }

        _logger.LogDebug("Metadata XML validation successful");
    }

    /// <summary>
    /// Clears the cached metadata, forcing a reload on next access.
    /// Useful for development or when metadata file is updated.
    /// </summary>
    public void ClearCache()
    {
        _cachedMetadata = null;
        _logger.LogInformation("Metadata cache cleared");
    }
}