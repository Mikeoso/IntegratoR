# ADR-0004: Configurable, chunked OData `$batch` writes

- **Status:** Accepted
- **Date:** 2026-07-02

## Context

Batch writes (`CreateBatchCommand<T>` / `UpdateBatchCommand<T>` / `DeleteBatchCommand<T>`) previously
went through `PanoramicData.OData.Client`'s changeset. That path has two limits IntegratoR consumers hit:

- **No composite / anonymous-key support.** PanoramicData ToStrings the key on every write path
  (re-verified empirically on 10.0.84), which is why single-entity composite-key writes already use a
  raw-`HttpClient` bypass in `ODataClientAdapter` — see [ADR-0003](0003-odata-expression-translator-fork.md)
  for the sibling "the upstream library doesn't model our key/wire shape" pattern.
- **Batch was implicitly all-or-nothing with no knobs.** No failure-mode choice, no large-dataset
  handling, and no per-item outcome for import error analysis. D365 F&O caps a single `$batch` at
  **200 operations** (owner-verified against live JFI; the Microsoft "service protection" doc's 5000 is
  a different limit), so any real bulk import already has to be split.

Consumers importing long datasets need two things the old path could not express: **choose rollback vs.
continue-on-error**, and **get a structured list of which rows failed and why**.

## Decision

Route **every** batch write through one hand-rolled `multipart/mixed` OData `$batch`
(`ODataBatchRequestBuilder` / `ODataBatchResponseParser`, OASIS OData v4.01 §11.7.7) — uniform across
create/update/delete and **every key shape**, including cross-company / no-`DataAreaId` / single-key
global entities. PanoramicData's changeset is dropped for writes.

- **`BatchFailureMode`** selects behaviour per call (`BatchOptions`) or per config
  (`ODataSettings.Batch.FailureMode`):
  - `Atomic` (**default**) — one all-or-nothing OData changeset per chunk.
  - `ContinueOnError` — operations applied independently, failures collected.
- **Auto-chunking** at `ODataSettings.Batch.MaxOperationsPerChunk` (**default 150**, validator-bounded
  **1..200** by D365's ceiling). `StopOnFirstFailedChunk` (default true) halts further chunks in `Atomic`.
- **`Result<BatchOutcome>`** (breaking) — success only if every item commits; a failure carries a
  `BatchIntegrationError` whose `BatchOutcome` lists per-item `Index` / `ChunkIndex` / `Key` / status /
  D365 error, retrievable via `result.GetError()`.
- The `$batch` POST rides the named `"ODataClient"` Polly policy (429/5xx) but is **not** wrapped in the
  operation retry — a processed batch must never be blindly re-POSTed.

## Consequences

- **Breaking (v3.0.0).** `IBatchService` / `IODataBatchService`, `ODataService<T>`, the three generic
  batch commands + handlers, the F&O `*sCommand` handlers, and `IODataClientAdapter.Batch*Async` all
  change to `Result<BatchOutcome>`; the `ODataService<T>` ctor gains an optional
  `IOptions<ODataSettings>?` (binary break for direct constructors, DI consumers unaffected). Signalled
  via `GitVersion.yml next-version: 3.0.0`.
- **Atomicity is per-chunk, not per-dataset.** A dataset larger than the chunk size spans several
  changesets; each commits or rolls back independently. This is inherent to D365's 200-op cap, not a
  choice — documented in `wiki/Known-Limitations.md`. Keep imports idempotent on the composite key.
- **`ContinueOnError` is per-item today.** The single-`$batch` transport (`Prefer:
  odata.continue-on-error`) is undocumented for D365 F&O; enabling it is deferred behind a live-JFI
  `$batch` smoke run that also confirms changeset atomicity end-to-end.
- One uniform write path replaces two (PanoramicData changeset + raw bypass), at the cost of owning the
  multipart emit/parse. Pinned by `ODataBatchWireTests`, `ODataClientAdapterAtomicBatchTests`, and
  `ODataServiceBatchChunkingTests`.
