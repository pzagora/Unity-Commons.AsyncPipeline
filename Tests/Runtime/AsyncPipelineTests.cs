using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Pipeline = Commons.AsyncPipeline.AsyncPipeline;
using StringPipeline = Commons.AsyncPipeline.AsyncPipeline<string>;

namespace Commons.Tests
{
    public sealed class AsyncPipelineTests
    {
        private const string PIPELINE_NAME = "Test pipeline";
        private const string INNER_PIPELINE_NAME = "Inner pipeline";
        private const string FAILING_STEP = "Failing step";
        private const string FOLLOWING_STEP = "Following step";
        private const string FAILURE_MESSAGE = "Expected failure";
        private const string FAILURE_LOG_PATTERN = "InvalidOperationException: Expected failure";
        private const string EXPECTED_CANCELLATION_MESSAGE = "Expected the pipeline to propagate cancellation.";

        [Test]
        public async Task DefaultModeContinuesAfterFailure()
        {
            var followingStepRan = false;
            var pipeline = Pipeline
                .Create(PIPELINE_NAME)
                .Then(FAILING_STEP, ThrowFailure)
                .Then(FOLLOWING_STEP, CompleteFollowingStep);
            ExpectFailureLog();

            await pipeline.RunAsync(null, CancellationToken.None);

            Assert.IsTrue(followingStepRan);
            return;

            UniTask CompleteFollowingStep(IProgress<float> progress, CancellationToken ct)
            {
                followingStepRan = true;
                return UniTask.CompletedTask;
            }
        }

        [Test]
        public void FailFastStopsAndPropagatesException()
        {
            var followingStepRan = false;
            var pipeline = StringPipeline
                .Create(PIPELINE_NAME, failFast: true)
                .Then(FAILING_STEP, ThrowFailure)
                .Then(FOLLOWING_STEP, CompleteFollowingStep);
            ExpectFailureLog();

            var exception = Assert.ThrowsAsync<InvalidOperationException>(
                async () => await pipeline.RunAsync(null, CancellationToken.None)
            );

            Assert.AreEqual(FAILURE_MESSAGE, exception.Message);
            Assert.IsFalse(followingStepRan);
            return;

            UniTask CompleteFollowingStep(IProgress<float> progress, CancellationToken ct)
            {
                followingStepRan = true;
                return UniTask.CompletedTask;
            }
        }

        [Test]
        public async Task FailFastStopsAndPropagatesCancellation()
        {
            var followingStepRan = false;
            var canceledCallbackRan = false;
            var pipeline = Pipeline
                .Create(PIPELINE_NAME, failFast: true)
                .Then(FAILING_STEP, ThrowCancellation)
                .Then(FOLLOWING_STEP, CompleteFollowingStep)
                .OnStepCanceled(_ => canceledCallbackRan = true);

            try
            {
                await pipeline.RunAsync(null, CancellationToken.None);
                Assert.Fail(EXPECTED_CANCELLATION_MESSAGE);
            }
            catch (OperationCanceledException)
            {
            }

            Assert.IsTrue(canceledCallbackRan);
            Assert.IsFalse(followingStepRan);
            return;

            UniTask CompleteFollowingStep(IProgress<float> progress, CancellationToken ct)
            {
                followingStepRan = true;
                return UniTask.CompletedTask;
            }
        }

        [Test]
        public void NestedFailFastPipelinePropagatesFailureToParent()
        {
            var parentFollowingStepRan = false;
            var innerPipeline = Pipeline
                .Create(INNER_PIPELINE_NAME, failFast: true)
                .Then(FAILING_STEP, ThrowFailure);
            var parentPipeline = Pipeline
                .Create(PIPELINE_NAME, failFast: true)
                .Then(INNER_PIPELINE_NAME, Pipeline.StepFor(innerPipeline.RunAsync))
                .Then(FOLLOWING_STEP, CompleteFollowingStep);
            ExpectFailureLog();
            ExpectFailureLog();

            var exception = Assert.ThrowsAsync<InvalidOperationException>(
                async () => await parentPipeline.RunAsync(null, CancellationToken.None)
            );

            Assert.AreEqual(FAILURE_MESSAGE, exception.Message);
            Assert.IsFalse(parentFollowingStepRan);
            return;

            UniTask CompleteFollowingStep(IProgress<float> progress, CancellationToken ct)
            {
                parentFollowingStepRan = true;
                return UniTask.CompletedTask;
            }
        }

        private static async UniTask ThrowFailure(IProgress<float> progress, CancellationToken ct)
        {
            await UniTask.CompletedTask;
            throw new InvalidOperationException(FAILURE_MESSAGE);
        }

        private static async UniTask ThrowCancellation(IProgress<float> progress, CancellationToken ct)
        {
            await UniTask.CompletedTask;
            throw new OperationCanceledException();
        }

        private static void ExpectFailureLog()
            => LogAssert.Expect(LogType.Exception, new Regex(FAILURE_LOG_PATTERN));
    }
}
