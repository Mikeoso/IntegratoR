// ---------------------------------------------------------------------------------------------
// Translates LINQ expressions into raw OData query strings (filter / select / expand) while
// honouring System.Text.Json [JsonPropertyName] attributes on entity properties.
//
// Why this exists:
//   PanoramicData.OData.Client (the underlying OData v4 client) reads MemberInfo.Name directly
//   in its ODataQueryBuilder<T>.GetMemberPath() and ignores [JsonPropertyName]. For D365 F&O
//   that is fatal because some system fields (e.g. dataAreaId, validFrom, recId-family) are
//   camelCase in the OData metadata but PascalCase in the C# CLR. A LINQ filter such as
//       x => x.DataAreaId == "USMF"
//   is therefore emitted as
//       $filter=DataAreaId eq 'USMF'
//   which D365 (case-sensitive) rejects, returning empty results.
//
// This translator is structurally a near-copy of PanoramicData's
//   ODataQueryBuilder.ExpressionParsing.cs and ODataQueryBuilder.LambdaParsing.cs
// with one targeted change: every read of a property/field MemberInfo.Name is routed through
// ResolveJsonName(...) which consults [JsonPropertyName] before falling back to MemberInfo.Name.
//
// Once the upstream PR (https://github.com/panoramicdata/panoramicdata.odata.client) lands,
// this file can be deleted and the ODataClientAdapter can revert to passing Expressions
// directly to PanoramicData.
//
// ATTRIBUTION
// -----------
// The parser logic in this file is derived from PanoramicData.OData.Client, copyright (c)
// 2025 Panoramic Data Limited, released under the MIT License. The full upstream LICENSE
// is reproduced in THIRD_PARTY_LICENSES.md at the repository root.
// Source files copied/adapted:
//   - PanoramicData.OData.Client/ODataQueryBuilder.ExpressionParsing.cs
//   - PanoramicData.OData.Client/ODataQueryBuilder.LambdaParsing.cs
// ---------------------------------------------------------------------------------------------

using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json.Serialization;

namespace IntegratoR.OData.Common.Filters;

/// <summary>
/// Translates strongly-typed LINQ expressions into raw OData query strings, honouring
/// <see cref="JsonPropertyNameAttribute"/> on entity properties.
/// </summary>
internal static class IntegratoRODataExpressionTranslator
{
    /// <summary>
    /// Converts a predicate expression into an OData <c>$filter</c> clause.
    /// </summary>
    public static string ToFilterString<T>(Expression<Func<T, bool>> filter)
    {
        ArgumentNullException.ThrowIfNull(filter);
        return ExpressionToODataFilter(filter.Body);
    }

    /// <summary>
    /// Converts a member selector expression into a comma-separated OData <c>$select</c> field list.
    /// </summary>
    public static string ToSelectString<T>(Expression<Func<T, object>> selector)
    {
        ArgumentNullException.ThrowIfNull(selector);
        var members = GetMemberNames(selector.Body);
        return string.Join(",", members);
    }

    /// <summary>
    /// Converts a navigation selector expression into an OData <c>$expand</c> clause supporting
    /// nested expand and per-segment <c>$select</c>.
    /// </summary>
    public static string ToExpandString<T>(Expression<Func<T, object>> selector)
    {
        ArgumentNullException.ThrowIfNull(selector);
        var pathInfos = GetExpandMemberPathsWithInfo(selector.Body);
        var fields = BuildNestedExpandFieldsWithInfo(pathInfos);
        return string.Join(",", fields);
    }

    // -----------------------------------------------------------------------------------------
    // [JsonPropertyName] resolution — the ONE patched line vs PanoramicData
    // -----------------------------------------------------------------------------------------

    private static readonly ConcurrentDictionary<MemberInfo, string> JsonNameCache = new();

    /// <summary>
    /// Resolves the on-the-wire OData property name for a CLR member, honouring
    /// <see cref="JsonPropertyNameAttribute"/> if present. This is the single behavioural
    /// difference from upstream PanoramicData's parser.
    /// </summary>
    private static string ResolveJsonName(MemberInfo member) =>
        JsonNameCache.GetOrAdd(member, static m =>
            m.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? m.Name);

    // -----------------------------------------------------------------------------------------
    // Filter parsing — derived from ODataQueryBuilder.ExpressionParsing.cs
    // -----------------------------------------------------------------------------------------

    private static readonly FrozenDictionary<ExpressionType, string> OperatorMap = new Dictionary<ExpressionType, string>
    {
        [ExpressionType.Equal] = "eq",
        [ExpressionType.NotEqual] = "ne",
        [ExpressionType.GreaterThan] = "gt",
        [ExpressionType.GreaterThanOrEqual] = "ge",
        [ExpressionType.LessThan] = "lt",
        [ExpressionType.LessThanOrEqual] = "le",
        [ExpressionType.AndAlso] = "and",
        [ExpressionType.OrElse] = "or"
    }.ToFrozenDictionary();

    private static string ExpressionToODataFilter(Expression expression) =>
        ExpressionToODataFilter(expression, parentOperator: null);

    private static string ExpressionToODataFilter(Expression expression, ExpressionType? parentOperator) => expression switch
    {
        BinaryExpression binary => ParseBinaryExpression(binary, parentOperator),
        MethodCallExpression methodCall => ParseMethodCallExpression(methodCall),
        UnaryExpression unary when unary.NodeType == ExpressionType.Not => $"not ({ExpressionToODataFilter(unary.Operand, parentOperator)})",
        UnaryExpression unary when unary.NodeType == ExpressionType.Convert => ExpressionToODataFilter(unary.Operand, parentOperator),
        MemberExpression member when member.Type == typeof(bool) && !ShouldEvaluate(member) => GetMemberPath(member),
        MemberExpression member when ShouldEvaluate(member) => FormatValue(EvaluateExpression(member)),
        MemberExpression member => GetMemberPath(member),
        ConstantExpression constant => FormatValue(constant.Value),
        _ => throw new NotSupportedException($"Expression type {expression.NodeType} is not supported")
    };

    private static bool ShouldEvaluate(MemberExpression member)
    {
        Expression? current = member;
        while (current is MemberExpression memberExpr)
        {
            current = memberExpr.Expression;
        }

        if (current is null)
        {
            return true;
        }

        return current is ConstantExpression;
    }

    private static object? EvaluateExpression(Expression expression)
    {
        if (TryEvaluateWithReflection(expression, out var result))
        {
            return result;
        }

        var objectMember = Expression.Convert(expression, typeof(object));
        var getterLambda = Expression.Lambda<Func<object>>(objectMember);
        var getter = getterLambda.Compile();
        return getter();
    }

    private static bool TryEvaluateWithReflection(Expression expression, out object? result)
    {
        result = null;

        // Unwrap Convert nodes that the C# compiler inserts around closure captures
        // (e.g. boxing to object). Without this, closure-captured arrays and other
        // reference types would fall through to Expression.Compile(), which throws
        // InvalidProgramException on .NET 10 preview for some shapes.
        var current = expression;
        while (current is UnaryExpression unary
            && (unary.NodeType == ExpressionType.Convert || unary.NodeType == ExpressionType.ConvertChecked))
        {
            current = unary.Operand;
        }

        var memberChain = new Stack<MemberInfo>();

        while (current is MemberExpression memberExpr)
        {
            memberChain.Push(memberExpr.Member);
            current = memberExpr.Expression;
        }

        if (current is not ConstantExpression constant)
        {
            return false;
        }

        object? value = constant.Value;

        while (memberChain.Count > 0 && value is not null)
        {
            var member = memberChain.Pop();

            value = member switch
            {
                FieldInfo field => field.GetValue(value),
                PropertyInfo prop => prop.GetValue(value),
                _ => null
            };
        }

        result = value;
        return true;
    }

    private static string ParseBinaryExpression(BinaryExpression binary, ExpressionType? parentOperator)
    {
        var left = ExpressionToODataFilter(binary.Left, binary.NodeType);
        var right = ExpressionToODataFilter(binary.Right, binary.NodeType);

        if (!OperatorMap.TryGetValue(binary.NodeType, out var op))
        {
            throw new NotSupportedException($"Binary operator {binary.NodeType} is not supported");
        }

        var result = $"{left} {op} {right}";

        if (binary.NodeType == ExpressionType.OrElse && parentOperator == ExpressionType.AndAlso)
        {
            return $"({result})";
        }

        return result;
    }

    private static string ParseMethodCallExpression(MethodCallExpression methodCall)
    {
        var methodName = methodCall.Method.Name;

        if (methodCall.Object?.Type == typeof(string))
        {
            return ParseStringMethodCall(methodCall, methodName);
        }

        if (methodName == "Contains" && methodCall.Arguments.Count >= 1)
        {
            var result = TryParseCollectionContains(methodCall);
            if (result is not null)
            {
                return result;
            }
        }

        if (methodName is "Any" or "All")
        {
            var result = TryParseAnyAll(methodCall, methodName);
            if (result is not null)
            {
                return result;
            }
        }

        throw new NotSupportedException($"Method {methodName} is not supported");
    }

    private static string ParseStringMethodCall(MethodCallExpression methodCall, string methodName)
    {
        var stringPath = GetStringExpressionPath(methodCall.Object!);

        return methodName switch
        {
            "Contains" => $"contains({stringPath},{FormatValue(GetValue(methodCall.Arguments[0]))})",
            "StartsWith" => $"startswith({stringPath},{FormatValue(GetValue(methodCall.Arguments[0]))})",
            "EndsWith" => $"endswith({stringPath},{FormatValue(GetValue(methodCall.Arguments[0]))})",
            "ToLower" => $"tolower({stringPath})",
            "ToUpper" => $"toupper({stringPath})",
            "Trim" => $"trim({stringPath})",
            _ => throw new NotSupportedException($"Method {methodName} is not supported")
        };
    }

    private static string? TryParseCollectionContains(MethodCallExpression methodCall)
    {
        if (methodCall.Arguments.Count == 2 && methodCall.Object is null)
        {
            return TryParseStaticContains(methodCall);
        }

        if (methodCall.Arguments.Count == 1 && methodCall.Object is not null)
        {
            return TryParseInstanceContains(methodCall);
        }

        return null;
    }

    private static string? TryParseStaticContains(MethodCallExpression methodCall)
    {
        var collection = GetValue(methodCall.Arguments[0]);
        var propertyArg = methodCall.Arguments[1];

        if (propertyArg is MemberExpression memberExpr && collection is IEnumerable enumerable)
        {
            return FormatInClause(GetMemberPath(memberExpr), enumerable);
        }

        return null;
    }

    private static string? TryParseInstanceContains(MethodCallExpression methodCall)
    {
        var collection = GetValue(methodCall.Object!);
        var propertyArg = methodCall.Arguments[0];

        if (propertyArg is MemberExpression memberExpr && collection is IEnumerable enumerable)
        {
            return FormatInClause(GetMemberPath(memberExpr), enumerable);
        }

        return null;
    }

    private static string GetStringExpressionPath(Expression expression) => expression switch
    {
        MemberExpression member => GetMemberPath(member),
        MethodCallExpression nestedMethodCall => ParseMethodCallExpression(nestedMethodCall),
        UnaryExpression unary when unary.Operand is MemberExpression unaryMember => GetMemberPath(unaryMember),
        _ => throw new NotSupportedException($"Expression type {expression.GetType().Name} is not supported for string operations")
    };

    private static string FormatInClause(string propertyPath, IEnumerable values)
    {
        var formattedValues = new List<string>();
        foreach (var value in values)
        {
            formattedValues.Add(FormatValue(value));
        }

        if (formattedValues.Count == 0)
        {
            return "false";
        }

        return $"{propertyPath} in ({string.Join(",", formattedValues)})";
    }

    /// <summary>
    /// Builds the slash-separated OData property path from a <see cref="MemberExpression"/>.
    /// **PATCHED**: each segment is resolved through <see cref="ResolveJsonName"/> instead of
    /// reading <c>MemberInfo.Name</c> directly, so <see cref="JsonPropertyNameAttribute"/> is honoured.
    /// </summary>
    private static string GetMemberPath(MemberExpression member)
    {
        var pathStack = new Stack<string>();
        Expression? current = member;

        while (current is MemberExpression memberExpr)
        {
            pathStack.Push(ResolveJsonName(memberExpr.Member)); // patched
            current = memberExpr.Expression;
        }

        return pathStack.Count switch
        {
            0 => string.Empty,
            1 => pathStack.Pop(),
            _ => string.Join("/", pathStack)
        };
    }

    private static object? GetValue(Expression expression)
    {
        // Unwrap Convert nodes (compiler-inserted boxing) before pattern-matching on the
        // operand. Otherwise a closure-captured array wrapped in Convert(_, object) would
        // skip the reflection fast path and hit Expression.Compile() which is broken on
        // .NET 10 preview for some shapes.
        while (expression is UnaryExpression unary
            && (unary.NodeType == ExpressionType.Convert || unary.NodeType == ExpressionType.ConvertChecked))
        {
            expression = unary.Operand;
        }

        return expression switch
        {
            ConstantExpression constant => constant.Value,
            MemberExpression member => GetMemberValue(member),
            _ => EvaluateExpression(expression)
        };
    }

    private static object? GetMemberValue(MemberExpression member)
    {
        if (TryEvaluateWithReflection(member, out var result))
        {
            return result;
        }

        var objectMember = Expression.Convert(member, typeof(object));
        var getterLambda = Expression.Lambda<Func<object>>(objectMember);
        var getter = getterLambda.Compile();
        return getter();
    }

    private static string FormatValue(object? value) => value switch
    {
        null => "null",
        string s => $"'{s.Replace("'", "''")}'",
        bool b => b.ToString().ToLowerInvariant(),
        DateTime dt => FormatDateTime(dt),
        DateTimeOffset dto => $"{dto.UtcDateTime:yyyy-MM-ddTHH:mm:ssZ}",
        Guid g => g.ToString(),
        Enum e => $"'{e}'",
        _ => value.ToString() ?? "null"
    };

    private static string FormatDateTime(DateTime dt)
    {
        var utc = dt.Kind switch
        {
            DateTimeKind.Utc => dt,
            DateTimeKind.Local => dt.ToUniversalTime(),
            DateTimeKind.Unspecified => DateTime.SpecifyKind(dt, DateTimeKind.Utc),
            _ => dt.ToUniversalTime()
        };
        return $"{utc:yyyy-MM-ddTHH:mm:ssZ}";
    }

    // -----------------------------------------------------------------------------------------
    // Select parsing
    // -----------------------------------------------------------------------------------------

    private static List<string> GetMemberNames(Expression body)
    {
        if (body is NewExpression newExpr)
        {
            var results = new List<string>();
            foreach (var arg in newExpr.Arguments)
            {
                var memberPath = GetMemberPathFromExpression(arg);
                if (!string.IsNullOrEmpty(memberPath))
                {
                    var firstSegment = memberPath.Split('/')[0];
                    if (!results.Contains(firstSegment))
                    {
                        results.Add(firstSegment);
                    }
                }
            }

            return results;
        }

        if (body is MemberExpression member)
        {
            var memberPath = GetMemberPathFromExpression(member);
            var firstSegment = memberPath.Split('/')[0];
            return [firstSegment];
        }

        if (body is UnaryExpression unary && unary.Operand is MemberExpression unaryMember)
        {
            var memberPath = GetMemberPathFromExpression(unaryMember);
            var firstSegment = memberPath.Split('/')[0];
            return [firstSegment];
        }

        return [];
    }

    private static string GetMemberPathFromExpression(Expression expression)
    {
        if (expression is UnaryExpression unary && unary.NodeType == ExpressionType.Convert)
        {
            expression = unary.Operand;
        }

        if (expression is MemberExpression member)
        {
            return GetMemberPath(member);
        }

        return string.Empty;
    }

    // -----------------------------------------------------------------------------------------
    // Expand parsing
    // -----------------------------------------------------------------------------------------

    private static List<ExpandPathInfo> GetExpandMemberPathsWithInfo(Expression body)
    {
        var results = new List<ExpandPathInfo>();

        if (body is NewExpression newExpr)
        {
            foreach (var arg in newExpr.Arguments)
            {
                var pathInfo = GetExpandPathInfoFromExpression(arg);
                if (pathInfo is not null && !results.Any(r => r.Path == pathInfo.Path))
                {
                    results.Add(pathInfo);
                }
            }

            return results;
        }

        var singlePathInfo = GetExpandPathInfoFromExpression(body);
        if (singlePathInfo is not null)
        {
            results.Add(singlePathInfo);
        }

        return results;
    }

    /// <summary>
    /// Extracts expand path info from an expression. **PATCHED**: each segment uses
    /// <see cref="ResolveJsonName"/> so navigation paths honour <see cref="JsonPropertyNameAttribute"/>.
    /// </summary>
    private static ExpandPathInfo? GetExpandPathInfoFromExpression(Expression expression)
    {
        if (expression is UnaryExpression unary && unary.NodeType == ExpressionType.Convert)
        {
            expression = unary.Operand;
        }

        if (expression is not MemberExpression member)
        {
            return null;
        }

        var segments = new List<ExpandSegment>();
        Expression? current = member;

        while (current is MemberExpression memberExpr)
        {
            if (memberExpr.Member is PropertyInfo propInfo)
            {
                segments.Insert(0, new ExpandSegment(ResolveJsonName(propInfo), IsNavigationProperty(propInfo))); // patched
            }
            else
            {
                segments.Insert(0, new ExpandSegment(ResolveJsonName(memberExpr.Member), false)); // patched
            }

            current = memberExpr.Expression;
        }

        if (segments.Count == 0)
        {
            return null;
        }

        return new ExpandPathInfo(segments);
    }

    private static bool IsNavigationProperty(PropertyInfo property)
    {
        var propertyType = property.PropertyType;

        var underlyingType = Nullable.GetUnderlyingType(propertyType);
        if (underlyingType is not null)
        {
            propertyType = underlyingType;
        }

        if (propertyType.IsPrimitive)
        {
            return false;
        }

        if (propertyType == typeof(string) ||
            propertyType == typeof(DateTime) ||
            propertyType == typeof(DateTimeOffset) ||
            propertyType == typeof(DateOnly) ||
            propertyType == typeof(TimeOnly) ||
            propertyType == typeof(TimeSpan) ||
            propertyType == typeof(Guid) ||
            propertyType == typeof(decimal))
        {
            return false;
        }

        if (propertyType.IsEnum)
        {
            return false;
        }

        if (propertyType == typeof(byte[]))
        {
            return false;
        }

        if (typeof(IEnumerable).IsAssignableFrom(propertyType) && propertyType != typeof(string))
        {
            return true;
        }

        return propertyType.IsClass;
    }

    private static List<string> BuildNestedExpandFieldsWithInfo(List<ExpandPathInfo> pathInfos)
    {
        var rootNodes = new Dictionary<string, ExpandNode>();

        foreach (var pathInfo in pathInfos)
        {
            AddPathToTreeWithInfo(rootNodes, pathInfo.Segments, 0);
        }

        var result = new List<string>();
        foreach (var node in rootNodes.Values)
        {
            result.Add(node.ToODataSyntax());
        }

        return result;
    }

    private static void AddPathToTreeWithInfo(Dictionary<string, ExpandNode> nodes, List<ExpandSegment> segments, int index)
    {
        if (index >= segments.Count)
        {
            return;
        }

        var segment = segments[index];

        if (!nodes.TryGetValue(segment.Name, out var node))
        {
            node = new ExpandNode(segment.Name, segment.IsNavigation);
            nodes[segment.Name] = node;
        }

        AddPathToTreeWithInfo(node.Children, segments, index + 1);
    }

    private sealed record ExpandSegment(string Name, bool IsNavigation);

    private sealed class ExpandPathInfo
    {
        public List<ExpandSegment> Segments { get; }
        public string Path => string.Join("/", Segments.Select(s => s.Name));

        public ExpandPathInfo(List<ExpandSegment> segments)
        {
            Segments = segments;
        }
    }

    private sealed class ExpandNode
    {
        public string Name { get; }
        public bool IsNavigation { get; }
        public Dictionary<string, ExpandNode> Children { get; } = [];

        public ExpandNode(string name, bool isNavigation = true)
        {
            Name = name;
            IsNavigation = isNavigation;
        }

        public string ToODataSyntax()
        {
            if (Children.Count == 0)
            {
                return Name;
            }

            var navigationChildren = Children.Values.Where(c => c.IsNavigation).ToList();
            var scalarChildren = Children.Values.Where(c => !c.IsNavigation).ToList();

            var options = new List<string>();

            if (scalarChildren.Count > 0)
            {
                var selectFields = string.Join(",", scalarChildren.Select(c => c.Name));
                options.Add($"$select={selectFields}");
            }

            if (navigationChildren.Count > 0)
            {
                var expandFields = string.Join(",", navigationChildren.Select(c => c.ToODataSyntax()));
                options.Add($"$expand={expandFields}");
            }

            if (options.Count == 0)
            {
                return Name;
            }

            return $"{Name}({string.Join(";", options)})";
        }
    }

    // -----------------------------------------------------------------------------------------
    // Lambda parsing (Any/All) — derived from ODataQueryBuilder.LambdaParsing.cs
    // -----------------------------------------------------------------------------------------

    private static string? TryParseAnyAll(MethodCallExpression methodCall, string methodName)
    {
        var (collectionExpr, predicateLambda) = ExtractAnyAllComponents(methodCall);

        if (collectionExpr is null)
        {
            return null;
        }

        var collectionPath = GetCollectionPath(collectionExpr);
        if (string.IsNullOrEmpty(collectionPath))
        {
            return null;
        }

        var odataMethodName = methodName.ToLowerInvariant();

        if (predicateLambda is null)
        {
            return $"{collectionPath}/{odataMethodName}()";
        }

        var parameterName = predicateLambda.Parameters[0].Name ?? "x";
        var predicateBody = ParseLambdaBody(predicateLambda.Body, predicateLambda.Parameters[0], parameterName, parentOperator: null);

        return $"{collectionPath}/{odataMethodName}({parameterName}: {predicateBody})";
    }

    private static (Expression? collectionExpr, LambdaExpression? predicateLambda) ExtractAnyAllComponents(MethodCallExpression methodCall)
    {
        if (methodCall.Object is not null)
        {
            return ExtractInstanceAnyAllComponents(methodCall);
        }

        return methodCall.Arguments.Count >= 1
            ? ExtractStaticAnyAllComponents(methodCall)
            : (null, null);
    }

    private static (Expression?, LambdaExpression?) ExtractInstanceAnyAllComponents(MethodCallExpression methodCall)
    {
        var lambda = methodCall.Arguments.Count > 0 ? methodCall.Arguments[0] as LambdaExpression : null;
        return (methodCall.Object, lambda);
    }

    private static (Expression?, LambdaExpression?) ExtractStaticAnyAllComponents(MethodCallExpression methodCall)
    {
        var predicateLambda = methodCall.Arguments.Count > 1
            ? ExtractLambdaFromArgument(methodCall.Arguments[1])
            : null;
        return (methodCall.Arguments[0], predicateLambda);
    }

    private static LambdaExpression? ExtractLambdaFromArgument(Expression argument)
    {
        if (argument is UnaryExpression quote && quote.NodeType == ExpressionType.Quote)
        {
            return quote.Operand as LambdaExpression;
        }

        return argument as LambdaExpression;
    }

    private static string GetCollectionPath(Expression expression) => expression switch
    {
        MemberExpression member => GetMemberPath(member),
        MethodCallExpression mc when mc.Method.Name == "Select" => GetCollectionPath(mc.Arguments[0]),
        UnaryExpression unary => GetCollectionPath(unary.Operand),
        _ => string.Empty
    };

    private static string ParseLambdaBody(Expression body, ParameterExpression lambdaParam, string odataParamName, ExpressionType? parentOperator) => body switch
    {
        BinaryExpression binary => ParseLambdaBinaryExpression(binary, lambdaParam, odataParamName, parentOperator),
        MethodCallExpression methodCall => ParseLambdaMethodCall(methodCall, lambdaParam, odataParamName),
        UnaryExpression u when u.NodeType == ExpressionType.Not => $"not ({ParseLambdaBody(u.Operand, lambdaParam, odataParamName, parentOperator)})",
        UnaryExpression u when u.NodeType == ExpressionType.Convert => ParseLambdaBody(u.Operand, lambdaParam, odataParamName, parentOperator),
        MemberExpression member => GetLambdaMemberPath(member, lambdaParam, odataParamName),
        ConstantExpression constant => FormatValue(constant.Value),
        ParameterExpression param when param == lambdaParam => odataParamName,
        _ => throw new NotSupportedException($"Expression type {body.NodeType} is not supported in lambda body")
    };

    private static string ParseLambdaBinaryExpression(BinaryExpression binary, ParameterExpression lambdaParam, string odataParamName, ExpressionType? parentOperator)
    {
        var left = ParseLambdaBody(binary.Left, lambdaParam, odataParamName, binary.NodeType);
        var right = ParseLambdaBody(binary.Right, lambdaParam, odataParamName, binary.NodeType);

        var op = binary.NodeType switch
        {
            ExpressionType.Equal => "eq",
            ExpressionType.NotEqual => "ne",
            ExpressionType.GreaterThan => "gt",
            ExpressionType.GreaterThanOrEqual => "ge",
            ExpressionType.LessThan => "lt",
            ExpressionType.LessThanOrEqual => "le",
            ExpressionType.AndAlso => "and",
            ExpressionType.OrElse => "or",
            _ => throw new NotSupportedException($"Binary operator {binary.NodeType} is not supported in lambda")
        };

        var result = $"{left} {op} {right}";

        if (binary.NodeType == ExpressionType.OrElse && parentOperator == ExpressionType.AndAlso)
        {
            return $"({result})";
        }

        return result;
    }

    private static string ParseLambdaMethodCall(MethodCallExpression methodCall, ParameterExpression lambdaParam, string odataParamName)
    {
        var methodName = methodCall.Method.Name;

        if (methodCall.Object?.Type == typeof(string))
        {
            return ParseLambdaStringMethod(methodCall, lambdaParam, odataParamName, methodName);
        }

        if (methodCall.Method.DeclaringType == typeof(string) && methodName == "IsNullOrEmpty")
        {
            var argPath = GetLambdaExpressionPath(methodCall.Arguments[0], lambdaParam, odataParamName);
            return $"({argPath} eq null or {argPath} eq '')";
        }

        if (methodName is "Any" or "All")
        {
            var nestedResult = TryParseNestedAnyAll(methodCall, methodName, lambdaParam, odataParamName);
            if (nestedResult is not null)
            {
                return nestedResult;
            }
        }

        throw new NotSupportedException($"Method {methodName} is not supported in lambda body");
    }

    private static string ParseLambdaStringMethod(MethodCallExpression methodCall, ParameterExpression lambdaParam, string odataParamName, string methodName)
    {
        var stringPath = GetLambdaExpressionPath(methodCall.Object!, lambdaParam, odataParamName);
        return methodName switch
        {
            "Contains" => $"contains({stringPath},{FormatValue(GetValue(methodCall.Arguments[0]))})",
            "StartsWith" => $"startswith({stringPath},{FormatValue(GetValue(methodCall.Arguments[0]))})",
            "EndsWith" => $"endswith({stringPath},{FormatValue(GetValue(methodCall.Arguments[0]))})",
            "ToLower" => $"tolower({stringPath})",
            "ToUpper" => $"toupper({stringPath})",
            "Trim" => $"trim({stringPath})",
            _ => throw new NotSupportedException($"String method {methodName} is not supported in lambda")
        };
    }

    private static string? TryParseNestedAnyAll(MethodCallExpression methodCall, string methodName, ParameterExpression outerLambdaParam, string outerODataParamName)
    {
        var (collectionExpr, predicateLambda) = ExtractAnyAllComponents(methodCall);

        if (collectionExpr is null)
        {
            return null;
        }

        var collectionPath = GetLambdaExpressionPath(collectionExpr, outerLambdaParam, outerODataParamName);
        var odataMethodName = methodName.ToLowerInvariant();

        if (predicateLambda is null)
        {
            return $"{collectionPath}/{odataMethodName}()";
        }

        var innerParamName = predicateLambda.Parameters[0].Name ?? "x";
        var predicateBody = ParseLambdaBody(predicateLambda.Body, predicateLambda.Parameters[0], innerParamName, parentOperator: null);

        return $"{collectionPath}/{odataMethodName}({innerParamName}: {predicateBody})";
    }

    private static string GetLambdaExpressionPath(Expression expression, ParameterExpression lambdaParam, string odataParamName) => expression switch
    {
        ParameterExpression param when param == lambdaParam => odataParamName,
        MemberExpression member => GetLambdaMemberPath(member, lambdaParam, odataParamName),
        MethodCallExpression methodCall => ParseLambdaMethodCall(methodCall, lambdaParam, odataParamName),
        UnaryExpression u when u.NodeType == ExpressionType.Convert => GetLambdaExpressionPath(u.Operand, lambdaParam, odataParamName),
        _ => throw new NotSupportedException($"Expression type {expression.GetType().Name} is not supported in lambda path")
    };

    /// <summary>
    /// Walks the member chain inside a lambda body. **PATCHED**: each segment uses
    /// <see cref="ResolveJsonName"/> so lambda paths honour <see cref="JsonPropertyNameAttribute"/>.
    /// </summary>
    private static string GetLambdaMemberPath(MemberExpression member, ParameterExpression lambdaParam, string odataParamName)
    {
        var path = new List<string>();
        Expression? current = member;

        while (current is MemberExpression memberExpr)
        {
            path.Insert(0, ResolveJsonName(memberExpr.Member)); // patched
            current = memberExpr.Expression;
        }

        if (current == lambdaParam)
        {
            path.Insert(0, odataParamName);
        }
        else if (current is ConstantExpression)
        {
            return FormatValue(EvaluateExpression(member));
        }

        return string.Join("/", path);
    }
}
