using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace TodoList.Api.Models
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum TodoPriority
    {
        [EnumMember(Value = "low")]
        Low,

        [EnumMember(Value = "medium")]
        Medium,

        [EnumMember(Value = "high")]
        High
    }
}
