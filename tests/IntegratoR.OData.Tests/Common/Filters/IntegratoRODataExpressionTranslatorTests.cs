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
    }

    private sealed class JournalEntityWithLines
    {
        [JsonPropertyName("dataAreaId")]
        public string DataAreaId { get; set; } = string.Empty;

        public List<JournalLine> Lines { get; set; } = new();
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
    /// Enum comparisons via <c>==</c> get compiled by C# with an implicit Convert-to-underlying-type
    /// on both sides. The translator (matching PanoramicData) strips the Convert and emits the
    /// integer value. D365 F&O OData accepts integer enum values in $filter, so this is correct.
    /// To force the named-string form, callers must wrap the constant: <c>x.Status.Equals(Status.Posted)</c>
    /// (which produces a method-call expression that preserves the enum type).
    /// </summary>
    [Fact]
    public void ToFilterString_EnumComparison_EmitsUnderlyingIntegerValue()
    {
        Expression<Func<JournalEntity, bool>> filter = x => x.Status == Status.Posted;

        var result = IntegratoRODataExpressionTranslator.ToFilterString(filter);

        result.Should().Be("Status eq 1");
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

    // -----------------------------------------------------------------------------------------
    // ADDITIONAL VALUE TYPES
    //
    // NOTE: Coverage for collection-Contains → OData `in (...)` clause is intentionally
    // omitted here. PanoramicData's Contains-as-in path falls back to Expression.Compile()
    // for closure-captured collections, which throws InvalidProgramException on .NET 10
    // preview for some shapes. This is unrelated to the [JsonPropertyName] patch and pre-
    // exists in upstream PanoramicData — track upstream / .NET runtime fixes and re-add
    // the test when the underlying bug is resolved.
    // -----------------------------------------------------------------------------------------
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
        var date = new DateTimeOffset(2026, 4, 13, 12, 0, 0, TimeSpan.Zero);
        Expression<Func<JournalEntity, bool>> filter = x => x.TransDate == date.UtcDateTime;

        var result = IntegratoRODataExpressionTranslator.ToFilterString(filter);

        result.Should().Be("TransDate eq 2026-04-13T12:00:00Z");
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
