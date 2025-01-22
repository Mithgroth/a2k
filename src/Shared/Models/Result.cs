using Spectre.Console.Rendering;

namespace a2k.Shared.Models;

public record Result(Outcome Outcome, IRenderable[] Messages)
{
    public Result(Outcome Outcome, string ResourceName, Exception? Exception = null)
        : this(Outcome, [Outcome.ToMarkup(ResourceName, Exception)])
    {

    }
}

public enum Outcome
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