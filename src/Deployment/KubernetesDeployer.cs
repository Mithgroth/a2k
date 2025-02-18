using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Logging;

namespace a2k.Deployment;

internal sealed class KubernetesDeployer(KubernetesContext context, ResourceLoggerService loggerService)
{
    public async Task DeployAsync(IKubernetesResource resource, CancellationToken cancellationToken)
    {
        var logger = loggerService.GetLogger(resource);

        try
        {
            logger.LogInformation("Deploying {ResourceName} to Kubernetes namespace {Namespace}",
                                                             resource.Name,
                                                             context.Namespace);
            
            await resource.DeployAsync(context.Client, cancellationToken);

            logger.LogInformation("Successfully deployed {ResourceName}", resource.Name);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to deploy {ResourceName}", resource.Name);
            throw;
        }
    }
}
