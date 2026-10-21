# Unity Commons — Async Pipeline

`AsyncPipeline<TStepId>` — a fluent builder for running a sequence of asynchronous steps with **weighted progress**, cancellation, automatic logging and per-step callbacks.

Its own assembly (`Commons.AsyncPipeline`) that depends on [Unity Commons](https://github.com/pzagora/Unity-Commons) and [Unity Reporter](https://github.com/pzagora/Unity-Reporter), and runs on [UniTask](https://github.com/Cysharp/UniTask) — which is why it ships as a separate package rather than compiling into the Commons assembly (the core stays free of Reporter and UniTask).

## Installation

Add to `Packages/manifest.json` (its dependencies are declared in `package.json`):

```json
"com.zagoradev.unity.commons.asyncpipeline": "https://github.com/pzagora/Unity-Commons.AsyncPipeline.git"
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
- **Logging** — start, per-step, cancellation, failure and completion are reported through `Report` automatically; `OnStepStart/Canceled/Complete/Error` attach extra handlers without replacing it.
