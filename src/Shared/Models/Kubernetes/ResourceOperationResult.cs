namespace a2k.Shared.Models.Kubernetes;

public enum ResourceOperationResult
{
    Created,
    Exists,
    Replaced,
    Deleted,
    Missing,
    Failed,
}
