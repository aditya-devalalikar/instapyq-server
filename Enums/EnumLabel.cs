namespace pqy_server.Models
{
    public class EnumLabel
    {
        public int Id { get; set; }
        public string EnumType { get; set; } = null!;  // E.g. "Nature"
        public string EnumName { get; set; } = null!;  // E.g. "FundamentalApplied"
        public string DisplayLabel { get; set; } = null!; // E.g. "Fundamental Applied"
    }
}
