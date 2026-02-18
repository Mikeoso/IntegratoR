using System.Net;
using System.Text;
using FluentAssertions;
using IntegratoR.RELion.Common.Services;
using IntegratoR.RELion.Domain.DTOs;
using IntegratoR.RELion.Domain.Models;
using IntegratoR.RELion.Domain.Settings;
using IntegratoR.TestKit.Assertions;
using IntegratoR.TestKit.Fakes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using NSubstitute;
using Xunit;

namespace IntegratoR.RELion.Tests.Common.Services;

/// <summary>
/// Tests for <see cref="RelionService"/> covering company lookup, ledger account mapping,
/// journal line pagination, and Base64/date-format verification.
/// </summary>
public class RelionServiceTests
{
    private const string BaseUrl = "https://relion.test";
    private const string CompanyName = "TestCo";

    private static (RelionService Sut, FakeHttpMessageHandler FakeHandler) CreateSut(string company = CompanyName)
    {
        var fakeHandler = new FakeHttpMessageHandler();
        var httpClient = new HttpClient(fakeHandler) { BaseAddress = new Uri(BaseUrl) };
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("RelionApiClient").Returns(httpClient);
        var settings = Options.Create(new RelionSettings
        {
            Url = BaseUrl,
            Company = company
        });
        var logger = Substitute.For<ILogger<RelionService>>();
        var sut = new RelionService(factory, logger, settings);
        return (sut, fakeHandler);
    }

    private static string BuildCompanyJson(string id = "company-123", string name = CompanyName)
    {
        var wrapper = new { value = new[] { new { id, name, displayName = name } } };
        return JsonConvert.SerializeObject(wrapper);
    }

    private static string BuildPageJson<T>(IEnumerable<T> items, bool moreRows)
    {
        var innerData = new { Data = items.ToArray() };
        var innerJson = JsonConvert.SerializeObject(innerData);
        var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(innerJson));
        var responseEntity = new RelionResponseEntity
        {
            EncodedResponseJson = base64,
            MoreRows = moreRows
        };
        var payload = new RelionResponsePayload
        {
            EntitySet = new List<RelionResponseEntity> { responseEntity }
        };
        return JsonConvert.SerializeObject(payload);
    }

    private static RelionLedgerJournalLine CreateTestJournalLine(int entryNo = 1) =>
        new()
        {
            EntryNo = entryNo,
            AccountNum = "1000",
            PostingDate = DateTimeOffset.UtcNow,
            DocumentNo = "DOC001",
            Description = "Test",
            ICPartnerCode = string.Empty,
            ShortcutDimensionCode = string.Empty,
            MovementType = string.Empty,
            RelObjectNum = string.Empty,
            RelCompetenceUnit = string.Empty
        };

    private static RelionLedgerAccountMapping CreateTestMapping() =>
        new()
        {
            LedgerAccountNo = "GL100",
            TaxAccountNo = "VAT100"
        };

    #region GetCompanyByNameAsync Tests

    /// <summary>
    /// Verifies that a successful company lookup returns the matching company.
    /// </summary>
    [Fact]
    public async Task GetCompanyByNameAsync_CompanyFound_ReturnsCompany()
    {
        // Arrange
        var (sut, fakeHandler) = CreateSut();
        fakeHandler.Queue(HttpStatusCode.OK, BuildCompanyJson());

        // Act
        var result = await sut.GetCompanyByNameAsync(CompanyName, CancellationToken.None);

        // Assert
        result.Should().BeSuccessful();
        result.Value.Id.Should().Be("company-123");
        result.Value.Name.Should().Be(CompanyName);
    }

    /// <summary>
    /// Verifies that when no company matches the name, a CompanyNotFound error is returned.
    /// </summary>
    [Fact]
    public async Task GetCompanyByNameAsync_CompanyNotFound_ReturnsNotFoundError()
    {
        // Arrange
        var (sut, fakeHandler) = CreateSut();
        var json = JsonConvert.SerializeObject(new { value = new[] { new { id = "other-id", name = "OtherCompany", displayName = "Other" } } });
        fakeHandler.Queue(HttpStatusCode.OK, json);

        // Act
        var result = await sut.GetCompanyByNameAsync(CompanyName, CancellationToken.None);

        // Assert
        result.Should().BeFailed().And.HaveErrorCode("Relion.CompanyNotFound");
    }

    /// <summary>
    /// Verifies that company name matching is case-insensitive.
    /// </summary>
    [Fact]
    public async Task GetCompanyByNameAsync_CaseInsensitiveMatch_FindsCompany()
    {
        // Arrange
        var (sut, fakeHandler) = CreateSut();
        fakeHandler.Queue(HttpStatusCode.OK, BuildCompanyJson(name: "TESTCO"));

        // Act
        var result = await sut.GetCompanyByNameAsync("testco", CancellationToken.None);

        // Assert
        result.Should().BeSuccessful();
        result.Value.Name.Should().Be("TESTCO");
    }

    /// <summary>
    /// Verifies that a non-successful HTTP response returns a Relion.ApiError.
    /// </summary>
    [Fact]
    public async Task GetCompanyByNameAsync_ApiError_ReturnsFailure()
    {
        // Arrange
        var (sut, fakeHandler) = CreateSut();
        fakeHandler.Queue(HttpStatusCode.InternalServerError, "Server error");

        // Act
        var result = await sut.GetCompanyByNameAsync(CompanyName, CancellationToken.None);

        // Assert
        result.Should().BeFailed().And.HaveErrorCode("Relion.ApiError");
    }

    /// <summary>
    /// Verifies that an exception during the HTTP call returns a Relion.Exception error.
    /// </summary>
    [Fact]
    public async Task GetCompanyByNameAsync_Exception_ReturnsFailureWithException()
    {
        // Arrange
        var fakeHandler = new FakeHttpMessageHandler();
        // Do not queue any response -- calling will throw InvalidOperationException
        var httpClient = new HttpClient(fakeHandler) { BaseAddress = new Uri(BaseUrl) };
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("RelionApiClient").Returns(httpClient);
        var settings = Options.Create(new RelionSettings { Url = BaseUrl, Company = CompanyName });
        var logger = Substitute.For<ILogger<RelionService>>();
        var sut = new RelionService(factory, logger, settings);

        // Act
        var result = await sut.GetCompanyByNameAsync(CompanyName, CancellationToken.None);

        // Assert
        result.Should().BeFailed().And.HaveErrorCode("Relion.Exception");
    }

    #endregion

    #region GetLedgerAccountMappingsAsync Tests

    /// <summary>
    /// Verifies that a successful mapping lookup returns the mapping data.
    /// </summary>
    [Fact]
    public async Task GetLedgerAccountMappingsAsync_MappingFound_ReturnsMapping()
    {
        // Arrange
        var (sut, fakeHandler) = CreateSut();
        fakeHandler.Queue(HttpStatusCode.OK, BuildCompanyJson());
        fakeHandler.Queue(HttpStatusCode.OK, BuildPageJson(new[] { CreateTestMapping() }, moreRows: false));

        // Act
        var result = await sut.GetLedgerAccountMappingsAsync(1, CancellationToken.None);

        // Assert
        result.Should().BeSuccessful();
        result.Value.LedgerAccountNo.Should().Be("GL100");
        result.Value.TaxAccountNo.Should().Be("VAT100");
    }

    /// <summary>
    /// Verifies that when no mapping data is returned, an empty mapping object is returned.
    /// </summary>
    [Fact]
    public async Task GetLedgerAccountMappingsAsync_NoMapping_ReturnsEmptyMapping()
    {
        // Arrange
        var (sut, fakeHandler) = CreateSut();
        fakeHandler.Queue(HttpStatusCode.OK, BuildCompanyJson());
        // Return payload with no data entity containing encoded JSON
        var emptyPayload = new RelionResponsePayload
        {
            EntitySet = new List<RelionResponseEntity>
            {
                new RelionResponseEntity { MoreRows = false, EncodedResponseJson = null }
            }
        };
        fakeHandler.Queue(HttpStatusCode.OK, JsonConvert.SerializeObject(emptyPayload));

        // Act
        var result = await sut.GetLedgerAccountMappingsAsync(1, CancellationToken.None);

        // Assert
        result.Should().BeSuccessful();
        result.Value.LedgerAccountNo.Should().Be(string.Empty);
        result.Value.TaxAccountNo.Should().Be(string.Empty);
    }

    /// <summary>
    /// Verifies that when the company is not found, a CompanyNotFound error is propagated.
    /// </summary>
    [Fact]
    public async Task GetLedgerAccountMappingsAsync_CompanyNotFound_ReturnsError()
    {
        // Arrange
        var (sut, fakeHandler) = CreateSut();
        var json = JsonConvert.SerializeObject(new { value = Array.Empty<object>() });
        fakeHandler.Queue(HttpStatusCode.OK, json);

        // Act
        var result = await sut.GetLedgerAccountMappingsAsync(1, CancellationToken.None);

        // Assert
        result.Should().BeFailed();
        result.Errors.Should().NotBeEmpty();
    }

    /// <summary>
    /// Verifies that when the POST query fails, the error is propagated.
    /// </summary>
    [Fact]
    public async Task GetLedgerAccountMappingsAsync_QueryFails_ReturnsError()
    {
        // Arrange
        var (sut, fakeHandler) = CreateSut();
        fakeHandler.Queue(HttpStatusCode.OK, BuildCompanyJson());
        fakeHandler.Queue(HttpStatusCode.InternalServerError, "Internal Server Error");

        // Act
        var result = await sut.GetLedgerAccountMappingsAsync(1, CancellationToken.None);

        // Assert
        result.Should().BeFailed();
    }

    #endregion

    #region GetNewJournalLinesAsync Tests

    /// <summary>
    /// Verifies that a single-page response returns all journal lines.
    /// </summary>
    [Fact]
    public async Task GetNewJournalLinesAsync_SinglePage_ReturnsAllLines()
    {
        // Arrange
        var (sut, fakeHandler) = CreateSut();
        var line = CreateTestJournalLine(1);
        fakeHandler.Queue(HttpStatusCode.OK, BuildCompanyJson());
        fakeHandler.Queue(HttpStatusCode.OK, BuildPageJson(new[] { line }, moreRows: false));

        // Act
        var result = await sut.GetNewJournalLinesAsync(DateTime.UtcNow.AddDays(-1), CancellationToken.None);

        // Assert
        result.Should().BeSuccessful();
        result.Value.Should().HaveCount(1);
        result.Value[0].EntryNo.Should().Be(1);
    }

    /// <summary>
    /// Verifies that multiple pages are fetched and aggregated into a single result list.
    /// </summary>
    [Fact]
    public async Task GetNewJournalLinesAsync_MultiplePages_PaginatesAndAggregates()
    {
        // Arrange
        var (sut, fakeHandler) = CreateSut();
        var line1 = CreateTestJournalLine(1);
        var line2 = CreateTestJournalLine(2);
        fakeHandler.Queue(HttpStatusCode.OK, BuildCompanyJson());
        fakeHandler.Queue(HttpStatusCode.OK, BuildPageJson(new[] { line1 }, moreRows: true));
        fakeHandler.Queue(HttpStatusCode.OK, BuildPageJson(new[] { line2 }, moreRows: false));

        // Act
        var result = await sut.GetNewJournalLinesAsync(DateTime.UtcNow.AddDays(-1), CancellationToken.None);

        // Assert
        result.Should().BeSuccessful();
        result.Value.Should().HaveCount(2);
        result.Value.Should().Contain(l => l.EntryNo == 1);
        result.Value.Should().Contain(l => l.EntryNo == 2);
    }

    /// <summary>
    /// Verifies that a response with no data returns an empty list.
    /// </summary>
    [Fact]
    public async Task GetNewJournalLinesAsync_EmptyResponse_ReturnsEmptyList()
    {
        // Arrange
        var (sut, fakeHandler) = CreateSut();
        fakeHandler.Queue(HttpStatusCode.OK, BuildCompanyJson());
        var emptyPayload = new RelionResponsePayload
        {
            EntitySet = new List<RelionResponseEntity>
            {
                new RelionResponseEntity { MoreRows = false, EncodedResponseJson = null }
            }
        };
        fakeHandler.Queue(HttpStatusCode.OK, JsonConvert.SerializeObject(emptyPayload));

        // Act
        var result = await sut.GetNewJournalLinesAsync(DateTime.UtcNow.AddDays(-1), CancellationToken.None);

        // Assert
        result.Should().BeSuccessful();
        result.Value.Should().BeEmpty();
    }

    /// <summary>
    /// Verifies that when the company lookup fails, the error is propagated and no page queries are made.
    /// </summary>
    [Fact]
    public async Task GetNewJournalLinesAsync_CompanyNotFound_ReturnsError()
    {
        // Arrange
        var (sut, fakeHandler) = CreateSut();
        var noCompanies = JsonConvert.SerializeObject(new { value = Array.Empty<object>() });
        fakeHandler.Queue(HttpStatusCode.OK, noCompanies);

        // Act
        var result = await sut.GetNewJournalLinesAsync(DateTime.UtcNow.AddDays(-1), CancellationToken.None);

        // Assert
        result.Should().BeFailed();
    }

    /// <summary>
    /// Verifies that when the page POST request fails, the error is propagated.
    /// </summary>
    [Fact]
    public async Task GetNewJournalLinesAsync_PageQueryFails_ReturnsError()
    {
        // Arrange
        var (sut, fakeHandler) = CreateSut();
        fakeHandler.Queue(HttpStatusCode.OK, BuildCompanyJson());
        fakeHandler.Queue(HttpStatusCode.InternalServerError, "Error");

        // Act
        var result = await sut.GetNewJournalLinesAsync(DateTime.UtcNow.AddDays(-1), CancellationToken.None);

        // Assert
        result.Should().BeFailed().And.HaveErrorCode("Relion.ApiError");
    }

    #endregion

    #region QueryAsync Private Method Tests (via public methods)

    /// <summary>
    /// Verifies that a valid Base64-encoded response is decoded and deserialized correctly.
    /// </summary>
    [Fact]
    public async Task QueryAsync_ValidResponse_DecodesBase64AndDeserializes()
    {
        // Arrange
        var (sut, fakeHandler) = CreateSut();
        var line = CreateTestJournalLine(42);
        fakeHandler.Queue(HttpStatusCode.OK, BuildCompanyJson());
        fakeHandler.Queue(HttpStatusCode.OK, BuildPageJson(new[] { line }, moreRows: false));

        // Act
        var result = await sut.GetNewJournalLinesAsync(DateTime.UtcNow.AddDays(-1), CancellationToken.None);

        // Assert
        result.Should().BeSuccessful();
        result.Value.Should().HaveCount(1);
        result.Value[0].EntryNo.Should().Be(42);
    }

    /// <summary>
    /// Verifies that when EncodedResponseJson is null or empty, an empty list is returned.
    /// </summary>
    [Fact]
    public async Task QueryAsync_NullEncodedResponseJson_ReturnsEmptyList()
    {
        // Arrange
        var (sut, fakeHandler) = CreateSut();
        fakeHandler.Queue(HttpStatusCode.OK, BuildCompanyJson());
        var payloadWithNullJson = new RelionResponsePayload
        {
            EntitySet = new List<RelionResponseEntity>
            {
                new RelionResponseEntity { MoreRows = false, EncodedResponseJson = null }
            }
        };
        fakeHandler.Queue(HttpStatusCode.OK, JsonConvert.SerializeObject(payloadWithNullJson));

        // Act
        var result = await sut.GetNewJournalLinesAsync(DateTime.UtcNow.AddDays(-1), CancellationToken.None);

        // Assert
        result.Should().BeSuccessful();
        result.Value.Should().BeEmpty();
    }

    /// <summary>
    /// Verifies that the date filter in the POST request body uses ISO 8601 format.
    /// </summary>
    [Fact]
    public async Task QueryAsync_DateFilterFormat_UsesIso8601()
    {
        // Arrange
        var (sut, fakeHandler) = CreateSut();
        var since = new DateTime(2024, 6, 15, 12, 30, 0, DateTimeKind.Utc);
        fakeHandler.Queue(HttpStatusCode.OK, BuildCompanyJson());
        fakeHandler.Queue(HttpStatusCode.OK, BuildPageJson(Array.Empty<RelionLedgerJournalLine>(), moreRows: false));

        // Act
        await sut.GetNewJournalLinesAsync(since, CancellationToken.None);

        // Assert
        fakeHandler.SentRequests.Should().HaveCount(2);
        var postRequest = fakeHandler.SentRequests[1];
        var body = await postRequest.Content!.ReadAsStringAsync(CancellationToken.None);
        // ISO 8601 format: yyyy-MM-ddTHH:mm:ss.fffffffK
        body.Should().Contain("2024-06-15");
        body.Should().Contain("12:30:00");
    }

    #endregion
}
