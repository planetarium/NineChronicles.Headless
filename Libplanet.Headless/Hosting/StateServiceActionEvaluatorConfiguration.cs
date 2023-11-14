namespace Libplanet.Headless.Hosting
{
    public class StateServiceActionEvaluatorConfiguration : IActionEvaluatorConfiguration
    {
        public ActionEvaluatorType Type => ActionEvaluatorType.StateServiceActionEvaluator;
        public StateServiceConfiguration[] StateServices { get; set; } = null!;
        
        public string StateServiceDownloadPath { get; set; } = null!;
    }
}
