using System;
using UnityEngine;

namespace Commons.AsyncPipeline
{
    internal class ScopedProgress : IProgress<float>
    {
        private readonly IProgress<float> _outer;
        private readonly float _start;
        private readonly float _end;

        public ScopedProgress(IProgress<float> outer, float start, float end)
        {
            _outer = outer;
            _start = start;
            _end = end;
        }

        public void Report(float value)
        {
            var clamped = Mathf.Clamp01(value);
            var mapped = Mathf.Lerp(_start, _end, clamped);

            _outer?.Report(mapped);
        }
    }
}
