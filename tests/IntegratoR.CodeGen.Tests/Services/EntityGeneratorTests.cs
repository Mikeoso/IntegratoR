using FluentAssertions;
using IntegratoR.CodeGen.Models;
using IntegratoR.CodeGen.Services;
using Xunit;

namespace IntegratoR.CodeGen.Tests.Services;

public class EntityGeneratorTests
{
    private readonly EntityGenerator _sut = new("TestNamespace");

    [Fact]
    public void Generate_EntityWithCompositeKey_GeneratesGetCompositeKey()
    {
        var schema = CreateSchemaWithEntity(new ODataEntityModel
        {
            Name = "TestEntity",
            EntitySetName = "TestEntities",
            KeyPropertyNames = ["dataAreaId", "Code"],
            Properties =
            [
                new ODataPropertyModel { Name = "dataAreaId", EdmType = "Edm.String", IsNullable = false, IsKey = true },
                new ODataPropertyModel { Name = "Code", EdmType = "Edm.String", IsNullable = false, IsKey = true },
                new ODataPropertyModel { Name = "Description", EdmType = "Edm.String", IsNullable = true }
            ]
        });

        string output = GenerateSingleEntity(schema, "TestEntities");

        output.Should().Contain("public override object[] GetCompositeKey() => [DataAreaId, Code];");
    }

    [Fact]
    public void Generate_PropertyWithAllowEditFalse_EmitsODataFieldAttribute()
    {
        var schema = CreateSchemaWithEntity(new ODataEntityModel
        {
            Name = "TestEntity",
            EntitySetName = "TestEntities",
            KeyPropertyNames = ["Id"],
            Properties =
            [
                new ODataPropertyModel { Name = "Id", EdmType = "Edm.String", IsNullable = false, IsKey = true },
                new ODataPropertyModel { Name = "ReadOnlyField", EdmType = "Edm.String", AllowEdit = false }
            ]
        });

        string output = GenerateSingleEntity(schema, "TestEntities");

        output.Should().Contain("[ODataField(AllowEdit = false,");
    }

    [Fact]
    public void Generate_RequiredProperty_EmitsRequiredKeyword()
    {
        var schema = CreateSchemaWithEntity(new ODataEntityModel
        {
            Name = "TestEntity",
            EntitySetName = "TestEntities",
            KeyPropertyNames = ["Id"],
            Properties =
            [
                new ODataPropertyModel { Name = "Id", EdmType = "Edm.String", IsNullable = false, IsKey = true },
                new ODataPropertyModel { Name = "Name", EdmType = "Edm.String", IsRequired = true }
            ]
        });

        string output = GenerateSingleEntity(schema, "TestEntities");

        output.Should().Contain("public required string Name { get; set; }");
    }

    [Fact]
    public void Generate_NullableProperty_EmitsNullableType()
    {
        var schema = CreateSchemaWithEntity(new ODataEntityModel
        {
            Name = "TestEntity",
            EntitySetName = "TestEntities",
            KeyPropertyNames = ["Id"],
            Properties =
            [
                new ODataPropertyModel { Name = "Id", EdmType = "Edm.String", IsNullable = false, IsKey = true },
                new ODataPropertyModel { Name = "Optional", EdmType = "Edm.String", IsNullable = true }
            ]
        });

        string output = GenerateSingleEntity(schema, "TestEntities");

        output.Should().Contain("public string? Optional { get; set; }");
    }

    [Fact]
    public void Generate_EnumProperty_UsesEnumTypeName()
    {
        var schema = CreateSchemaWithEntity(
            new ODataEntityModel
            {
                Name = "TestEntity",
                EntitySetName = "TestEntities",
                KeyPropertyNames = ["Id"],
                Properties =
                [
                    new ODataPropertyModel { Name = "Id", EdmType = "Edm.String", IsNullable = false, IsKey = true },
                    new ODataPropertyModel { Name = "Status", EdmType = "Microsoft.Dynamics.DataEntities.NoYes", IsNullable = true, IsEnum = true, EnumTypeName = "NoYes" }
                ]
            },
            [new ODataEnumModel { Name = "NoYes", Members = [new ODataEnumMember { Name = "No", Value = 0 }, new ODataEnumMember { Name = "Yes", Value = 1 }] }]);

        string output = GenerateSingleEntity(schema, "TestEntities");

        output.Should().Contain("public NoYes? Status { get; set; }");
    }

    [Fact]
    public void Generate_TableAttribute_UsesEntitySetName()
    {
        var schema = CreateSchemaWithEntity(new ODataEntityModel
        {
            Name = "MyEntity",
            EntitySetName = "MyEntities",
            KeyPropertyNames = ["Id"],
            Properties = [new ODataPropertyModel { Name = "Id", EdmType = "Edm.String", IsNullable = false, IsKey = true }]
        });

        string output = GenerateSingleEntity(schema, "MyEntities");

        output.Should().Contain("[Table(\"MyEntities\")]");
    }

    [Fact]
    public void Generate_PartialClass_EmitsPartialKeyword()
    {
        var schema = CreateSchemaWithEntity(new ODataEntityModel
        {
            Name = "MyEntity",
            EntitySetName = "MyEntities",
            KeyPropertyNames = ["Id"],
            Properties = [new ODataPropertyModel { Name = "Id", EdmType = "Edm.String", IsNullable = false, IsKey = true }]
        });

        string output = GenerateSingleEntity(schema, "MyEntities");

        output.Should().Contain("public partial class MyEntity : BaseEntity<string>");
    }

    [Fact]
    public void Generate_WithFilter_OnlyGeneratesMatchingEntities()
    {
        var schema = new CsdlSchema
        {
            Namespace = "Test",
            EntityTypes =
            [
                new ODataEntityModel { Name = "A", EntitySetName = "As", KeyPropertyNames = ["Id"], Properties = [new ODataPropertyModel { Name = "Id", EdmType = "Edm.String", IsNullable = false, IsKey = true }] },
                new ODataEntityModel { Name = "B", EntitySetName = "Bs", KeyPropertyNames = ["Id"], Properties = [new ODataPropertyModel { Name = "Id", EdmType = "Edm.String", IsNullable = false, IsKey = true }] }
            ],
            EnumTypes = [],
            EntitySetMapping = new Dictionary<string, string> { ["As"] = "A", ["Bs"] = "B" }
        };

        string outputDir = Path.Combine(Path.GetTempPath(), "codegen-test-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            int count = _sut.Generate(schema, outputDir, new HashSet<string>(["As"], StringComparer.OrdinalIgnoreCase));

            count.Should().Be(1);
            File.Exists(Path.Combine(outputDir, "A.g.cs")).Should().BeTrue();
            File.Exists(Path.Combine(outputDir, "B.g.cs")).Should().BeFalse();
        }
        finally
        {
            if (Directory.Exists(outputDir)) Directory.Delete(outputDir, true);
        }
    }

    [Fact]
    public void ToPascalCase_LowercaseFirst_ConvertsToUppercase()
    {
        EntityGenerator.ToPascalCase("dataAreaId").Should().Be("DataAreaId");
    }

    [Fact]
    public void ToPascalCase_AlreadyPascal_ReturnsUnchanged()
    {
        EntityGenerator.ToPascalCase("JournalBatchNumber").Should().Be("JournalBatchNumber");
    }

    [Fact]
    public void Generate_JsonPropertyName_UsesOriginalODataName()
    {
        var schema = CreateSchemaWithEntity(new ODataEntityModel
        {
            Name = "TestEntity",
            EntitySetName = "TestEntities",
            KeyPropertyNames = ["dataAreaId"],
            Properties = [new ODataPropertyModel { Name = "dataAreaId", EdmType = "Edm.String", IsNullable = false, IsKey = true }]
        });

        string output = GenerateSingleEntity(schema, "TestEntities");

        output.Should().Contain("[JsonPropertyName(\"dataAreaId\")]");
        output.Should().Contain("public required string DataAreaId { get; set; }");
    }

    private static CsdlSchema CreateSchemaWithEntity(ODataEntityModel entity, IReadOnlyList<ODataEnumModel>? enums = null)
    {
        return new CsdlSchema
        {
            Namespace = "Test",
            EntityTypes = [entity],
            EnumTypes = enums ?? [],
            EntitySetMapping = entity.EntitySetName is not null
                ? new Dictionary<string, string> { [entity.EntitySetName] = entity.Name }
                : new Dictionary<string, string>()
        };
    }

    private string GenerateSingleEntity(CsdlSchema schema, string entitySetName)
    {
        string outputDir = Path.Combine(Path.GetTempPath(), "codegen-test-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            _sut.Generate(schema, outputDir, new HashSet<string>([entitySetName], StringComparer.OrdinalIgnoreCase));
            string entityName = schema.EntityTypes.First(e => e.EntitySetName == entitySetName).Name;
            return File.ReadAllText(Path.Combine(outputDir, $"{entityName}.g.cs"));
        }
        finally
        {
            if (Directory.Exists(outputDir)) Directory.Delete(outputDir, true);
        }
    }
}
