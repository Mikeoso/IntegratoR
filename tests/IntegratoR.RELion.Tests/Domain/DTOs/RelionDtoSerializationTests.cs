using FluentAssertions;
using IntegratoR.RELion.Domain.DTOs;
using IntegratoR.RELion.Domain.Models;
using Newtonsoft.Json;
using System.Text;
using Xunit;

namespace IntegratoR.RELion.Tests.Domain.DTOs;

/// <summary>
/// Tests verifying Newtonsoft.Json serialization round-trips for all RELion DTO and model types.
/// </summary>
public class RelionDtoSerializationTests
{
    /// <summary>
    /// Verifies that RelionDataWrapper deserializes a JSON object with a "Data" array correctly.
    /// </summary>
    [Fact]
    public void RelionDataWrapper_Deserialize_ParsesDataArray()
    {
        // Arrange
        var json = """{"Data": [{"Entry No.": 1, "G/L Account No.": "1000", "Posting Date": "2024-01-01T00:00:00+00:00", "Document No.": "DOC001", "Description": "Test", "IC Partner Code": "", "Shortcut Dimension 8 Code": "", "Shortcut Dimension 4 Code": "", "RelC Object No.": "", "RelC Competence Unit": ""}]}""";

        // Act
        var result = JsonConvert.DeserializeObject<RelionDataWrapper<RelionLedgerJournalLine>>(json);

        // Assert
        result.Should().NotBeNull();
        result!.Data.Should().HaveCount(1);
        result.Data[0].EntryNo.Should().Be(1);
        result.Data[0].AccountNum.Should().Be("1000");
    }

    /// <summary>
    /// Verifies that RelionCompanyDataWrapper deserializes a JSON object with a "value" array correctly.
    /// </summary>
    [Fact]
    public void RelionCompanyDataWrapper_Deserialize_ParsesValueArray()
    {
        // Arrange
        var json = """{"value": [{"id": "company-1", "name": "Test Company", "displayName": "Test Company Display"}]}""";

        // Act
        var result = JsonConvert.DeserializeObject<RelionCompanyDataWrapper<RelionCompany>>(json);

        // Assert
        result.Should().NotBeNull();
        result!.Data.Should().HaveCount(1);
        result.Data[0].Id.Should().Be("company-1");
        result.Data[0].Name.Should().Be("Test Company");
    }

    /// <summary>
    /// Verifies that RelionRequest serializes using the expected Newtonsoft.Json property names.
    /// </summary>
    [Fact]
    public void RelionRequest_Serialize_UsesNewtonsoftPropertyNames()
    {
        // Arrange
        var request = new RelionRequest
        {
            TableNumber = "17",
            Top = 500,
            Skip = 10,
            EntitySet = new List<RelionRequestFilter>()
        };

        // Act
        var json = JsonConvert.SerializeObject(request);

        // Assert
        json.Should().Contain("\"tableNo\"");
        json.Should().Contain("\"operation\"");
        json.Should().Contain("\"entitySet\"");
        json.Should().Contain("\"top\"");
        json.Should().Contain("\"skip\"");
    }

    /// <summary>
    /// Verifies that RelionRequestFilter serializes excluding null-valued properties
    /// when DefaultValueHandling.Ignore is applied.
    /// </summary>
    [Fact]
    public void RelionRequestFilter_Serialize_NullValueHandling_ExcludesNulls()
    {
        // Arrange
        var filter = new RelionRequestFilter
        {
            SubOperation = "DONE",
            ResponseFields = "1|2|3"
            // FieldNumber, Filter, Value are null
        };
        var settings = new JsonSerializerSettings { DefaultValueHandling = DefaultValueHandling.Ignore };

        // Act
        var json = JsonConvert.SerializeObject(filter, settings);

        // Assert
        json.Should().NotContain("\"fieldNo\"");
        json.Should().NotContain("\"filter\"");
        json.Should().NotContain("\"value\"");
        json.Should().Contain("\"subOperation\"");
        json.Should().Contain("\"responseFields\"");
    }

    /// <summary>
    /// Verifies that RelionResponseEntity deserializes all fields correctly from JSON property names.
    /// </summary>
    [Fact]
    public void RelionResponseEntity_Deserialize_ParsesAllFields()
    {
        // Arrange
        var json = """{"moreRows": true, "encodedResponseJson": "dGVzdA==", "subOperation": "DONE", "numberOfRows": 5, "skippedRows": 0}""";

        // Act
        var result = JsonConvert.DeserializeObject<RelionResponseEntity>(json);

        // Assert
        result.Should().NotBeNull();
        result!.MoreRows.Should().BeTrue();
        result.NumberOfRows.Should().Be(5);
        result.SkippedRows.Should().Be(0);
    }

    /// <summary>
    /// Verifies that RelionResponsePayload deserializes a JSON object with an "entitySet" array correctly.
    /// </summary>
    [Fact]
    public void RelionResponsePayload_Deserialize_ParsesEntitySet()
    {
        // Arrange
        var json = """{"entitySet": [{"moreRows": false, "numberOfRows": 1, "skippedRows": 0, "ResponseJson2": "dGVzdA=="}]}""";

        // Act
        var result = JsonConvert.DeserializeObject<RelionResponsePayload>(json);

        // Assert
        result.Should().NotBeNull();
        result!.EntitySet.Should().HaveCount(1);
        result.EntitySet[0].MoreRows.Should().BeFalse();
        result.EntitySet[0].EncodedResponseJson.Should().Be("dGVzdA==");
    }

    /// <summary>
    /// Verifies that RelionResponseEntity.EncodedResponseJson stores a Base64 string that can be decoded.
    /// </summary>
    [Fact]
    public void RelionResponseEntity_EncodedResponseJson_CanBeBase64Decoded()
    {
        // Arrange
        var originalContent = """{"Data": []}""";
        var base64Encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(originalContent));
        var entity = new RelionResponseEntity { EncodedResponseJson = base64Encoded };

        // Act
        var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(entity.EncodedResponseJson!));

        // Assert
        decoded.Should().Be(originalContent);
    }

    /// <summary>
    /// Verifies that RelionLedgerJournalLine deserializes from Newtonsoft.Json property names correctly.
    /// </summary>
    [Fact]
    public void RelionLedgerJournalLine_Deserialize_MapsNewtonJsonProperties()
    {
        // Arrange
        var json = """
        {
            "Entry No.": 100,
            "G/L Account No.": "2000",
            "Posting Date": "2024-06-15T00:00:00+00:00",
            "Document No.": "DOC100",
            "Description": "Test Line",
            "VAT Amount": 25.50,
            "IC Partner Code": "PARTNER1",
            "Shortcut Dimension 8 Code": "DIM8",
            "Shortcut Dimension 4 Code": "DIM4",
            "RelC Object No.": "OBJ1",
            "RelC Competence Unit": "UNIT1"
        }
        """;

        // Act
        var result = JsonConvert.DeserializeObject<RelionLedgerJournalLine>(json);

        // Assert
        result.Should().NotBeNull();
        result!.EntryNo.Should().Be(100);
        result.AccountNum.Should().Be("2000");
        result.DocumentNo.Should().Be("DOC100");
        result.Description.Should().Be("Test Line");
        result.VatAmount.Should().Be(25.50m);
        result.ICPartnerCode.Should().Be("PARTNER1");
        result.ShortcutDimensionCode.Should().Be("DIM8");
        result.MovementType.Should().Be("DIM4");
        result.RelObjectNum.Should().Be("OBJ1");
        result.RelCompetenceUnit.Should().Be("UNIT1");
    }
}
