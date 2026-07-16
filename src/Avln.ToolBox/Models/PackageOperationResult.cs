namespace Avln.ToolBox.Models;

public sealed record PackageOperationResult(bool IsSuccess, string Message)
{
    public static PackageOperationResult Success(string message) => new(true, message);

    public static PackageOperationResult Failure(string message) => new(false, message);
}
