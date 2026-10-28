# Unity Commons — Async Pipeline

`AsyncPipeline<TStepId>` — a fluent builder for running a sequence of asynchronous steps with **weighted progress**, cancellation, automatic logging and per-step callbacks.

**Not standalone**: `Runtime/` carries a `.asmref` pointing at the `Commons` assembly, so the code compiles **into** [Unity Commons](https://github.com/pzagora/Unity-Commons) (namespace `Commons.AsyncPipeline`) and needs no separate assembly reference. It runs on [UniTask](https://github.com/Cysharp/UniTask). Logs go through `CommonsLog`, so they use [Unity Reporter](https://github.com/pzagora/Unity-Reporter) when it is in the project and the Unity console otherwise — Reporter is optional.

## Installation

Install from the zagora.dev UPM registry — add the scoped registry (and OpenUPM, for UniTask) to `Packages/manifest.json`, then the package; its dependencies install automatically:

```json
"scopedRegistries": [
    { "name": "zagora.dev", "url": "https://upm.zagora.dev", "scopes": [ "com.zagoradev" ] },
    { "name": "OpenUPM", "url": "https://package.openupm.com", "scopes": [ "com.cysharp" ] }
],
"dependencies": {
  "com.zagoradev.unity.commons.asyncpipeline": "2.0.4"
}
```

Requires Unity `6000.0+`.

## Usage

```csharp
using Commons.AsyncPipeline;

// Step ids default to string via the non-generic entry point; use AsyncPipeline<MyEnum> for another id type.
var pipeline = AsyncPipeline
    .Start("Boot")
    .Then("Localization", AsyncPipeline.StepFor(_localization.Initialize))
    .Then("Scene",        AsyncPipeline.StepFor(LoadSceneAsync), weight: 0.5f)
    .OnStepError((id, ex) => /* extra handling; default logging still runs */);

await pipeline.RunAsync(progress, cancellationToken);
```

- **Step id type** — `AsyncPipeline.Start(...)` defaults ids to `string`; use the generic `AsyncPipeline<TStepId>.Start(...)` (e.g. `AsyncPipeline<MyEnum>`) for another id type.
- **Weighted progress** — each step contributes to a `[0, 1]` progress stream; unspecified weights are distributed evenly across the remaining budget, and the sum of explicit weights may not exceed `1.0`.
- **Nesting** — a pipeline is a step: `StepFor(inner.RunAsync)` composes pipelines, and progress paths nest in the logs (`Boot -> Inner`).
- **Steps** — build one with `AsyncPipeline.StepFor` (or `AsyncPipeline<TStepId>.StepFor` for a non-string id); it accepts a `UniTask`- or `Task`-returning function, or an `Action` for a synchronous step.
- **Logging** — start, per-step, cancellation, failure and completion are reported through `CommonsLog` automatically (Reporter when present); `OnStepStart/Canceled/Complete/Error` attach extra handlers without replacing it.
