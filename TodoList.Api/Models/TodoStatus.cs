using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace TodoList.Api.Models
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum TodoStatus
    {
        [EnumMember(Value = "pending")]
        Pending,

        [EnumMember(Value = "inProgress")]
        InProgress,

        [EnumMember(Value = "completed")]
        Completed
    }
}
