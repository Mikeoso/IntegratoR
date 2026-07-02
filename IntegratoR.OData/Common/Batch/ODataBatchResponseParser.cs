namespace IntegratoR.OData.Common.Batch;

/// <summary>
/// Parses an OData v4 <c>multipart/mixed</c> <c>$batch</c> response into a flat list of per-operation
/// sub-responses, recursing into changeset parts and correlating by <c>Content-ID</c> where present.
/// </summary>
/// <remarks>
/// The server chooses its own response boundary (taken from the response <c>Content-Type</c>, not the
/// request boundary). A changeset that failed atomically collapses to a single error sub-response;
/// callers map that failure onto every operation the changeset contained. Correlation: top-level
/// responses are positional (document order); changeset responses carry their request's <c>Content-ID</c>.
/// </remarks>
internal static class ODataBatchResponseParser
{
    /// <summary>
    /// One embedded HTTP response from a batch: its <c>Content-ID</c> (when inside a changeset), the
    /// HTTP status code, and the response body (if any).
    /// </summary>
    internal sealed record BatchSubResponse(int? ContentId, int StatusCode, string? Body);

    /// <summary>
    /// Parses a batch response body using the boundary from its <c>Content-Type</c> header.
    /// </summary>
    /// <exception cref="FormatException">Thrown when the Content-Type carries no boundary parameter.</exception>
    internal static IReadOnlyList<BatchSubResponse> Parse(string responseContentType, string responseBody)
    {
        string boundary = ExtractBoundary(responseContentType)
            ?? throw new FormatException(
                $"Batch response Content-Type did not contain a boundary: '{responseContentType}'.");

        var results = new List<BatchSubResponse>();
        ParseInto(boundary, responseBody, results);
        return results;
    }

    private static void ParseInto(string boundary, string content, List<BatchSubResponse> results)
    {
        string delimiter = "--" + boundary;
        string[] segments = content.Split(delimiter);

        foreach (string segment in segments)
        {
            string part = segment.Trim('\r', '\n');
            if (part.Length == 0 || part == "--")
            {
                // Preamble, inter-part whitespace, or the closing "--boundary--" tail.
                continue;
            }

            int headerSep = part.IndexOf("\r\n\r\n", StringComparison.Ordinal);
            string partHeaders = headerSep >= 0 ? part[..headerSep] : part;
            string partContent = headerSep >= 0 ? part[(headerSep + 4)..] : string.Empty;

            string? partContentType = GetHeader(partHeaders, "Content-Type");

            if (partContentType is not null &&
                partContentType.StartsWith("multipart/mixed", StringComparison.OrdinalIgnoreCase))
            {
                string? nested = ExtractBoundary(partContentType);
                if (nested is not null)
                {
                    ParseInto(nested, partContent, results);
                }

                continue;
            }

            int? contentId = TryParseInt(GetHeader(partHeaders, "Content-ID"));
            (int status, string? body) = ParseEmbeddedHttp(partContent);
            results.Add(new BatchSubResponse(contentId, status, body));
        }
    }

    private static (int Status, string? Body) ParseEmbeddedHttp(string embedded)
    {
        string trimmed = embedded.TrimStart('\r', '\n');

        int firstLineEnd = trimmed.IndexOf("\r\n", StringComparison.Ordinal);
        string statusLine = firstLineEnd >= 0 ? trimmed[..firstLineEnd] : trimmed;
        int status = ParseStatusCode(statusLine);

        int bodySep = trimmed.IndexOf("\r\n\r\n", StringComparison.Ordinal);
        string? body = bodySep >= 0 ? trimmed[(bodySep + 4)..].Trim('\r', '\n') : null;
        return (status, string.IsNullOrEmpty(body) ? null : body);
    }

    private static int ParseStatusCode(string statusLine)
    {
        // "HTTP/1.1 204 No Content" -> 204
        string[] tokens = statusLine.Split(' ', 3);
        return tokens.Length >= 2 && int.TryParse(tokens[1], out int code) ? code : 0;
    }

    private static string? GetHeader(string headerBlock, string name)
    {
        foreach (string line in headerBlock.Split("\r\n"))
        {
            int colon = line.IndexOf(':');
            if (colon > 0 && line[..colon].Trim().Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return line[(colon + 1)..].Trim();
            }
        }

        return null;
    }

    private static string? ExtractBoundary(string contentType)
    {
        int idx = contentType.IndexOf("boundary=", StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
        {
            return null;
        }

        string value = contentType[(idx + "boundary=".Length)..].Trim();
        int semicolon = value.IndexOf(';');
        if (semicolon >= 0)
        {
            value = value[..semicolon];
        }

        return value.Trim().Trim('"');
    }

    private static int? TryParseInt(string? raw) => int.TryParse(raw, out int value) ? value : null;
}
