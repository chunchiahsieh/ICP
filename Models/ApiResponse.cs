using System.Text.Json.Serialization;

namespace ICP.Models;

public class ApiResponse<T>
{
    [JsonPropertyName("success")]
    public bool Success { get; init; }

    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;

    [JsonPropertyName("data")]
    public T? Data { get; init; }

    public static ApiResponse<T> Ok(T data, string message = "") =>
        new()
        {
            Success = true,
            Message = message,
            Data = data
        };

    public static ApiResponse<T> Fail(string message) =>
        new()
        {
            Success = false,
            Message = message,
            Data = default
        };
}
