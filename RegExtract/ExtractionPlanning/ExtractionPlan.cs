using RegExtract.ExtractionPlanning;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace RegExtract
{
    public abstract class ExtractionPlan<T>
    {
        public ExtractionPlanNode Plan { get; private set; }

        protected ExtractionPlan() { }

        abstract public T Extract(Match match);


    }
}
