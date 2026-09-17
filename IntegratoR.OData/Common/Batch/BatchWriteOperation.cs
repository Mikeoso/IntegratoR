namespace IntegratoR.OData.Common.Batch;

/// <summary>
/// One write request inside an OData <c>$batch</c>: the HTTP method, the entity-set-relative URL, an
/// optional JSON body, and the <c>Content-ID</c> used to correlate the request with its response.
/// </summary>
/// <param name="ContentId">The 1-based Content-ID that correlates this request with its response part.</param>
/// <param name="Method">The HTTP method (POST for create, PATCH for update, DELETE for delete).</param>
/// <param name="RelativeUrl">The entity-set-relative URL, e.g. <c>LedgerJournalHeaders(dataAreaId='USMF',JournalBatchNumber='B1')</c> or <c>LedgerJournalHeaders</c> for a create.</param>
/// <param name="JsonBody">The serialised JSON payload, or <c>null</c> for a body-less request such as DELETE.</param>
internal sealed record BatchWriteOperation(int ContentId, HttpMethod Method, string RelativeUrl, string? JsonBody = null);
