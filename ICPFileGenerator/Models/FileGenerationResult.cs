namespace ICPFileGenerator.Models;

public class FileGenerationResult
{
    public bool Success { get; init; }

    public string? OutputFilePath { get; init; }

    public string? ErrorMessage { get; init; }

    public static FileGenerationResult Ok(string outputFilePath) =>
        new() { Success = true, OutputFilePath = outputFilePath };

    public static FileGenerationResult Fail(string errorMessage) =>
        new() { Success = false, ErrorMessage = errorMessage };
}
