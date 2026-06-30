using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace IntegratoR.OData.FO.Tests.Integration.Fakes;

/// <summary>
/// A stateful, in-process fake of a D365 F&amp;O OData endpoint, wired as the primary
/// <see cref="HttpMessageHandler"/> behind the named <c>"ODataClient"</c> HttpClient so the full
/// pipeline (auth handler -&gt; Polly no-op -&gt; this fake) drives a real create/read/update/delete
/// cycle against an in-memory store. It is the automated equivalent of a live D365 sandbox.
/// </summary>
/// <remarks>
/// The store models the two LedgerJournal entity sets used by the integration test:
/// <list type="bullet">
///   <item><description><c>LedgerJournalHeaders</c>, keyed by <c>dataAreaId</c> + <c>JournalBatchNumber</c>.</description></item>
///   <item><description><c>LedgerJournalLines</c>, keyed by <c>dataAreaId</c> + <c>JournalBatchNumber</c> + <c>LineNumber</c>.</description></item>
/// </list>
/// Server-assigned keys mimic D365 number sequences: a header POST is given a fresh
/// <c>JournalBatchNumber</c> ("LNR0000001", "LNR0000002", …) and a line POST is given a per-batch
/// decimal <c>LineNumber</c> (1, 2, …).
///
/// <para>
/// Any request whose method + URL shape is not recognised <b>throws</b> with the absolute URL so a
/// PanoramicData wire-format surprise surfaces as a diagnostic failure rather than silently wrong
/// behaviour. Every request (method + absolute URL + body) is also captured on
/// <see cref="Requests"/> for assertion.
/// </para>
/// </remarks>
public sealed class StatefulD365Handler : HttpMessageHandler
{
    private const string HeaderSet = "LedgerJournalHeaders";
    private const string LineSet = "LedgerJournalLines";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    // Composite-key -> stored entity (a mutable JSON object). Insertion order is preserved so GET
    // filter results are returned deterministically.
    private readonly Dictionary<string, JsonObject> _headers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, JsonObject> _lines = new(StringComparer.Ordinal);

    private int _headerCounter;
    private readonly Dictionary<string, decimal> _lineCountersByBatch = new(StringComparer.Ordinal);

    private readonly List<CapturedRequest> _requests = new();

    /// <summary>Every request observed by the fake, in send order.</summary>
    public IReadOnlyList<CapturedRequest> Requests => _requests;

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        string absoluteUrl = request.RequestUri!.AbsoluteUri;
        string? body = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        _requests.Add(new CapturedRequest(request.Method, absoluteUrl, body));

        // Path after the OData service root, e.g. "LedgerJournalHeaders" or
        // "LedgerJournalHeaders(dataAreaId='1210',JournalBatchNumber='LNR0000001')".
        string path = Uri.UnescapeDataString(request.RequestUri.AbsolutePath);
        string lastSegment = path[(path.LastIndexOf('/') + 1)..];
        string query = request.RequestUri.Query; // includes leading '?'

        // PanoramicData may probe $metadata before typed operations. Serve a minimal valid CSDL so
        // the typed model can resolve; if it never asks, this arm is simply never hit.
        if (lastSegment.Equals("$metadata", StringComparison.OrdinalIgnoreCase))
        {
            return Xml(MinimalMetadata);
        }

        // Keyed segment "EntitySet(field=literal,...)" — used by PATCH/DELETE (the composite-key
        // write bypass) and, in principle, a keyed GET (the framework does NOT issue keyed GETs;
        // composite reads go through $filter, handled below).
        int parenIndex = lastSegment.IndexOf('(');
        if (parenIndex >= 0)
        {
            string entitySet = lastSegment[..parenIndex];
            string keySegment = lastSegment[(parenIndex + 1)..].TrimEnd(')');
            IReadOnlyDictionary<string, string> keyFields = ParseKeySegment(keySegment);

            return request.Method.Method switch
            {
                "PATCH" => HandlePatch(entitySet, keyFields, body, absoluteUrl, request.Method),
                "DELETE" => HandleDelete(entitySet, keyFields),
                "GET" => HandleKeyedGet(entitySet, keyFields),
                _ => throw Unrecognised(request.Method, absoluteUrl),
            };
        }

        // Collection segment "EntitySet" — POST (create) and GET (filter).
        return request.Method.Method switch
        {
            "POST" => HandlePost(lastSegment, body, absoluteUrl, request.Method),
            "GET" => HandleFilterGet(lastSegment, query, absoluteUrl, request.Method),
            _ => throw Unrecognised(request.Method, absoluteUrl),
        };
    }

    // ----- POST (create) ---------------------------------------------------------------------

    private HttpResponseMessage HandlePost(string entitySet, string? body, string url, HttpMethod method)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            throw Unrecognised(method, url, "POST with no body");
        }

        JsonObject entity = ParseObject(body, url);

        if (entitySet.Equals(HeaderSet, StringComparison.Ordinal))
        {
            string dataAreaId = RequireString(entity, "dataAreaId", url);
            string jbn = $"LNR{(++_headerCounter):D7}";
            entity["JournalBatchNumber"] = jbn;

            string key = HeaderKey(dataAreaId, jbn);
            _headers[key] = entity;
            return Json(HttpStatusCode.Created, entity.ToJsonString());
        }

        if (entitySet.Equals(LineSet, StringComparison.Ordinal))
        {
            string dataAreaId = RequireString(entity, "dataAreaId", url);
            string jbn = RequireString(entity, "JournalBatchNumber", url);

            string batchKey = HeaderKey(dataAreaId, jbn);
            decimal next = _lineCountersByBatch.TryGetValue(batchKey, out decimal current) ? current + 1m : 1m;
            _lineCountersByBatch[batchKey] = next;
            entity["LineNumber"] = next;

            // D365 returns the FULL row, including fields the client omitted from the create payload
            // because they are server-assigned/read-only (IgnoreOnCreate). The client entity declares
            // AccountDisplayValue and TransDate as required, so the create response must carry them or
            // STJ's required-property check fails on the round-trip. Backfill the values D365 echoes.
            entity.TryAdd("AccountDisplayValue", string.Empty);
            entity.TryAdd("TransDate", DateTimeOffset.UnixEpoch.ToString("O"));

            string key = LineKey(dataAreaId, jbn, next);
            _lines[key] = entity;
            return Json(HttpStatusCode.Created, entity.ToJsonString());
        }

        throw Unrecognised(method, url, $"POST to unknown entity set '{entitySet}'");
    }

    // ----- GET (filter) ----------------------------------------------------------------------

    private HttpResponseMessage HandleFilterGet(string entitySet, string query, string url, HttpMethod method)
    {
        Dictionary<string, JsonObject> store = StoreFor(entitySet, method, url);

        IReadOnlyDictionary<string, string> queryParams = ParseQuery(query);
        queryParams.TryGetValue("$filter", out string? filter);
        int? top = queryParams.TryGetValue("$top", out string? topValue)
                   && int.TryParse(topValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedTop)
            ? parsedTop
            : null;

        IEnumerable<JsonObject> matches = store.Values;
        if (!string.IsNullOrWhiteSpace(filter))
        {
            IReadOnlyList<FilterClause> clauses = ParseFilter(filter!, url);
            matches = matches.Where(entity => clauses.All(clause => Matches(entity, clause)));
        }

        if (top.HasValue)
        {
            matches = matches.Take(top.Value);
        }

        var array = new JsonArray();
        foreach (JsonObject match in matches)
        {
            array.Add(Clone(match));
        }

        var envelope = new JsonObject
        {
            ["@odata.context"] = $"https://fake.local/data/$metadata#{entitySet}",
            ["value"] = array,
        };

        return Json(HttpStatusCode.OK, envelope.ToJsonString());
    }

    // ----- GET (keyed) -----------------------------------------------------------------------

    private HttpResponseMessage HandleKeyedGet(string entitySet, IReadOnlyDictionary<string, string> keyFields)
    {
        Dictionary<string, JsonObject> store = entitySet.Equals(HeaderSet, StringComparison.Ordinal)
            ? _headers
            : _lines;

        string key = KeyFromSegment(entitySet, keyFields);
        return store.TryGetValue(key, out JsonObject? entity)
            ? Json(HttpStatusCode.OK, entity.ToJsonString())
            : Json(HttpStatusCode.NotFound, "{\"error\":{\"code\":\"NotFound\",\"message\":\"Not found\"}}");
    }

    // ----- PATCH (update) --------------------------------------------------------------------

    private HttpResponseMessage HandlePatch(
        string entitySet,
        IReadOnlyDictionary<string, string> keyFields,
        string? body,
        string url,
        HttpMethod method)
    {
        Dictionary<string, JsonObject> store = entitySet.Equals(HeaderSet, StringComparison.Ordinal)
            ? _headers
            : _lines;

        string key = KeyFromSegment(entitySet, keyFields);
        if (!store.TryGetValue(key, out JsonObject? stored))
        {
            return Json(HttpStatusCode.NotFound, "{\"error\":{\"code\":\"NotFound\",\"message\":\"Not found\"}}");
        }

        if (!string.IsNullOrWhiteSpace(body))
        {
            JsonObject patch = ParseObject(body!, url);
            foreach (KeyValuePair<string, JsonNode?> property in patch)
            {
                stored[property.Key] = property.Value is null ? null : property.Value.DeepClone();
            }
        }

        // D365 answers a PATCH with 204 No Content (it does not echo the entity unless asked via
        // Prefer: return=representation), so the store is mutated but no body is returned. Mirroring
        // that here exercises ODataService.UpdateAsync's null-result handling — a 200+body would
        // have masked the live 204-No-Content bug this test now guards against.
        return new HttpResponseMessage(HttpStatusCode.NoContent);
    }

    // ----- DELETE ----------------------------------------------------------------------------

    private HttpResponseMessage HandleDelete(string entitySet, IReadOnlyDictionary<string, string> keyFields)
    {
        Dictionary<string, JsonObject> store = entitySet.Equals(HeaderSet, StringComparison.Ordinal)
            ? _headers
            : _lines;

        string key = KeyFromSegment(entitySet, keyFields);
        store.Remove(key);
        return new HttpResponseMessage(HttpStatusCode.NoContent);
    }

    // ----- Key helpers -----------------------------------------------------------------------

    private static string HeaderKey(string dataAreaId, string jbn) => $"{dataAreaId}|{jbn}";

    private static string LineKey(string dataAreaId, string jbn, decimal lineNumber)
        => $"{dataAreaId}|{jbn}|{lineNumber.ToString(CultureInfo.InvariantCulture)}";

    private static string KeyFromSegment(string entitySet, IReadOnlyDictionary<string, string> keyFields)
    {
        string dataAreaId = keyFields["dataAreaId"];
        string jbn = keyFields["JournalBatchNumber"];

        if (entitySet.Equals(HeaderSet, StringComparison.Ordinal))
        {
            return HeaderKey(dataAreaId, jbn);
        }

        decimal lineNumber = decimal.Parse(keyFields["LineNumber"], CultureInfo.InvariantCulture);
        return LineKey(dataAreaId, jbn, lineNumber);
    }

    private Dictionary<string, JsonObject> StoreFor(string entitySet, HttpMethod method, string url)
    {
        if (entitySet.Equals(HeaderSet, StringComparison.Ordinal))
        {
            return _headers;
        }

        if (entitySet.Equals(LineSet, StringComparison.Ordinal))
        {
            return _lines;
        }

        throw Unrecognised(method, url, $"unknown entity set '{entitySet}'");
    }

    // ----- Parsing ---------------------------------------------------------------------------

    // Splits "dataAreaId='1210',JournalBatchNumber='LNR0000001'" (or with an unquoted decimal
    // LineNumber=1) into field -> literal-value (quotes stripped). Respects single-quoted strings so
    // a comma inside a value would not split incorrectly.
    private static Dictionary<string, string> ParseKeySegment(string segment)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string part in SplitTopLevel(segment, ','))
        {
            int eq = part.IndexOf('=');
            if (eq < 0)
            {
                continue;
            }

            string field = part[..eq].Trim();
            string value = StripQuotes(part[(eq + 1)..].Trim());
            result[field] = value;
        }

        return result;
    }

    // Parses an OData $filter of the form "<clause> and <clause> and ..." where each clause is
    // "<field> eq <literal>" (string literals single-quoted, decimals unquoted). Field names are the
    // wire names, including the camelCase dataAreaId.
    private static IReadOnlyList<FilterClause> ParseFilter(string filter, string url)
    {
        // PanoramicData wraps the whole expression in a single pair of parentheses, e.g.
        // "(dataAreaId eq '1210' and JournalBatchNumber eq 'LNR0000001')". Strip a balanced
        // enclosing pair (outside any string literal) before splitting on " and ".
        string normalised = StripEnclosingParens(filter.Trim());

        var clauses = new List<FilterClause>();
        foreach (string clause in SplitOnKeyword(normalised, " and "))
        {
            string trimmed = clause.Trim();
            int eqIndex = trimmed.IndexOf(" eq ", StringComparison.Ordinal);
            if (eqIndex < 0)
            {
                throw new InvalidOperationException(
                    $"StatefulD365Handler could not parse $filter clause '{trimmed}' (URL: {url}). " +
                    "Only '<field> eq <literal>' clauses joined by ' and ' are supported.");
            }

            string field = trimmed[..eqIndex].Trim();
            string literal = trimmed[(eqIndex + 4)..].Trim();
            clauses.Add(new FilterClause(field, StripQuotes(literal), IsQuoted(literal)));
        }

        return clauses;
    }

    private static bool Matches(JsonObject entity, FilterClause clause)
    {
        if (!entity.TryGetPropertyValue(clause.Field, out JsonNode? node) || node is null)
        {
            return false;
        }

        string actual = node.GetValueKind() == JsonValueKind.String
            ? node.GetValue<string>()
            : node.ToJsonString();

        if (clause.IsString)
        {
            return string.Equals(actual, clause.Value, StringComparison.Ordinal);
        }

        // Numeric comparison so "1" matches a stored 1 regardless of decimal formatting.
        return decimal.TryParse(actual, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal actualNumber)
               && decimal.TryParse(clause.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal expectedNumber)
               && actualNumber == expectedNumber;
    }

    private static IReadOnlyDictionary<string, string> ParseQuery(string query)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrEmpty(query))
        {
            return result;
        }

        foreach (string pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            int eq = pair.IndexOf('=');
            if (eq < 0)
            {
                continue;
            }

            string name = Uri.UnescapeDataString(pair[..eq]);
            string value = Uri.UnescapeDataString(pair[(eq + 1)..]);
            result[name] = value;
        }

        return result;
    }

    private static JsonObject ParseObject(string json, string url)
    {
        JsonNode? node = JsonNode.Parse(json);
        return node as JsonObject
               ?? throw new InvalidOperationException(
                   $"StatefulD365Handler expected a JSON object body but got '{json}' (URL: {url}).");
    }

    private static string RequireString(JsonObject entity, string field, string url)
    {
        if (!entity.TryGetPropertyValue(field, out JsonNode? node) || node is null)
        {
            throw new InvalidOperationException(
                $"StatefulD365Handler expected payload to contain '{field}' but it was missing (URL: {url}). " +
                $"Body: {entity.ToJsonString()}");
        }

        return node.GetValue<string>();
    }

    // ----- Low-level string helpers ----------------------------------------------------------

    private static IEnumerable<string> SplitTopLevel(string input, char separator)
    {
        var current = new StringBuilder();
        bool inQuotes = false;
        foreach (char c in input)
        {
            if (c == '\'')
            {
                inQuotes = !inQuotes;
                current.Append(c);
            }
            else if (c == separator && !inQuotes)
            {
                yield return current.ToString();
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        if (current.Length > 0)
        {
            yield return current.ToString();
        }
    }

    // Splits on a keyword (e.g. " and ") that is NOT inside a single-quoted string literal.
    private static IEnumerable<string> SplitOnKeyword(string input, string keyword)
    {
        var segments = new List<string>();
        var current = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < input.Length; i++)
        {
            char c = input[i];
            if (c == '\'')
            {
                inQuotes = !inQuotes;
                current.Append(c);
                continue;
            }

            if (!inQuotes
                && i + keyword.Length <= input.Length
                && input.AsSpan(i, keyword.Length).SequenceEqual(keyword))
            {
                segments.Add(current.ToString());
                current.Clear();
                i += keyword.Length - 1;
                continue;
            }

            current.Append(c);
        }

        segments.Add(current.ToString());
        return segments;
    }

    // Removes one balanced enclosing pair of parentheses (ignoring parens inside string literals)
    // when the entire expression is wrapped, e.g. "(a eq '1' and b eq '2')" -> "a eq '1' and b eq '2'".
    private static string StripEnclosingParens(string expression)
    {
        while (expression.Length >= 2 && expression[0] == '(' && expression[^1] == ')')
        {
            int depth = 0;
            bool inQuotes = false;
            bool wrapsWhole = true;

            for (int i = 0; i < expression.Length; i++)
            {
                char c = expression[i];
                if (c == '\'')
                {
                    inQuotes = !inQuotes;
                }
                else if (!inQuotes && c == '(')
                {
                    depth++;
                }
                else if (!inQuotes && c == ')')
                {
                    depth--;
                    // The opening paren closed before the end — it does not wrap the whole string.
                    if (depth == 0 && i != expression.Length - 1)
                    {
                        wrapsWhole = false;
                        break;
                    }
                }
            }

            if (!wrapsWhole)
            {
                break;
            }

            expression = expression[1..^1].Trim();
        }

        return expression;
    }

    private static bool IsQuoted(string literal)
        => literal.Length >= 2 && literal[0] == '\'' && literal[^1] == '\'';

    private static string StripQuotes(string literal)
        => IsQuoted(literal) ? literal[1..^1].Replace("''", "'") : literal;

    private static JsonObject Clone(JsonObject source) => (JsonObject)source.DeepClone();

    // ----- Responses -------------------------------------------------------------------------

    private static HttpResponseMessage Json(HttpStatusCode status, string json)
        => new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static HttpResponseMessage Xml(string xml)
        => new(HttpStatusCode.OK) { Content = new StringContent(xml, Encoding.UTF8, "application/xml") };

    private static InvalidOperationException Unrecognised(HttpMethod method, string url, string? detail = null)
        => new(
            $"StatefulD365Handler received an unrecognised request: {method} {url}" +
            (detail is null ? "." : $" ({detail})."));

    // Minimal CSDL covering both entity sets, served only if PanoramicData probes $metadata.
    private const string MinimalMetadata =
        """
        <?xml version="1.0" encoding="utf-8"?>
        <edmx:Edmx Version="4.0" xmlns:edmx="http://docs.oasis-open.org/odata/ns/edmx">
          <edmx:DataServices>
            <Schema Namespace="Microsoft.Dynamics.DataEntities" xmlns="http://docs.oasis-open.org/odata/ns/edm">
              <EntityType Name="LedgerJournalHeader">
                <Key>
                  <PropertyRef Name="dataAreaId" />
                  <PropertyRef Name="JournalBatchNumber" />
                </Key>
                <Property Name="dataAreaId" Type="Edm.String" Nullable="false" />
                <Property Name="JournalBatchNumber" Type="Edm.String" Nullable="false" />
                <Property Name="JournalName" Type="Edm.String" />
                <Property Name="Description" Type="Edm.String" />
              </EntityType>
              <EntityType Name="LedgerJournalLine">
                <Key>
                  <PropertyRef Name="dataAreaId" />
                  <PropertyRef Name="JournalBatchNumber" />
                  <PropertyRef Name="LineNumber" />
                </Key>
                <Property Name="dataAreaId" Type="Edm.String" Nullable="false" />
                <Property Name="JournalBatchNumber" Type="Edm.String" Nullable="false" />
                <Property Name="LineNumber" Type="Edm.Decimal" Nullable="false" />
                <Property Name="DebitAmount" Type="Edm.Decimal" />
                <Property Name="CreditAmount" Type="Edm.Decimal" />
                <Property Name="CurrencyCode" Type="Edm.String" />
                <Property Name="Text" Type="Edm.String" />
              </EntityType>
              <EntityContainer Name="Resources">
                <EntitySet Name="LedgerJournalHeaders" EntityType="Microsoft.Dynamics.DataEntities.LedgerJournalHeader" />
                <EntitySet Name="LedgerJournalLines" EntityType="Microsoft.Dynamics.DataEntities.LedgerJournalLine" />
              </EntityContainer>
            </Schema>
          </edmx:DataServices>
        </edmx:Edmx>
        """;

    /// <summary>A single request observed by the fake.</summary>
    public sealed record CapturedRequest(HttpMethod Method, string Url, string? Body);

    private sealed record FilterClause(string Field, string Value, bool IsString);
}
