using Aspire.Hosting.ApplicationModel;

internal static class ResourceExtensions
{
    public static T? TrySelectParentResource<T>(this IResource resource) where T : IResource
        => resource switch
        {
            T ar => ar,
            IResourceWithParent rp => TrySelectParentResource<T>(rp.Parent),
            _ => default
        };
} 