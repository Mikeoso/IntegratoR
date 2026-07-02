using FluentAssertions;
using IntegratoR.OData.Common.Batch;
using Xunit;

namespace IntegratoR.OData.Tests.Common.Batch;

/// <summary>
/// Tests the hand-rolled multipart <c>$batch</c> emitter and response parser against the OData v4.01
/// multipart shape (OASIS Part 1 §11.7.7).
/// </summary>
public class ODataBatchWireTests
{
    private static readonly BatchWriteOperation Patch = new(
        1, HttpMethod.Patch, "LedgerJournalHeaders(dataAreaId='USMF',JournalBatchNumber='B1')", "{\"Description\":\"x\"}");

    private static readonly BatchWriteOperation Delete = new(
        2, HttpMethod.Delete, "LedgerJournalHeaders(dataAreaId='USMF',JournalBatchNumber='B2')");

    /// <summary>Joins lines with CRLF and appends a trailing CRLF, matching the wire format.</summary>
    private static string Wire(params string[] lines) => string.Join("\r\n", lines) + "\r\n";

    [Fact]
    public void Build_Atomic_WrapsOperationsInASingleChangeset()
    {
        ODataBatchRequestBuilder.BuiltBatchRequest built =
            ODataBatchRequestBuilder.Build([Patch, Delete], atomic: true, "batch1", "cs1");

        built.ContentType.Should().Be("multipart/mixed; boundary=batch1");
        built.Body.Should().Contain("--batch1\r\nContent-Type: multipart/mixed; boundary=cs1");
        built.Body.Should().Contain("Content-ID: 1").And.Contain("Content-ID: 2");
        built.Body.Should().Contain("PATCH LedgerJournalHeaders(dataAreaId='USMF',JournalBatchNumber='B1') HTTP/1.1");
        built.Body.Should().Contain("Content-Type: application/json\r\n\r\n{\"Description\":\"x\"}");
        built.Body.Should().Contain("DELETE LedgerJournalHeaders(dataAreaId='USMF',JournalBatchNumber='B2') HTTP/1.1");
        built.Body.Should().EndWith("--cs1--\r\n--batch1--\r\n");
    }

    [Fact]
    public void Build_Individual_EmitsTopLevelParts_WithNoChangeset()
    {
        ODataBatchRequestBuilder.BuiltBatchRequest built =
            ODataBatchRequestBuilder.Build([Patch, Delete], atomic: false, "batch1", "cs1");

        built.Body.Should().NotContain("multipart/mixed; boundary=cs1");
        built.Body.Should().Contain("--batch1\r\nContent-Type: application/http");
        built.Body.Should().EndWith("--batch1--\r\n");
    }

    [Fact]
    public void Parse_IndividualResponses_ReturnsPerOperationStatusesInOrder()
    {
        string body = Wire(
            "--resp1",
            "Content-Type: application/http",
            "",
            "HTTP/1.1 204 No Content",
            "",
            "--resp1",
            "Content-Type: application/http",
            "",
            "HTTP/1.1 400 Bad Request",
            "Content-Type: application/json",
            "",
            "{\"error\":{\"code\":\"X\",\"message\":\"bad\"}}",
            "--resp1--");

        IReadOnlyList<ODataBatchResponseParser.BatchSubResponse> results =
            ODataBatchResponseParser.Parse("multipart/mixed; boundary=resp1", body);

        results.Select(r => r.StatusCode).Should().Equal(204, 400);
        results[0].Body.Should().BeNull();
        results[1].Body.Should().Contain("\"code\":\"X\"");
    }

    [Fact]
    public void Parse_ChangesetSuccess_CorrelatesByContentId()
    {
        string body = Wire(
            "--b1",
            "Content-Type: multipart/mixed; boundary=cs1",
            "",
            "--cs1",
            "Content-Type: application/http",
            "Content-ID: 1",
            "",
            "HTTP/1.1 201 Created",
            "",
            "--cs1",
            "Content-Type: application/http",
            "Content-ID: 2",
            "",
            "HTTP/1.1 204 No Content",
            "",
            "--cs1--",
            "--b1--");

        IReadOnlyList<ODataBatchResponseParser.BatchSubResponse> results =
            ODataBatchResponseParser.Parse("multipart/mixed; boundary=b1", body);

        results.Select(r => r.ContentId).Should().Equal(1, 2);
        results.Select(r => r.StatusCode).Should().Equal(201, 204);
    }

    [Fact]
    public void Parse_ChangesetCollapsedError_ReturnsSingleFailure()
    {
        string body = Wire(
            "--b1",
            "Content-Type: multipart/mixed; boundary=cs1",
            "",
            "--cs1",
            "Content-Type: application/http",
            "",
            "HTTP/1.1 400 Bad Request",
            "Content-Type: application/json",
            "",
            "{\"error\":{\"code\":\"Y\",\"message\":\"nope\"}}",
            "--cs1--",
            "--b1--");

        IReadOnlyList<ODataBatchResponseParser.BatchSubResponse> results =
            ODataBatchResponseParser.Parse("multipart/mixed; boundary=b1", body);

        results.Should().ContainSingle();
        results[0].StatusCode.Should().Be(400);
        results[0].Body.Should().Contain("\"code\":\"Y\"");
    }

    [Fact]
    public void Parse_MissingBoundary_Throws()
    {
        Action act = () => ODataBatchResponseParser.Parse("application/json", "irrelevant");

        act.Should().Throw<FormatException>();
    }
}
