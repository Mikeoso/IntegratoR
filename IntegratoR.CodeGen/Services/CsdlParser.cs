using System.Xml;
using System.Xml.Linq;
using IntegratoR.CodeGen.Models;

namespace IntegratoR.CodeGen.Services;

/// <summary>
/// Parses OData CSDL (Common Schema Definition Language) XML from D365 F&amp;O $metadata
/// into structured models for code generation.
/// </summary>
public static class CsdlParser
{
    private static readonly XNamespace Edm = "http://docs.oasis-open.org/odata/ns/edm";
    private static readonly XNamespace Edmx = "http://docs.oasis-open.org/odata/ns/edmx";

    private const string D365AnnotationPrefix = "Microsoft.Dynamics.OData.Core.V1.";

    private static readonly XmlReaderSettings SafeXmlSettings = new()
    {
        DtdProcessing = DtdProcessing.Ignore,
        XmlResolver = null
    };

    /// <summary>
    /// Loads a CSDL XML file and parses it into a <see cref="CsdlSchema"/>.
    /// </summary>
    /// <exception cref="System.IO.IOException">The file cannot be read.</exception>
    /// <exception cref="XmlException">The file is not well-formed XML.</exception>
    /// <exception cref="InvalidOperationException">The document contains no Schema element.</exception>
    public static CsdlSchema LoadAndParse(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        using XmlReader reader = XmlReader.Create(stream, SafeXmlSettings);
        XDocument doc = XDocument.Load(reader);
        return ParseDocument(doc);
    }

    /// <summary>
    /// Parses a CSDL XML string into a <see cref="CsdlSchema"/>.
    /// </summary>
    /// <remarks>Uses secure XML settings that ignore DTD declarations to prevent XXE attacks.</remarks>
    /// <exception cref="XmlException">The string is not well-formed XML.</exception>
    /// <exception cref="InvalidOperationException">The document contains no Schema element.</exception>
    public static CsdlSchema Parse(string xml)
    {
        using var stringReader = new StringReader(xml);
        using XmlReader reader = XmlReader.Create(stringReader, SafeXmlSettings);
        XDocument doc = XDocument.Load(reader);
        return ParseDocument(doc);
    }

    private static CsdlSchema ParseDocument(XDocument doc)
    {
        XElement schema = doc.Descendants(Edm + "Schema").FirstOrDefault()
            ?? throw new InvalidOperationException(
                "CSDL document contains no Schema element. Verify the metadata file is a valid OData $metadata response.");
        string schemaNamespace = schema.Attribute("Namespace")?.Value ?? "";

        var enumTypes = ParseEnumTypes(schema);
        var enumTypeNames = new HashSet<string>(enumTypes.Select(e => e.Name));
        var entitySetMapping = ParseEntitySets(schema, schemaNamespace);
        var entityTypes = ParseEntityTypes(schema, schemaNamespace, entitySetMapping, enumTypeNames);

        return new CsdlSchema
        {
            Namespace = schemaNamespace,
            EntityTypes = entityTypes,
            EnumTypes = enumTypes,
            EntitySetMapping = entitySetMapping
        };
    }

    private static IReadOnlyList<ODataEntityModel> ParseEntityTypes(
        XElement schema,
        string schemaNamespace,
        IReadOnlyDictionary<string, string> entitySetMapping,
        HashSet<string> enumTypeNames)
    {
        // Reverse mapping: entity type name → entity set name
        var typeToEntitySet = entitySetMapping
            .ToDictionary(kvp => kvp.Value, kvp => kvp.Key, StringComparer.OrdinalIgnoreCase);

        var entities = new List<ODataEntityModel>();

        foreach (XElement entityTypeElement in schema.Elements(Edm + "EntityType"))
        {
            string typeName = entityTypeElement.Attribute("Name")?.Value ?? "";
            if (string.IsNullOrEmpty(typeName)) continue;

            // Parse key property names
            var keyNames = entityTypeElement
                .Element(Edm + "Key")?
                .Elements(Edm + "PropertyRef")
                .Select(p => p.Attribute("Name")?.Value ?? "")
                .Where(n => !string.IsNullOrEmpty(n))
                .ToList() ?? [];

            var keySet = new HashSet<string>(keyNames, StringComparer.Ordinal);

            // Parse properties
            var properties = entityTypeElement
                .Elements(Edm + "Property")
                .Select(p => ParseProperty(p, keySet, schemaNamespace, enumTypeNames))
                .ToList();

            // Entity-level annotations
            bool isReadOnly = GetAnnotationBool(entityTypeElement, "IsReadOnly");
            string? label = GetAnnotationString(entityTypeElement, "LabelId");

            typeToEntitySet.TryGetValue(typeName, out string? entitySetName);

            entities.Add(new ODataEntityModel
            {
                Name = typeName,
                EntitySetName = entitySetName,
                IsReadOnly = isReadOnly,
                Label = label,
                Properties = properties,
                KeyPropertyNames = keyNames
            });
        }

        return entities;
    }

    private static ODataPropertyModel ParseProperty(
        XElement propertyElement,
        HashSet<string> keyPropertyNames,
        string schemaNamespace,
        HashSet<string> enumTypeNames)
    {
        string name = propertyElement.Attribute("Name")?.Value ?? "";
        string edmType = propertyElement.Attribute("Type")?.Value ?? "Edm.String";
        bool isNullable = propertyElement.Attribute("Nullable")?.Value != "false";
        bool isKey = keyPropertyNames.Contains(name);

        // Determine if type is an enum (non-Edm. prefix, in schema namespace)
        bool isEnum = false;
        string? enumTypeName = null;
        if (!edmType.StartsWith("Edm.", StringComparison.Ordinal))
        {
            string shortName = edmType.StartsWith(schemaNamespace + ".", StringComparison.Ordinal)
                ? edmType[(schemaNamespace.Length + 1)..]
                : edmType;

            if (enumTypeNames.Contains(shortName))
            {
                isEnum = true;
                enumTypeName = shortName;
            }
        }

        // D365 annotations
        bool allowEdit = GetAnnotationBool(propertyElement, "AllowEdit", defaultValue: true);
        bool allowEditOnCreate = GetAnnotationBool(propertyElement, "AllowEditOnCreate", defaultValue: true);
        bool isRequired = GetAnnotationBool(propertyElement, "IsRequired");
        string? label = GetAnnotationString(propertyElement, "LabelId");

        return new ODataPropertyModel
        {
            Name = name,
            EdmType = edmType,
            IsNullable = isNullable,
            IsKey = isKey,
            AllowEdit = allowEdit,
            AllowEditOnCreate = allowEditOnCreate,
            IsRequired = isRequired,
            Label = label,
            IsEnum = isEnum,
            EnumTypeName = enumTypeName
        };
    }

    private static IReadOnlyList<ODataEnumModel> ParseEnumTypes(XElement schema)
    {
        var enums = new List<ODataEnumModel>();

        foreach (XElement enumElement in schema.Elements(Edm + "EnumType"))
        {
            string name = enumElement.Attribute("Name")?.Value ?? "";
            if (string.IsNullOrEmpty(name)) continue;

            var members = enumElement
                .Elements(Edm + "Member")
                .Select(m => new ODataEnumMember
                {
                    Name = m.Attribute("Name")?.Value ?? "",
                    Value = int.TryParse(m.Attribute("Value")?.Value, out int v) ? v : 0,
                    Label = GetAnnotationString(m, "LabelId")
                })
                .Where(m => !string.IsNullOrEmpty(m.Name))
                .ToList();

            string? label = GetAnnotationString(enumElement, "LabelId");

            enums.Add(new ODataEnumModel
            {
                Name = name,
                Members = members,
                Label = label
            });
        }

        return enums;
    }

    private static IReadOnlyDictionary<string, string> ParseEntitySets(XElement schema, string schemaNamespace)
    {
        var mapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // EntityContainer is within the same Schema element
        XElement? container = schema.Element(Edm + "EntityContainer");
        if (container is null) return mapping;

        foreach (XElement entitySet in container.Elements(Edm + "EntitySet"))
        {
            string setName = entitySet.Attribute("Name")?.Value ?? "";
            string entityType = entitySet.Attribute("EntityType")?.Value ?? "";

            if (string.IsNullOrEmpty(setName) || string.IsNullOrEmpty(entityType)) continue;

            // Strip namespace prefix: "Microsoft.Dynamics.DataEntities.LedgerJournalHeader" → "LedgerJournalHeader"
            string typeName = entityType.StartsWith(schemaNamespace + ".", StringComparison.Ordinal)
                ? entityType[(schemaNamespace.Length + 1)..]
                : entityType;

            mapping[setName] = typeName;
        }

        return mapping;
    }

    private static bool GetAnnotationBool(XElement element, string annotationName, bool defaultValue = false)
    {
        XElement? annotation = element
            .Elements(Edm + "Annotation")
            .FirstOrDefault(a => a.Attribute("Term")?.Value == D365AnnotationPrefix + annotationName);

        if (annotation is null) return defaultValue;

        string? value = annotation.Attribute("Bool")?.Value;
        return value is not null && bool.TryParse(value, out bool result) ? result : defaultValue;
    }

    private static string? GetAnnotationString(XElement element, string annotationName)
    {
        XElement? annotation = element
            .Elements(Edm + "Annotation")
            .FirstOrDefault(a => a.Attribute("Term")?.Value == D365AnnotationPrefix + annotationName);

        return annotation?.Attribute("String")?.Value;
    }
}
