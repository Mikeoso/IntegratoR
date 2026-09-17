using System.Text;

namespace IntegratoR.OData.Common.Batch;

/// <summary>
/// Emits an OData v4 <c>multipart/mixed</c> <c>$batch</c> request body for a set of write operations,
/// either as a single atomic changeset or as independent top-level requests.
/// </summary>
/// <remarks>
/// D365 F&amp;O accepts only the multipart batch format (not the OData 4.01 JSON batch format), so the
/// body is hand-assembled to the OASIS OData v4.01 Part 1 §11.7.7 shape: CRLF line endings, a
/// client-chosen batch boundary, per-part <c>application/http</c> / <c>Content-Transfer-Encoding:
/// binary</c> / <c>Content-ID</c> headers, and an embedded <c>METHOD url HTTP/1.1</c> request line.
/// </remarks>
internal static class ODataBatchRequestBuilder
{
    private const string Crlf = "\r\n";

    /// <summary>
    /// A built batch body together with the <c>Content-Type</c> header value (carrying the boundary)
    /// that must be set on the outer <c>$batch</c> request.
    /// </summary>
    internal sealed record BuiltBatchRequest(string ContentType, string Body);

    /// <summary>
    /// Generates a boundary token that cannot collide with typical body content.
    /// </summary>
    internal static string NewBoundary(string prefix) => $"{prefix}_{Guid.NewGuid():N}";

    /// <summary>
    /// Builds the batch body and its Content-Type header.
    /// </summary>
    /// <param name="operations">The write operations, in the order they should be applied.</param>
    /// <param name="atomic">When <c>true</c>, wraps all operations in a single changeset (all-or-nothing); when <c>false</c>, emits them as independent top-level requests.</param>
    /// <param name="batchBoundary">The outer batch boundary token.</param>
    /// <param name="changesetBoundary">The changeset boundary token, used only when <paramref name="atomic"/> is <c>true</c>.</param>
    internal static BuiltBatchRequest Build(
        IReadOnlyList<BatchWriteOperation> operations,
        bool atomic,
        string batchBoundary,
        string changesetBoundary)
    {
        var sb = new StringBuilder();

        if (atomic)
        {
            sb.Append("--").Append(batchBoundary).Append(Crlf);
            sb.Append("Content-Type: multipart/mixed; boundary=").Append(changesetBoundary).Append(Crlf);
            sb.Append(Crlf);

            foreach (BatchWriteOperation operation in operations)
            {
                sb.Append("--").Append(changesetBoundary).Append(Crlf);
                AppendOperation(sb, operation);
            }

            sb.Append("--").Append(changesetBoundary).Append("--").Append(Crlf);
            sb.Append("--").Append(batchBoundary).Append("--").Append(Crlf);
        }
        else
        {
            foreach (BatchWriteOperation operation in operations)
            {
                sb.Append("--").Append(batchBoundary).Append(Crlf);
                AppendOperation(sb, operation);
            }

            sb.Append("--").Append(batchBoundary).Append("--").Append(Crlf);
        }

        return new BuiltBatchRequest($"multipart/mixed; boundary={batchBoundary}", sb.ToString());
    }

    private static void AppendOperation(StringBuilder sb, BatchWriteOperation operation)
    {
        // MIME part headers.
        sb.Append("Content-Type: application/http").Append(Crlf);
        sb.Append("Content-Transfer-Encoding: binary").Append(Crlf);
        sb.Append("Content-ID: ").Append(operation.ContentId).Append(Crlf);
        sb.Append(Crlf);

        // Embedded HTTP request: request line, headers, blank line, optional body.
        sb.Append(operation.Method.Method).Append(' ').Append(operation.RelativeUrl).Append(" HTTP/1.1").Append(Crlf);
        sb.Append("OData-Version: 4.0").Append(Crlf);
        if (operation.JsonBody is not null)
        {
            sb.Append("Content-Type: application/json").Append(Crlf);
        }

        sb.Append(Crlf);

        if (operation.JsonBody is not null)
        {
            sb.Append(operation.JsonBody).Append(Crlf);
        }
    }
}
