using Aspire.Hosting.ApplicationModel;

namespace a2k.Annotations;

public sealed class KubernetesAnnotation(string name, string value) : IResourceAnnotation
{
    public string Name { get; } = name;
    public string Value { get; } = value;
}