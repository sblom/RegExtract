using System;
using System.Collections.Generic;
using System.Text;

namespace RegExtract.ExtractionPlanning
{
    public record ExtractionPlanNode(string groupName, Type type, ExtractionPlanNode[] constructorNodes, ExtractionPlanNode[] propertyNodes)
    {
        internal virtual object? Execute()
        {
            throw new NotImplementedException();
        }
    }
}
