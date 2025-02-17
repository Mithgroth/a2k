using a2k.Models;
using Aspire.Hosting.ApplicationModel;

namespace a2k.Provisioning;

//internal sealed class HelmProvisioner(ResourceLoggerService loggerService,
//                                      ResourceNotificationService notificationService)
//    : KubernetesResourceProvisioner<HelmChart>(loggerService, notificationService)
//{
//    protected override async Task ApplyResourceAsync(HelmChart resource, CancellationToken cancellationToken)
//    {
//        using var client = new k8s.Kubernetes(resource.KubernetesConfig);
//        await HelmInstallAsync(resource, client, cancellationToken);
//    }

//    private async Task HelmInstallAsync(HelmChart chart, k8s.Kubernetes client, CancellationToken ct)
//    {
//        // Implementation using Helm dotnet SDK or shelling out to helm CLI
//        // Placeholder for actual Helm installation logic
//    }
//}