using IntegratoR.CodeGen.Services;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using MSBuildTask = Microsoft.Build.Utilities.Task;

namespace IntegratoR.CodeGen.Tasks;

/// <summary>
/// MSBuild task that generates C# entity classes from D365 F&amp;O OData $metadata CSDL.
/// </summary>
public class GenerateODataEntitiesTask : MSBuildTask
{
    /// <summary>
    /// Path to the local metadata.xml file.
    /// </summary>
    [Required]
    public string MetadataSource { get; set; } = "";

    /// <summary>
    /// Directory where generated .g.cs files will be written.
    /// </summary>
    [Required]
    public string OutputPath { get; set; } = "";

    /// <summary>
    /// The C# namespace for generated entity classes.
    /// </summary>
    [Required]
    public string Namespace { get; set; } = "";

    /// <summary>
    /// Semicolon-separated list of entity set names or entity type names to generate.
    /// If empty, all entity types with an entity set mapping are generated.
    /// </summary>
    public string Entities { get; set; } = "";

    public override bool Execute()
    {
        try
        {
            if (!File.Exists(MetadataSource))
            {
                Log.LogError("Metadata file not found: {0}", MetadataSource);
                return false;
            }

            var schema = CsdlParser.LoadAndParse(MetadataSource);

            Log.LogMessage(MessageImportance.Normal,
                "Parsed {0} entity types, {1} enum types from {2}",
                schema.EntityTypes.Count, schema.EnumTypes.Count, MetadataSource);

            HashSet<string>? filter = !string.IsNullOrWhiteSpace(Entities)
                ? new HashSet<string>(
                    Entities.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                    StringComparer.OrdinalIgnoreCase)
                : null;

            var generator = new EntityGenerator(Namespace);
            int count = generator.Generate(schema, OutputPath, filter);

            Log.LogMessage(MessageImportance.High,
                "Generated {0} entity files in {1}", count, OutputPath);

            return true;
        }
        catch (Exception ex)
        {
            Log.LogErrorFromException(ex, showStackTrace: true);
            return false;
        }
    }
}
