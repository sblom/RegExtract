using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace RegExtract.ExtractionPlanning
{
    public class TypeFirstExtractionPlanner<T> : ExtractionPlan<T>
    {
        public override T Extract(Match match)
        {
            throw new NotImplementedException();
        }
    }
}
