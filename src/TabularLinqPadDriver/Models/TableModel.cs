using System.Collections.Generic;

namespace TabularLinqPadDriver.Models
{
    internal class TableModel
    {
        public List<ColumnModel> Columns { get; set; }
        public string Identifier { get; set; }
        public List<MeasureModel> Measures { get; set; }
        public string Name { get; set; }
        public List<string> References { get; set; }
    }
}