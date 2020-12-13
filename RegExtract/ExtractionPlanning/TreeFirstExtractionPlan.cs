using RegExtract.RegexTools;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace RegExtract.ExtractionPlanning
{
    public class TreeFirstExtractionPlan<T> : ExtractionPlan<T>
    {
        public ExtractionPlanNode Plan { get; protected set; }
        public override T Extract(Match match)
        {
            return (T)Plan.Execute();
        }

        public static TreeFirstExtractionPlan<T> CreatePlan(Regex regex)
        {
            return null;
        }
    }
}
