using Aspire.Hosting.ApplicationModel;

internal sealed class KubernetesAnnotation(string name, string value) : IResourceAnnotation
{
    public string Name { get; } = name;
    public string Value { get; } = value;
} 