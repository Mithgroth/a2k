using a2k.Shared.Models.Aspire;
using k8s.Models;

namespace a2k.Shared.Models.Kubernetes;

public static class Ingress
{
    public static V1Ingress Create(string solutionName, IEnumerable<(Resource Resource, int Port)> externalBindings)
    {
        return new V1Ingress
        {
            ApiVersion = "networking.k8s.io/v1",
            Kind = "Ingress",
            Metadata = new V1ObjectMeta
            {
                Name = $"{solutionName}-ingress",
                Annotations = new Dictionary<string, string>
                {
                    ["traefik.ingress.kubernetes.io/router.entrypoints"] = "web"
                }
            },
            Spec = new V1IngressSpec
            {
                IngressClassName = "traefik",
                Rules = externalBindings
                    .Select(binding => new V1IngressRule
                    {
                        Host = $"{binding.Resource.ResourceName}.{solutionName}.local",
                        Http = new V1HTTPIngressRuleValue
                        {
                            Paths =
                            [
                                new V1HTTPIngressPath
                                {
                                    //Path = $"/{binding.Resource.ResourceName}",
                                    Path = $"/",
                                    PathType = "Prefix",
                                    Backend = new V1IngressBackend
                                    {
                                        Service = new V1IngressServiceBackend
                                        {
                                            Name = $"{binding.Resource.ResourceName}-service",
                                            Port = new V1ServiceBackendPort { Number = binding.Port }
                                        }
                                    }
                                }
                            ]
                        }
                    })
                    .ToList()
            }
        };
    }
} 