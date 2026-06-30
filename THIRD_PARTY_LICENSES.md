# Third-Party Licenses

This file lists the licenses of third-party software whose source code is incorporated
into IntegratoR (in addition to NuGet package dependencies, which carry their own
licenses through the package manager).

---

## PanoramicData.OData.Client

`IntegratoR.OData.Common.Filters.IntegratoRODataExpressionTranslator` is derived from
`ODataQueryBuilder.ExpressionParsing.cs` and `ODataQueryBuilder.LambdaParsing.cs` in
PanoramicData.OData.Client (https://github.com/panoramicdata/panoramicdata.odata.client).

The structural code began as a near-copy of the upstream parser. Each read of a
`MemberInfo.Name` (for entity property paths) is routed through a `ResolveJsonName` helper
that consults `[System.Text.Json.Serialization.JsonPropertyName]` before falling back to
the CLR member name, adding attribute-based property name mapping to the filter / select /
expand / orderby path resolution.

`IntegratoRODataExpressionTranslator` is a **permanent, owned D365 extension** of the
PanoramicData parser, not a temporary fork awaiting an upstream merge. The MIT attribution
below is retained, but the translator has diverged intentionally from upstream (D365 enum
literal handling, .NET 10 expression-tree workarounds, `[JsonPropertyName]` resolution, and
the composite-key write path) and is **not tracked for deletion**. Treat it as first-party
source maintained in this repository.

```
MIT License

Copyright (c) 2025 Panoramic Data Limited

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```
