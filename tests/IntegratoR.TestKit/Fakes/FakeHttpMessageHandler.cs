using System.Net;

namespace IntegratoR.TestKit.Fakes;

/// <summary>
/// A test double for <see cref="HttpMessageHandler"/> that dequeues pre-configured
/// <see cref="HttpResponseMessage"/> instances in FIFO order and records all sent requests.
/// </summary>
/// <remarks>
/// Use <see cref="Queue(HttpResponseMessage)"/> or <see cref="Queue(HttpStatusCode, string?)"/>
/// before each expected HTTP call. Calling <see cref="CreateClient"/> produces an
/// <see cref="HttpClient"/> that routes all requests through this handler.
/// </remarks>
public sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<HttpResponseMessage> _responses = new();
    private readonly List<HttpRequestMessage> _sentRequests = new();
    private readonly List<string?> _sentRequestBodies = new();

    /// <summary>
    /// Gets a read-only view of all <see cref="HttpRequestMessage"/> instances that were sent
    /// through this handler in the order they were received.
    /// </summary>
    public IReadOnlyList<HttpRequestMessage> SentRequests => _sentRequests.AsReadOnly();

    /// <summary>
    /// Gets a read-only view of the request body strings captured at send time, indexed in
    /// lockstep with <see cref="SentRequests"/>. Captured eagerly so the body is still readable
    /// after the caller disposes the <see cref="HttpRequestMessage"/> (which disposes its content).
    /// A request with no content yields <c>null</c>.
    /// </summary>
    public IReadOnlyList<string?> SentRequestBodies => _sentRequestBodies.AsReadOnly();

    /// <summary>
    /// Enqueues a pre-built <see cref="HttpResponseMessage"/> to be returned by the next
    /// call to <see cref="SendAsync"/>.
    /// </summary>
    /// <param name="response">The response message to enqueue.</param>
    public void Queue(HttpResponseMessage response)
        => _responses.Enqueue(response);

    /// <summary>
    /// Enqueues a response with the given <paramref name="statusCode"/> and optional
    /// <paramref name="content"/> string body to be returned by the next call to <see cref="SendAsync"/>.
    /// </summary>
    /// <param name="statusCode">The HTTP status code for the response.</param>
    /// <param name="content">Optional string content for the response body.</param>
    public void Queue(HttpStatusCode statusCode, string? content = null)
    {
        var response = new HttpResponseMessage(statusCode);
        if (content is not null)
            response.Content = new StringContent(content);
        _responses.Enqueue(response);
    }

    /// <summary>
    /// Creates an <see cref="HttpClient"/> that routes all requests through this handler.
    /// </summary>
    /// <returns>A configured <see cref="HttpClient"/> instance.</returns>
    public HttpClient CreateClient() => new(this, disposeHandler: false);

    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (_responses.Count == 0)
            throw new InvalidOperationException(
                "No more queued responses. Call Queue() before making HTTP requests.");

        _sentRequests.Add(request);

        // Capture the body eagerly so it survives the caller disposing the request.
        string? body = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        _sentRequestBodies.Add(body);

        return _responses.Dequeue();
    }
}
