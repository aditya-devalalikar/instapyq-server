using System.Text.Json.Serialization;

namespace pqy_server.Models.Generic
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ReorderItemType
    {
        Exam = 1,
        Subject = 2,
        Year = 3,
        Topic = 4
    }

    public class ReorderRequest
    {
        public ReorderItemType ItemType { get; set; }
        public int ItemId { get; set; }
        public int NewOrder { get; set; }
    }

    public interface IOrderable
    {
        int Id { get; }
        int Order { get; set; }
    }
}
