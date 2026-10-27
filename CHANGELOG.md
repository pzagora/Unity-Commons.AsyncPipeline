# Changelog

All notable changes to this package are documented here. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and this package adheres to Semantic Versioning.

## [2.0.0]

### Changed
- **BREAKING:** No longer its own assembly. `Runtime/` now carries a `.asmref` into the `Commons` assembly (like Commons.Pooling and Commons.Services), so remove `Commons.AsyncPipeline` from your `.asmdef` references; the namespace stays `Commons.AsyncPipeline`.
- Dropped the dependency on Unity Reporter. Pipeline logging goes through `CommonsLog` (Unity Commons 2.1.0+), which uses Reporter when it is in the project and the Unity console otherwise.

## [1.0.0]

### Added
- Initial release, extracted from the game's `Frontend` assembly: `AsyncPipeline<TStepId>` — fluent sequential async steps with weighted progress, cancellation, automatic `Report` logging and per-step callbacks, plus the static `ForAction` / `ForSyncAction` step factories.

### Changed
- Converted from `System.Threading.Tasks.Task` to **UniTask**. `AsyncPipeline.RunAsync` and `StepFunc` now return `UniTask`; `ForAction` has both `UniTask`- and `Task`-returning overloads, so existing `Task` step methods keep working.
- Folded the step factories (`ForAction` / `ForSyncAction`) into `AsyncPipeline<TStepId>` as static methods (previously a separate `AsyncPipelineStep` helper class).
- Extracted the nested `Step` and `ScopedProgress` types into their own files as `internal` classes (one type per file).
