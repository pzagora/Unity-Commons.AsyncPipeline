namespace Commons.AsyncPipeline
{
    internal class Step<TStepId>
    {
        public TStepId id;
        public AsyncPipeline<TStepId>.StepFunc func;
        public float weight;
        public bool weightSpecified;
    }
}
