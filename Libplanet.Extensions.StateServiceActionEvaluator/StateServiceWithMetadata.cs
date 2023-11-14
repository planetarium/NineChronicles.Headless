namespace Libplanet.Extensions.StateServiceActionEvaluator
{
    public struct StateServiceWithMetadata
    {
        public EvaluateRange Range { get; set; }
        public StateService StateService { get; set; }
        
        public string StateServiceDownloadPath { get; set; }
        
        public StateServiceWithMetadata(StateService stateService, EvaluateRange range, string stateServiceDownloadPath)
        {
            StateService = stateService;
            Range = range;
            StateServiceDownloadPath = stateServiceDownloadPath;
        }
    }

    public struct EvaluateRange
    {
        public long Start { get; set; }
        
        public long End { get; set; }
        
        public EvaluateRange(long start, long end)
        {
            Start = start;
            End = end;
        }
    }
}
