using IntegratoR.CodeGen.Services;

if (args.Length == 0)
{
    Console.Error.WriteLine("Usage: integrator-codegen <metadata-file> <output-dir> <namespace> [entity1,entity2,...]");
    return 1;
}

string metadataPath = args[0];
string outputDir = args.Length > 1 ? args[1] : "Generated";
string targetNamespace = args.Length > 2 ? args[2] : "Generated.Entities";
string[]? entityFilter = args.Length > 3 ? args[3].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) : null;

if (!File.Exists(metadataPath))
{
    Console.Error.WriteLine($"Metadata file not found: {metadataPath}");
    return 1;
}

var schema = CsdlParser.LoadAndParse(metadataPath);

Console.WriteLine($"Parsed {schema.EntityTypes.Count} entity types, {schema.EnumTypes.Count} enum types, {schema.EntitySetMapping.Count} entity sets");

HashSet<string>? filter = entityFilter is not null ? new HashSet<string>(entityFilter, StringComparer.OrdinalIgnoreCase) : null;

var generator = new EntityGenerator(targetNamespace);
int generated = generator.Generate(schema, outputDir, filter);

Console.WriteLine($"Generated {generated} files in {outputDir}");
return 0;
