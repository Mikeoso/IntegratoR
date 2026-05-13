using System.Linq.Expressions;
using System.Text.Json.Serialization;
using FluentAssertions;
using IntegratoR.OData.Common.Filters;
using Xunit;

namespace IntegratoR.OData.Tests.Common.Filters;

/// <summary>
/// Unit tests for <see cref="IntegratoRODataExpressionTranslator"/> covering filter / select /
/// expand translation. The critical assertion across all tests is that
/// <see cref="JsonPropertyNameAttribute"/> is honoured when building OData property paths —
/// this is the behavioural difference from upstream PanoramicData.OData.Client which the
/// translator exists to fix.
/// </summary>
public sealed class IntegratoRODataExpressionTranslatorTests
{
    /// <summary>
    /// Test fixture that mimics the D365 F&O LedgerJournalHeader pattern: a CLR property
    /// in PascalCase decorated with [JsonPropertyName] in camelCase, alongside ordinary
    /// PascalCase fields. This is exactly the shape that broke under PanoramicData's parser.
    /// </summary>
    private sealed class JournalEntity
    {
        [JsonPropertyName("dataAreaId")]
        public string DataAreaId { get; set; } = string.Empty;

        public string JournalBatchNumber { get; set; } = string.Empty;
        public string JournalName { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public bool IsPosted { get; set; }
        public DateTime TransDate { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateOnly DueDate { get; set; }
        public long LineCount { get; set; }
        public Guid RecordId { get; set; }
        public Status Status { get; set; }
        public NestedNavigation? Nested { get; set; }
    }

    private sealed class NestedNavigation
    {
        [JsonPropertyName("nestedDataAreaId")]
        public string DataAreaId { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;
    }

    /// <summary>
    /// Collection navigation property used by the Any/All lambda tests. The inner DataAreaId
    /// also has a [JsonPropertyName] attribute so the tests prove the patch on the lambda
    /// path (GetLambdaMemberPath) honours the attribute too.
    /// </summary>
    private sealed class JournalLine
    {
        [JsonPropertyName("dataAreaId")]
        public string DataAreaId { get; set; } = string.Empty;

        public decimal Amount { get; set; }

        public Status Status { get; set; }
    }

    private sealed class JournalEntityWithLines
    {
        [JsonPropertyName("dataAreaId")]
        public string DataAreaId { get; set; } = string.Empty;

        public List<JournalLine> Lines { get; set; } = new();
    }

    private sealed class EntityWithJsonNamedNavigation
    {
        [JsonPropertyName("nestedNav")]
        public NestedNavigation? NavigationProperty { get; set; }
    }

    private enum Status { Draft, Posted, Reversed }

    // -----------------------------------------------------------------------------------------
    // FILTER — the bug the translator exists to fix
    // -----------------------------------------------------------------------------------------

    /// <summary>
    /// The canonical regression test: a CLR property `DataAreaId` with [JsonPropertyName("dataAreaId")]
    /// MUST emit the camelCase wire name in the filter, not the PascalCase CLR name.
    /// </summary>
    [Fact]
    public void ToFilterString_PropertyWithJsonPropertyName_UsesJsonName()
    {
        Expression<Func<JournalEntity, bool>> filter = x => x.DataAreaId == "USMF";

        var result = IntegratoRODataExpressionTranslator.ToFilterString(filter);

        result.Should().Be("dataAreaId eq 'USMF'");
    }

    /// <summary>
    /// Properties without [JsonPropertyName] use their CLR name verbatim — preserving the
    /// PanoramicData default behaviour for the 99% case.
    /// </summary>
    [Fact]
    public void ToFilterString_PropertyWithoutJsonPropertyName_UsesClrName()
    {
        Expression<Func<JournalEntity, bool>> filter = x => x.JournalBatchNumber == "00123";

        var result = IntegratoRODataExpressionTranslator.ToFilterString(filter);

        result.Should().Be("JournalBatchNumber eq '00123'");
    }

    /// <summary>
    /// Mixed PascalCase + camelCase in the same filter — the most realistic D365 query shape.
    /// </summary>
    [Fact]
    public void ToFilterString_MixedJsonNameAndClrName_UsesEachAppropriately()
    {
        Expression<Func<JournalEntity, bool>> filter =
            x => x.DataAreaId == "USMF" && x.JournalBatchNumber == "00123";

        var result = IntegratoRODataExpressionTranslator.ToFilterString(filter);

        result.Should().Be("dataAreaId eq 'USMF' and JournalBatchNumber eq '00123'");
    }

    [Fact]
    public void ToFilterString_NestedNavigationWithJsonPropertyName_UsesJsonNameForEachSegment()
    {
        Expression<Func<JournalEntity, bool>> filter = x => x.Nested!.DataAreaId == "DEMF";

        var result = IntegratoRODataExpressionTranslator.ToFilterString(filter);

        result.Should().Be("Nested/nestedDataAreaId eq 'DEMF'");
    }

    [Theory]
    [InlineData(ExpressionType.Equal, "eq")]
    [InlineData(ExpressionType.NotEqual, "ne")]
    [InlineData(ExpressionType.GreaterThan, "gt")]
    [InlineData(ExpressionType.GreaterThanOrEqual, "ge")]
    [InlineData(ExpressionType.LessThan, "lt")]
    [InlineData(ExpressionType.LessThanOrEqual, "le")]
    public void ToFilterString_ComparisonOperators_EmitCorrectODataOperator(ExpressionType op, string expected)
    {
        var parameter = Expression.Parameter(typeof(JournalEntity), "x");
        var member = Expression.Property(parameter, nameof(JournalEntity.Amount));
        var constant = Expression.Constant(100m);
        var binary = Expression.MakeBinary(op, member, constant);
        var lambda = Expression.Lambda<Func<JournalEntity, bool>>(binary, parameter);

        var result = IntegratoRODataExpressionTranslator.ToFilterString(lambda);

        result.Should().Be($"Amount {expected} 100");
    }

    [Fact]
    public void ToFilterString_LogicalAnd_JoinsWithAnd()
    {
        Expression<Func<JournalEntity, bool>> filter = x => x.IsPosted && x.JournalBatchNumber == "1";

        var result = IntegratoRODataExpressionTranslator.ToFilterString(filter);

        result.Should().Be("IsPosted and JournalBatchNumber eq '1'");
    }

    [Fact]
    public void ToFilterString_LogicalOr_JoinsWithOr()
    {
        Expression<Func<JournalEntity, bool>> filter = x => x.IsPosted || x.JournalName == "GenJrn";

        var result = IntegratoRODataExpressionTranslator.ToFilterString(filter);

        result.Should().Be("IsPosted or JournalName eq 'GenJrn'");
    }

    [Fact]
    public void ToFilterString_OrInsideAnd_WrapsOrInParentheses()
    {
        Expression<Func<JournalEntity, bool>> filter =
            x => x.DataAreaId == "USMF" && (x.JournalName == "A" || x.JournalName == "B");

        var result = IntegratoRODataExpressionTranslator.ToFilterString(filter);

        result.Should().Be("dataAreaId eq 'USMF' and (JournalName eq 'A' or JournalName eq 'B')");
    }

    [Fact]
    public void ToFilterString_NotOperator_EmitsNot()
    {
        Expression<Func<JournalEntity, bool>> filter = x => !x.IsPosted;

        var result = IntegratoRODataExpressionTranslator.ToFilterString(filter);

        result.Should().Be("not (IsPosted)");
    }

    [Fact]
    public void ToFilterString_NullComparison_EmitsNull()
    {
        Expression<Func<JournalEntity, bool>> filter = x => x.JournalName == null;

        var result = IntegratoRODataExpressionTranslator.ToFilterString(filter);

        result.Should().Be("JournalName eq null");
    }

    [Fact]
    public void ToFilterString_ClosureCapturedConstant_InlinesValue()
    {
        var company = "USMF";
        Expression<Func<JournalEntity, bool>> filter = x => x.DataAreaId == company;

        var result = IntegratoRODataExpressionTranslator.ToFilterString(filter);

        result.Should().Be("dataAreaId eq 'USMF'");
    }

    [Fact]
    public void ToFilterString_StringContains_EmitsContainsFunction()
    {
        Expression<Func<JournalEntity, bool>> filter = x => x.JournalName.Contains("Gen");

        var result = IntegratoRODataExpressionTranslator.ToFilterString(filter);

        result.Should().Be("contains(JournalName,'Gen')");
    }

    [Fact]
    public void ToFilterString_StringStartsWith_EmitsStartsWithFunction()
    {
        Expression<Func<JournalEntity, bool>> filter = x => x.JournalName.StartsWith("Gen");

        var result = IntegratoRODataExpressionTranslator.ToFilterString(filter);

        result.Should().Be("startswith(JournalName,'Gen')");
    }

    [Fact]
    public void ToFilterString_StringEndsWith_EmitsEndsWithFunction()
    {
        Expression<Func<JournalEntity, bool>> filter = x => x.JournalName.EndsWith("Jrn");

        var result = IntegratoRODataExpressionTranslator.ToFilterString(filter);

        result.Should().Be("endswith(JournalName,'Jrn')");
    }

    [Fact]
    public void ToFilterString_BoolMemberStandalone_EmitsBareMemberPath()
    {
        Expression<Func<JournalEntity, bool>> filter = x => x.IsPosted;

        var result = IntegratoRODataExpressionTranslator.ToFilterString(filter);

        result.Should().Be("IsPosted");
    }

    /// <summary>
    /// Enum comparisons against a <b>constant literal</b> are constant-folded by C# to an
    /// underlying integer <see cref="ConstantExpression"/>, and <see cref="BinaryExpression.Left"/>
    /// is wrapped in <c>Convert(enumMember, Int32)</c>. <c>ParseBinaryExpression</c> detects this
    /// shape and emits the D365-compatible qualified-type literal. The previous integer form
    /// (<c>Status eq 1</c>) was rejected by D365 F&amp;O OData with
    /// "incompatible types ... 'Microsoft.Dynamics.DataEntities.EnumType' and 'Edm.Int32'",
    /// confirmed by the 2026-04-16 dimension smoke test.
    /// </summary>
    [Fact]
    public void ToFilterString_EnumComparison_ConstantLiteral_EmitsQualifiedTypeLiteral()
    {
        Expression<Func<JournalEntity, bool>> filter = x => x.Status == Status.Posted;

        var result = IntegratoRODataExpressionTranslator.ToFilterString(filter);

        result.Should().Be("Status eq Microsoft.Dynamics.DataEntities.Status'Posted'");
    }

    /// <summary>
    /// Regression for the 2026-04-15 dimension smoke test: when the enum comparison is against
    /// a <b>captured local variable</b> (not a constant literal), the C# compiler emits a
    /// MemberExpression on the closure field WITHOUT a Convert wrapper. The value reaches
    /// FormatValue as an <see cref="Enum"/> instance and must be emitted in the OData v4
    /// qualified-type form <c>Microsoft.Dynamics.DataEntities.EnumType'MemberName'</c>.
    /// Prior to the fix the bare enum name was emitted as an unquoted identifier, causing D365
    /// to reject the filter with "Could not find a property named 'Posted'".
    /// </summary>
    [Fact]
    public void ToFilterString_EnumComparison_CapturedVariable_EmitsQualifiedTypeLiteral()
    {
        var capturedStatus = Status.Posted;
        Expression<Func<JournalEntity, bool>> filter = x => x.Status == capturedStatus;

        var result = IntegratoRODataExpressionTranslator.ToFilterString(filter);

        result.Should().Be("Status eq Microsoft.Dynamics.DataEntities.Status'Posted'");
    }

    /// <summary>
    /// Inequality comparison shape (<c>!=</c>) must emit the same qualified-type literal as
    /// equality — the operator is the only thing that changes. Pins that the enum-constant
    /// detection path in <c>ParseBinaryExpression</c> is not inadvertently restricted to
    /// <c>ExpressionType.Equal</c>.
    /// </summary>
    [Fact]
    public void ToFilterString_EnumInequality_ConstantLiteral_EmitsQualifiedTypeLiteral()
    {
        Expression<Func<JournalEntity, bool>> filter = x => x.Status != Status.Posted;

        var result = IntegratoRODataExpressionTranslator.ToFilterString(filter);

        result.Should().Be("Status ne Microsoft.Dynamics.DataEntities.Status'Posted'");
    }

    /// <summary>
    /// Multi-predicate shape mirrors the real-world failure surface from
    /// <c>GetDimensionOrdersQueryHandler</c>: a captured-variable enum AND a constant-literal
    /// enum combined with <c>&amp;&amp;</c>. Both predicates must emit the qualified-type form;
    /// before the ParseBinaryExpression fix the second predicate emitted <c>IsActive eq 1</c>
    /// which D365 rejected.
    /// </summary>
    [Fact]
    public void ToFilterString_MixedEnumPredicates_BothEmitQualifiedTypeLiteral()
    {
        var capturedStatus = Status.Posted;
        Expression<Func<JournalEntity, bool>> filter = x => x.Status == capturedStatus && x.Status == Status.Draft;

        var result = IntegratoRODataExpressionTranslator.ToFilterString(filter);

        result.Should().Be(
            "Status eq Microsoft.Dynamics.DataEntities.Status'Posted' and "
            + "Status eq Microsoft.Dynamics.DataEntities.Status'Draft'");
    }

    [Fact]
    public void ToFilterString_DateTimeComparison_EmitsIsoDate()
    {
        var date = new DateTime(2026, 4, 13, 0, 0, 0, DateTimeKind.Utc);
        Expression<Func<JournalEntity, bool>> filter = x => x.TransDate == date;

        var result = IntegratoRODataExpressionTranslator.ToFilterString(filter);

        result.Should().Be("TransDate eq 2026-04-13T00:00:00Z");
    }

    [Fact]
    public void ToFilterString_StringWithApostrophe_DoublesQuotedApostrophes()
    {
        Expression<Func<JournalEntity, bool>> filter = x => x.JournalName == "O'Brien";

        var result = IntegratoRODataExpressionTranslator.ToFilterString(filter);

        result.Should().Be("JournalName eq 'O''Brien'");
    }

    [Fact]
    public void ToFilterString_NullFilter_Throws()
    {
        Action act = () => IntegratoRODataExpressionTranslator.ToFilterString<JournalEntity>(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    // -----------------------------------------------------------------------------------------
    // SELECT
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void ToSelectString_PropertyWithJsonPropertyName_UsesJsonName()
    {
        Expression<Func<JournalEntity, object>> selector = x => x.DataAreaId;

        var result = IntegratoRODataExpressionTranslator.ToSelectString(selector);

        result.Should().Be("dataAreaId");
    }

    [Fact]
    public void ToSelectString_AnonymousTypeWithMixedNames_UsesEachAppropriately()
    {
        Expression<Func<JournalEntity, object>> selector =
            x => new { x.DataAreaId, x.JournalBatchNumber, x.Amount };

        var result = IntegratoRODataExpressionTranslator.ToSelectString(selector);

        result.Should().Be("dataAreaId,JournalBatchNumber,Amount");
    }

    [Fact]
    public void ToSelectString_NullSelector_Throws()
    {
        Action act = () => IntegratoRODataExpressionTranslator.ToSelectString<JournalEntity>(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    // -----------------------------------------------------------------------------------------
    // EXPAND
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void ToExpandString_NavigationProperty_EmitsPath()
    {
        Expression<Func<JournalEntity, object>> selector = x => x.Nested!;

        var result = IntegratoRODataExpressionTranslator.ToExpandString(selector);

        result.Should().Be("Nested");
    }

    [Fact]
    public void ToExpandString_NavigationPropertyWithJsonPropertyName_UsesJsonName()
    {
        Expression<Func<EntityWithJsonNamedNavigation, object>> selector = x => x.NavigationProperty!;

        var result = IntegratoRODataExpressionTranslator.ToExpandString(selector);

        result.Should().Be("nestedNav");
    }

    [Fact]
    public void ToExpandString_NullSelector_Throws()
    {
        Action act = () => IntegratoRODataExpressionTranslator.ToExpandString<JournalEntity>(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    // -----------------------------------------------------------------------------------------
    // LAMBDA PATH (Any/All) — the fourth patched site
    // -----------------------------------------------------------------------------------------

    /// <summary>
    /// Critical regression test for the patch on <c>GetLambdaMemberPath</c>. Without the
    /// [JsonPropertyName] resolution in the lambda path, this would emit
    /// <c>Lines/any(l: l/DataAreaId eq 'USMF')</c> and D365 would reject it.
    /// </summary>
    [Fact]
    public void ToFilterString_AnyLambdaWithJsonPropertyName_UsesJsonNameInsideLambda()
    {
        Expression<Func<JournalEntityWithLines, bool>> filter =
            x => x.Lines.Any(l => l.DataAreaId == "USMF");

        var result = IntegratoRODataExpressionTranslator.ToFilterString(filter);

        result.Should().Be("Lines/any(l: l/dataAreaId eq 'USMF')");
    }

    [Fact]
    public void ToFilterString_AllLambdaWithJsonPropertyName_UsesJsonNameInsideLambda()
    {
        Expression<Func<JournalEntityWithLines, bool>> filter =
            x => x.Lines.All(l => l.DataAreaId == "USMF");

        var result = IntegratoRODataExpressionTranslator.ToFilterString(filter);

        result.Should().Be("Lines/all(l: l/dataAreaId eq 'USMF')");
    }

    [Fact]
    public void ToFilterString_AnyLambdaWithCompositeCondition_HonoursPrecedence()
    {
        Expression<Func<JournalEntityWithLines, bool>> filter =
            x => x.Lines.Any(l => l.DataAreaId == "USMF" && l.Amount > 100);

        var result = IntegratoRODataExpressionTranslator.ToFilterString(filter);

        result.Should().Be("Lines/any(l: l/dataAreaId eq 'USMF' and l/Amount gt 100)");
    }

    [Fact]
    public void ToFilterString_AnyWithoutPredicate_EmitsAnyParens()
    {
        Expression<Func<JournalEntityWithLines, bool>> filter = x => x.Lines.Any();

        var result = IntegratoRODataExpressionTranslator.ToFilterString(filter);

        result.Should().Be("Lines/any()");
    }

    [Fact]
    public void ToFilterString_StringIsNullOrEmptyInLambda_EmitsEqNullOrEqEmpty()
    {
        Expression<Func<JournalEntityWithLines, bool>> filter =
            x => x.Lines.Any(l => string.IsNullOrEmpty(l.DataAreaId));

        var result = IntegratoRODataExpressionTranslator.ToFilterString(filter);

        result.Should().Be("Lines/any(l: (l/dataAreaId eq null or l/dataAreaId eq ''))");
    }

    /// <summary>
    /// Regression for the enum-constant interception missing on the lambda path. Before this
    /// fix, <c>l => l.Status == Status.Posted</c> inside an Any/All body was parsed by
    /// ParseLambdaBinaryExpression which stripped the Convert(enum, int) wrapper on the
    /// member side and emitted the integer form (<c>l/Status eq 1</c>) — D365 rejects this
    /// with the same "incompatible types ... 'Edm.Int32'" error as the top-level case.
    /// </summary>
    [Fact]
    public void ToFilterString_AnyLambdaWithEnumConstantLiteral_EmitsQualifiedTypeForm()
    {
        Expression<Func<JournalEntityWithLines, bool>> filter =
            x => x.Lines.Any(l => l.Status == Status.Posted);

        var result = IntegratoRODataExpressionTranslator.ToFilterString(filter);

        result.Should().Be("Lines/any(l: l/Status eq Microsoft.Dynamics.DataEntities.Status'Posted')");
    }

    [Fact]
    public void ToFilterString_AllLambdaWithEnumConstantLiteralInequality_EmitsQualifiedTypeForm()
    {
        Expression<Func<JournalEntityWithLines, bool>> filter =
            x => x.Lines.All(l => l.Status != Status.Draft);

        var result = IntegratoRODataExpressionTranslator.ToFilterString(filter);

        result.Should().Be("Lines/all(l: l/Status ne Microsoft.Dynamics.DataEntities.Status'Draft')");
    }

    [Fact]
    public void ToFilterString_AnyLambdaWithEnumLiteralCombinedPredicate_EmitsQualifiedTypeForm()
    {
        Expression<Func<JournalEntityWithLines, bool>> filter =
            x => x.Lines.Any(l => l.Status == Status.Posted && l.Amount > 100);

        var result = IntegratoRODataExpressionTranslator.ToFilterString(filter);

        result.Should().Be("Lines/any(l: l/Status eq Microsoft.Dynamics.DataEntities.Status'Posted' and l/Amount gt 100)");
    }

    // -----------------------------------------------------------------------------------------
    // COLLECTION CONTAINS → "in" CLAUSE
    //
    // Use a List<T> (or any IEnumerable<T> reference type) for the collection. On .NET 10 the
    // C# compiler prefers MemoryExtensions.Contains<T>(ReadOnlySpan<T>, T) over
    // Enumerable.Contains<T>(IEnumerable<T>, T) when the receiver is T[], and ReadOnlySpan<T>
    // is byref-like which neither the interpreter nor TryEvaluateWithReflection can hydrate.
    // List<T> avoids the span overload entirely.
    // -----------------------------------------------------------------------------------------

    private static readonly List<string> StaticCompanies = ["USMF", "DEMF", "GBSI"];

    [Fact]
    public void ToFilterString_StaticListContainsProperty_EmitsInClauseWithJsonName()
    {
        Expression<Func<JournalEntity, bool>> filter = x => StaticCompanies.Contains(x.DataAreaId);

        var result = IntegratoRODataExpressionTranslator.ToFilterString(filter);

        result.Should().Be("dataAreaId in ('USMF','DEMF','GBSI')");
    }

    // -----------------------------------------------------------------------------------------
    // ADDITIONAL VALUE TYPES
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void ToFilterString_NotEqualToNull_EmitsNeNull()
    {
        Expression<Func<JournalEntity, bool>> filter = x => x.JournalName != null;

        var result = IntegratoRODataExpressionTranslator.ToFilterString(filter);

        result.Should().Be("JournalName ne null");
    }

    [Fact]
    public void ToFilterString_DateTimeOffsetComparison_EmitsIsoUtc()
    {
        var moment = new DateTimeOffset(2026, 4, 13, 14, 30, 0, TimeSpan.FromHours(2));
        Expression<Func<JournalEntity, bool>> filter = x => x.CreatedAt == moment;

        var result = IntegratoRODataExpressionTranslator.ToFilterString(filter);

        // Expected emits the moment normalised to UTC (12:30Z), proving FormatValue's
        // DateTimeOffset arm converts to UtcDateTime before formatting.
        result.Should().Be("CreatedAt eq 2026-04-13T12:30:00Z");
    }

    /// <summary>
    /// Decimal literals must use InvariantCulture — without that, a German-locale process would
    /// emit "1,23" instead of "1.23" and D365 would reject the filter as an invalid numeric literal.
    /// This test runs under the test process's culture but the expectation is that the translator
    /// is culture-independent, so the result is the same in any locale.
    /// </summary>
    [Fact]
    public void ToFilterString_DecimalComparison_UsesInvariantCulture()
    {
        Expression<Func<JournalEntity, bool>> filter = x => x.Amount > 1234.56m;

        var result = IntegratoRODataExpressionTranslator.ToFilterString(filter);

        result.Should().Be("Amount gt 1234.56");
    }

    [Fact]
    public void ToFilterString_LongComparison_UsesInvariantCulture()
    {
        long threshold = 9_999_999_999L;
        Expression<Func<JournalEntity, bool>> filter = x => x.LineCount > threshold;

        var result = IntegratoRODataExpressionTranslator.ToFilterString(filter);

        result.Should().Be("LineCount gt 9999999999");
    }

    [Fact]
    public void ToFilterString_DateOnlyComparison_EmitsIsoDateOnly()
    {
        var due = new DateOnly(2026, 4, 13);
        Expression<Func<JournalEntity, bool>> filter = x => x.DueDate == due;

        var result = IntegratoRODataExpressionTranslator.ToFilterString(filter);

        result.Should().Be("DueDate eq 2026-04-13");
    }

    [Fact]
    public void ToFilterString_GuidComparison_EmitsBareGuid()
    {
        var id = new Guid("a3a8b7e3-1c2f-4a5b-9c0d-1e2f3a4b5c6d");
        Expression<Func<JournalEntity, bool>> filter = x => x.RecordId == id;

        var result = IntegratoRODataExpressionTranslator.ToFilterString(filter);

        result.Should().Be("RecordId eq a3a8b7e3-1c2f-4a5b-9c0d-1e2f3a4b5c6d");
    }

    // -----------------------------------------------------------------------------------------
    // EMPTY SELECT/EXPAND GUARDS
    // -----------------------------------------------------------------------------------------

    /// <summary>
    /// A select expression that doesn't resolve to any selectable members must throw rather
    /// than silently producing <c>$select=</c> with an empty list (which the OData server would
    /// reject with a less helpful error).
    /// </summary>
    [Fact]
    public void ToSelectString_UnsupportedShape_Throws()
    {
        // Constant-only selector — no member access, so GetMemberNames returns []
        Expression<Func<JournalEntity, object>> selector = x => "literal";

        Action act = () => IntegratoRODataExpressionTranslator.ToSelectString(selector);

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*did not resolve to any selectable OData members*");
    }

    [Fact]
    public void ToExpandString_UnsupportedShape_Throws()
    {
        Expression<Func<JournalEntity, object>> selector = x => "literal";

        Action act = () => IntegratoRODataExpressionTranslator.ToExpandString(selector);

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*did not resolve to any OData expand paths*");
    }

    // -----------------------------------------------------------------------------------------
    // CACHING
    // -----------------------------------------------------------------------------------------

    /// <summary>
    /// JSON-name resolution is cached per <see cref="System.Reflection.MemberInfo"/>. Calling
    /// the translator multiple times with the same expression shape should not re-read the
    /// attribute via reflection. We can't observe the cache directly, but we can verify the
    /// repeated calls produce identical results (regression guard against accidentally
    /// mutating the cache state).
    /// </summary>
    [Fact]
    public void ToFilterString_CalledRepeatedly_IsStable()
    {
        Expression<Func<JournalEntity, bool>> filter = x => x.DataAreaId == "USMF";

        var first = IntegratoRODataExpressionTranslator.ToFilterString(filter);
        var second = IntegratoRODataExpressionTranslator.ToFilterString(filter);
        var third = IntegratoRODataExpressionTranslator.ToFilterString(filter);

        first.Should().Be("dataAreaId eq 'USMF'");
        second.Should().Be(first);
        third.Should().Be(first);
    }
}
