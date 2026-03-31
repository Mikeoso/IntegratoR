using FluentAssertions;
using IntegratoR.CodeGen.Services;
using Xunit;

namespace IntegratoR.CodeGen.Tests.Services;

public class CsdlParserTests
{
    private const string SampleCsdl = """
        <?xml version="1.0" encoding="utf-8"?>
        <edmx:Edmx Version="4.0" xmlns:edmx="http://docs.oasis-open.org/odata/ns/edmx">
            <edmx:DataServices>
                <Schema Namespace="Microsoft.Dynamics.DataEntities" xmlns="http://docs.oasis-open.org/odata/ns/edm">
                    <EnumType Name="NoYes">
                        <Member Name="No" Value="0">
                            <Annotation Term="Microsoft.Dynamics.OData.Core.V1.LabelId" String="@sys2048" />
                        </Member>
                        <Member Name="Yes" Value="1">
                            <Annotation Term="Microsoft.Dynamics.OData.Core.V1.LabelId" String="@sys5461" />
                        </Member>
                    </EnumType>
                    <EntityType Name="TestJournalHeader">
                        <Key>
                            <PropertyRef Name="dataAreaId" />
                            <PropertyRef Name="JournalBatchNumber" />
                        </Key>
                        <Property Name="dataAreaId" Type="Edm.String" Nullable="false">
                            <Annotation Term="Microsoft.Dynamics.OData.Core.V1.IsRequired" Bool="true" />
                            <Annotation Term="Microsoft.Dynamics.OData.Core.V1.LabelId" String="@SYS13342" />
                        </Property>
                        <Property Name="JournalBatchNumber" Type="Edm.String" Nullable="false">
                            <Annotation Term="Microsoft.Dynamics.OData.Core.V1.AllowEdit" Bool="false" />
                            <Annotation Term="Microsoft.Dynamics.OData.Core.V1.AllowEditOnCreate" Bool="false" />
                        </Property>
                        <Property Name="Description" Type="Edm.String" />
                        <Property Name="Amount" Type="Edm.Decimal" Nullable="false" />
                        <Property Name="IsPosted" Type="Microsoft.Dynamics.DataEntities.NoYes">
                            <Annotation Term="Microsoft.Dynamics.OData.Core.V1.AllowEdit" Bool="false" />
                            <Annotation Term="Microsoft.Dynamics.OData.Core.V1.AllowEditOnCreate" Bool="false" />
                        </Property>
                        <Annotation Term="Microsoft.Dynamics.OData.Core.V1.LabelId" String="@SYS129344" />
                    </EntityType>
                    <EntityType Name="ReadOnlyEntity">
                        <Key>
                            <PropertyRef Name="Id" />
                        </Key>
                        <Property Name="Id" Type="Edm.String" Nullable="false" />
                        <Annotation Term="Microsoft.Dynamics.OData.Core.V1.IsReadOnly" Bool="true" />
                    </EntityType>
                    <EntityContainer Name="Resources">
                        <EntitySet Name="TestJournalHeaders" EntityType="Microsoft.Dynamics.DataEntities.TestJournalHeader" />
                        <EntitySet Name="ReadOnlyEntities" EntityType="Microsoft.Dynamics.DataEntities.ReadOnlyEntity" />
                    </EntityContainer>
                </Schema>
            </edmx:DataServices>
        </edmx:Edmx>
        """;

    [Fact]
    public void Parse_ValidCsdl_ReturnsCorrectNamespace()
    {
        var schema = CsdlParser.Parse(SampleCsdl);

        schema.Namespace.Should().Be("Microsoft.Dynamics.DataEntities");
    }

    [Fact]
    public void Parse_ValidCsdl_ParsesEntityTypes()
    {
        var schema = CsdlParser.Parse(SampleCsdl);

        schema.EntityTypes.Should().HaveCount(2);
        schema.EntityTypes.Should().Contain(e => e.Name == "TestJournalHeader");
        schema.EntityTypes.Should().Contain(e => e.Name == "ReadOnlyEntity");
    }

    [Fact]
    public void Parse_EntityWithCompositeKey_ParsesKeyProperties()
    {
        var schema = CsdlParser.Parse(SampleCsdl);
        var entity = schema.EntityTypes.First(e => e.Name == "TestJournalHeader");

        entity.KeyPropertyNames.Should().BeEquivalentTo(["dataAreaId", "JournalBatchNumber"]);
    }

    [Fact]
    public void Parse_EntityProperties_MapsEdmTypes()
    {
        var schema = CsdlParser.Parse(SampleCsdl);
        var entity = schema.EntityTypes.First(e => e.Name == "TestJournalHeader");

        entity.Properties.Should().Contain(p => p.Name == "dataAreaId" && p.EdmType == "Edm.String" && !p.IsNullable);
        entity.Properties.Should().Contain(p => p.Name == "Amount" && p.EdmType == "Edm.Decimal" && !p.IsNullable);
        entity.Properties.Should().Contain(p => p.Name == "Description" && p.EdmType == "Edm.String" && p.IsNullable);
    }

    [Fact]
    public void Parse_D365Annotations_ExtractsAllowEdit()
    {
        var schema = CsdlParser.Parse(SampleCsdl);
        var entity = schema.EntityTypes.First(e => e.Name == "TestJournalHeader");
        var batchNumber = entity.Properties.First(p => p.Name == "JournalBatchNumber");

        batchNumber.AllowEdit.Should().BeFalse();
        batchNumber.AllowEditOnCreate.Should().BeFalse();
    }

    [Fact]
    public void Parse_D365Annotations_ExtractsIsRequired()
    {
        var schema = CsdlParser.Parse(SampleCsdl);
        var entity = schema.EntityTypes.First(e => e.Name == "TestJournalHeader");
        var dataAreaId = entity.Properties.First(p => p.Name == "dataAreaId");

        dataAreaId.IsRequired.Should().BeTrue();
        dataAreaId.Label.Should().Be("@SYS13342");
    }

    [Fact]
    public void Parse_EnumProperty_DetectsEnumType()
    {
        var schema = CsdlParser.Parse(SampleCsdl);
        var entity = schema.EntityTypes.First(e => e.Name == "TestJournalHeader");
        var isPosted = entity.Properties.First(p => p.Name == "IsPosted");

        isPosted.IsEnum.Should().BeTrue();
        isPosted.EnumTypeName.Should().Be("NoYes");
    }

    [Fact]
    public void Parse_EnumTypes_ParsesMembersWithValues()
    {
        var schema = CsdlParser.Parse(SampleCsdl);
        var noYes = schema.EnumTypes.First(e => e.Name == "NoYes");

        noYes.Members.Should().HaveCount(2);
        noYes.Members.Should().Contain(m => m.Name == "No" && m.Value == 0);
        noYes.Members.Should().Contain(m => m.Name == "Yes" && m.Value == 1);
    }

    [Fact]
    public void Parse_EntitySets_MapsEntitySetToTypeName()
    {
        var schema = CsdlParser.Parse(SampleCsdl);

        schema.EntitySetMapping.Should().ContainKey("TestJournalHeaders");
        schema.EntitySetMapping["TestJournalHeaders"].Should().Be("TestJournalHeader");
    }

    [Fact]
    public void Parse_EntityWithEntitySet_ResolvesEntitySetName()
    {
        var schema = CsdlParser.Parse(SampleCsdl);
        var entity = schema.EntityTypes.First(e => e.Name == "TestJournalHeader");

        entity.EntitySetName.Should().Be("TestJournalHeaders");
    }

    [Fact]
    public void Parse_ReadOnlyEntity_SetsIsReadOnly()
    {
        var schema = CsdlParser.Parse(SampleCsdl);
        var entity = schema.EntityTypes.First(e => e.Name == "ReadOnlyEntity");

        entity.IsReadOnly.Should().BeTrue();
    }

    [Fact]
    public void Parse_DefaultAnnotationValues_DefaultToTrue()
    {
        var schema = CsdlParser.Parse(SampleCsdl);
        var entity = schema.EntityTypes.First(e => e.Name == "TestJournalHeader");
        var description = entity.Properties.First(p => p.Name == "Description");

        description.AllowEdit.Should().BeTrue();
        description.AllowEditOnCreate.Should().BeTrue();
        description.IsRequired.Should().BeFalse();
    }
}
