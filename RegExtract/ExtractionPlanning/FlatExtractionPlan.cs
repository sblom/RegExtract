using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace RegExtract
{
    public class FlatExtractionPlan<T> : ExtractionPlan<T>
    {
        internal override void InitializePlan(Regex regex)
        {
            throw new NotImplementedException();
        }
    }
}
