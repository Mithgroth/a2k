namespace a2k.Shared.Models;

public abstract class AspireResource(string Name)
{
    public string Name { get; } = Name;
    public AspireResourceType Type { get; set; } = AspireResourceType.Unknown;
}

public enum AspireResourceType
{
    Unknown,
    Project,
    Container,
    Parameter,
    Value
}
