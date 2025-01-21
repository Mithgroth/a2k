using Spectre.Console.Rendering;

namespace a2k.Shared.Models;

public record Result(ResourceOperationResult OperationResult, IRenderable[] Messages);

public enum ResourceOperationResult
{
    Created,
    Exists,
    Replaced,
    Updated,
    Deleted,
    Missing,
    Failed,
    Succeeded,
}