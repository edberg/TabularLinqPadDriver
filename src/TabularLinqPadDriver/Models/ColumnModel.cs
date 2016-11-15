namespace TabularLinqPadDriver.Models
{
    public class ColumnModel
    {
        public string Identifier { get; internal set; }
        public bool IsKey { get; internal set; }
        public string Name { get; internal set; }
        public string Type { get; internal set; }
    }
}