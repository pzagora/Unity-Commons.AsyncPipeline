using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Commons.AsyncPipeline
{
    /// <summary>
    /// Entry point for a <see cref="AsyncPipeline{TStepId}"/> whose steps are identified by <see cref="string"/> — the common
    /// case. For a different step-id type, use <see cref="AsyncPipeline{TStepId}"/> directly (e.g. <c>AsyncPipeline&lt;MyEnum&gt;</c>).
    /// </summary>
    public static class AsyncPipeline
    {
        /// <inheritdoc cref="AsyncPipeline{TStepId}.Start(string)"/>
        public static AsyncPipeline<string> Start(string name = null)
            => AsyncPipeline<string>.Start(name);

        /// <inheritdoc cref="AsyncPipeline{TStepId}.Start(TStepId, AsyncPipeline{TStepId}.StepFunc, float?)"/>
        public static AsyncPipeline<string> Start(string id, AsyncPipeline<string>.StepFunc func, float? weight = null)
            => AsyncPipeline<string>.Start(id, func, weight);

        /// <inheritdoc cref="AsyncPipeline{TStepId}.StepFor(System.Func{System.IProgress{float}, System.Threading.CancellationToken, Cysharp.Threading.Tasks.UniTask})"/>
        public static AsyncPipeline<string>.StepFunc StepFor(Func<IProgress<float>, CancellationToken, UniTask> action)
            => AsyncPipeline<string>.StepFor(action);

        /// <inheritdoc cref="AsyncPipeline{TStepId}.StepFor(System.Func{System.IProgress{float}, System.Threading.CancellationToken, System.Threading.Tasks.Task})"/>
        public static AsyncPipeline<string>.StepFunc StepFor(Func<IProgress<float>, CancellationToken, Task> action)
            => AsyncPipeline<string>.StepFor(action);

        /// <inheritdoc cref="AsyncPipeline{TStepId}.StepFor(System.Action{System.IProgress{float}, System.Threading.CancellationToken})"/>
        public static AsyncPipeline<string>.StepFunc StepFor(Action<IProgress<float>, CancellationToken> action)
            => AsyncPipeline<string>.StepFor(action);
    }

    /// <summary>
    /// Executes a sequence of asynchronous steps with weighted progress, automatic logging, and optional user-defined handlers.
    /// </summary>
    /// <typeparam name="TStepId">The type used to identify each step.</typeparam>
    public class AsyncPipeline<TStepId>
    {
        private readonly string _name;
        private string _path;

        private readonly List<Step<TStepId>> _steps = new();

        // ReSharper disable once StaticMemberInGenericType
        private static readonly AsyncLocal<string> CurrentPath = new();

        private Action<TStepId> _onStepStart;
        private Action<TStepId> _onStepCanceled;
        private Action<TStepId> _onStepComplete;
        private Action<TStepId, Exception> _onStepError;

        private const string LOG_PATH = "{0} -> {1}";
        private const string LOG_STARTED_PIPELINE = "Started pipeline";
        private const string LOG_STARTED_STEP = "Started step: {0}";
        private const string LOG_FINISHED_STEP = "[{0:P2}] Finished step: {1}";
        private const string LOG_CANCELED_ON_STEP = "Canceled on step: {0}";
        private const string LOG_FAILED_ON_STEP = "Failed on step: {0}";
        private const string LOG_FINISHED_PIPELINE = "Finished pipeline";
        private const string LOG_EXCEPTION_WEIGH = "Sum of specified weights exceeds 1.0";

        private AsyncPipeline(string name = null)
        {
            _name = name ?? nameof(AsyncPipeline);
        }

        /// <summary>
        /// Defines start of the pipeline.
        /// </summary>
        /// <param name="name">Pipeline identifier</param>
        /// <returns>An instance of the pipeline.</returns>
        public static AsyncPipeline<TStepId> Start(string name = null)
            => new(name);

        /// <summary>
        /// Defines start of the pipeline along with the first step.
        /// </summary>
        /// <param name="id">Identifier of the step.</param>
        /// <param name="func">Asynchronous function of the step.</param>
        /// <param name="weight">
        /// Optional relative weight of the step in the progress calculation.
        /// If null, weight will be distributed automatically.</param>
        /// <returns>The pipeline instance.</returns>
        public static AsyncPipeline<TStepId> Start(TStepId id, StepFunc func, float? weight = null)
        {
            var pipeline = new AsyncPipeline<TStepId>();
            pipeline._steps.Add(new Step<TStepId>
            {
                id = id,
                func = func,
                weight = weight ?? NumericExtensions.Zero,
                weightSpecified = weight.HasValue
            });
            return pipeline;
        }

        /// <summary>
        /// Adds step to the pipeline.
        /// </summary>
        /// <param name="id">Identifier of the step.</param>
        /// <param name="func">Asynchronous function of the step.</param>
        /// <param name="weight">
        /// Optional relative weight of the step in the progress calculation.
        /// If null, weight will be distributed automatically.</param>
        /// <returns>The pipeline instance.</returns>
        public AsyncPipeline<TStepId> Then(TStepId id, StepFunc func, float? weight = null)
        {
            _steps.Add(new Step<TStepId>
            {
                id = id,
                func = func,
                weight = weight ?? NumericExtensions.Zero,
                weightSpecified = weight.HasValue
            });
            return this;
        }

        /// <summary>
        /// Creates a step from a <see cref="UniTask"/>-returning async method with no result.
        /// </summary>
        public static StepFunc StepFor(Func<IProgress<float>, CancellationToken, UniTask> action)
        {
            return async (stepProgress, ct) =>
            {
                ct.ThrowIfCancellationRequested();
                await action(stepProgress, ct);
                ct.ThrowIfCancellationRequested();
            };
        }

        /// <summary>
        /// Creates a step from a <see cref="Task"/>-returning async method with no result.
        /// </summary>
        public static StepFunc StepFor(Func<IProgress<float>, CancellationToken, Task> action)
        {
            return async (stepProgress, ct) =>
            {
                ct.ThrowIfCancellationRequested();
                await action(stepProgress, ct);
                ct.ThrowIfCancellationRequested();
            };
        }

        /// <summary>
        /// Creates a step from a synchronous method.
        /// </summary>
        public static StepFunc StepFor(Action<IProgress<float>, CancellationToken> action)
        {
            return (stepProgress, ct) =>
            {
                ct.ThrowIfCancellationRequested();
                action(stepProgress, ct);
                ct.ThrowIfCancellationRequested();
                return UniTask.CompletedTask;
            };
        }

        /// <summary>
        /// Attaches an additional callback invoked when a step starts. Default logging is always executed.
        /// </summary>
        public AsyncPipeline<TStepId> OnStepStart(Action<TStepId> handler)
        {
            _onStepStart += handler;
            return this;
        }

        /// <summary>
        /// Attaches an additional callback invoked when a step completes. Default logging is always executed.
        /// </summary>
        public AsyncPipeline<TStepId> OnStepCanceled(Action<TStepId> handler)
        {
            _onStepCanceled += handler;
            return this;
        }

        /// <summary>
        /// Attaches an additional callback invoked when a step completes. Default logging is always executed.
        /// </summary>
        public AsyncPipeline<TStepId> OnStepComplete(Action<TStepId> handler)
        {
            _onStepComplete += handler;
            return this;
        }

        /// <summary>
        /// Attaches an additional callback invoked when a step throws an exception. Default logging is always executed.
        /// </summary>
        public AsyncPipeline<TStepId> OnStepError(Action<TStepId, Exception> handler)
        {
            _onStepError += handler;
            return this;
        }

        /// <summary>
        /// Executes the pipeline.
        /// </summary>
        /// <param name="progress">Progress reporter that receives values in the range [0, 1].</param>
        /// <param name="ct">Cancellation token.</param>
        public async UniTask RunAsync(IProgress<float> progress, CancellationToken ct)
        {
            var parentPath = CurrentPath.Value;
            _path = string.IsNullOrEmpty(parentPath) ? _name : string.Format(LOG_PATH, parentPath, _name);
            CurrentPath.Value = _path;

            try
            {
                LogPipelineStart();

                var totalExplicit = _steps.Where(s => s.weightSpecified).Sum(s => s.weight);
                var unspecifiedCount = _steps.Count(s => !s.weightSpecified);

                if (totalExplicit > NumericExtensions.One)
                    throw new InvalidOperationException(LOG_EXCEPTION_WEIGH);

                var remaining = NumericExtensions.One - totalExplicit;
                var autoWeight = unspecifiedCount > NumericExtensions.Zero
                    ? remaining / unspecifiedCount
                    : NumericExtensions.Zero;

                foreach (var step in _steps)
                {
                    if (!step.weightSpecified)
                        step.weight = autoWeight;
                }

                float accumulated = NumericExtensions.Zero;

                foreach (var step in _steps)
                {
                    ct.ThrowIfCancellationRequested();

                    var stepWeight = step.weight;

                    try
                    {
                        LogStepStart(step.id);

                        await step.func(new ScopedProgress(progress, accumulated, accumulated + stepWeight), ct);

                        accumulated += stepWeight;

                        LogStepComplete(step.id, accumulated);
                    }
                    catch (OperationCanceledException)
                    {
                        LogStepCanceled(step.id);
                    }
                    catch (Exception ex)
                    {
                        LogStepError(step.id, ex);
                    }
                }

                progress?.Report(NumericExtensions.One);
                LogPipelineComplete();
            }
            finally
            {
                CurrentPath.Value = parentPath;
            }
        }

        /// <summary>
        /// Delegate for a single pipeline step.
        /// </summary>
        /// <param name="progress">Progress reporter for the step.</param>
        /// <param name="ct">Cancellation token.</param>
        public delegate UniTask StepFunc(IProgress<float> progress, CancellationToken ct);

        private void LogPipelineStart()
            => Report.Event(LOG_STARTED_PIPELINE, sourceName: _path);

        private void LogStepStart(TStepId id)
        {
            Report.Event(string.Format(LOG_STARTED_STEP, id), sourceName: _path);
            _onStepStart?.Invoke(id);
        }

        private void LogStepCanceled(TStepId id)
        {
            Report.Event(string.Format(LOG_CANCELED_ON_STEP, id), sourceName: _path);
            _onStepCanceled?.Invoke(id);
        }

        private void LogStepComplete(TStepId id, float progress)
        {
            Report.Event(string.Format(LOG_FINISHED_STEP, progress, id), true, sourceName: _path);
            _onStepComplete?.Invoke(id);
        }

        private void LogStepError(TStepId id, Exception ex)
        {
            Report.Event(string.Format(LOG_FAILED_ON_STEP, id), false, sourceName: _path);
            Report.Exception(ex, sourceName: _path);
            _onStepError?.Invoke(id, ex);
        }

        private void LogPipelineComplete()
            => Report.Event(LOG_FINISHED_PIPELINE, sourceName: _path);
    }
}
