using k8s;
using k8s.Models;

namespace a2k.Shared.Models.Kubernetes;

public static class Traefik
{
    public static V1ServiceAccount CreateServiceAccount()
        => new()
        {
            ApiVersion = "v1",
            Kind = "ServiceAccount",
            Metadata = new V1ObjectMeta
            {
                Name = "traefik-account",
                NamespaceProperty = "kube-system"
            }
        };

    public static V1ClusterRole CreateClusterRole()
        => new()
        {
            ApiVersion = "rbac.authorization.k8s.io/v1",
            Kind = "ClusterRole",
            Metadata = new V1ObjectMeta
            {
                Name = "traefik-role"
            },
            Rules = new List<V1PolicyRule>
            {
                // Allow access to services, endpoints, secrets
                new()
                {
                    ApiGroups = new[] { "" },
                    Resources = new[] { "services", "endpoints", "secrets" },
                    Verbs = new[] { "get", "list", "watch" }
                },
                // Allow access to ingress resources
                new()
                {
                    ApiGroups = new[] { "networking.k8s.io" },
                    Resources = new[] { "ingresses" },
                    Verbs = new[] { "get", "list", "watch" }
                },
                // Allow access to ingressclasses
                new()
                {
                    ApiGroups = new[] { "networking.k8s.io" },
                    Resources = new[] { "ingressclasses" },
                    Verbs = new[] { "get", "list", "watch" }
                },
                // Allow access to endpointslices
                new()
                {
                    ApiGroups = new[] { "discovery.k8s.io" },
                    Resources = new[] { "endpointslices" },
                    Verbs = new[] { "get", "list", "watch" }
                },
                // Allow access to nodes
                new()
                {
                    ApiGroups = new[] { "" },
                    Resources = new[] { "nodes" },
                    Verbs = new[] { "get", "list", "watch" }
                }
            }
        };

    public static V1ClusterRoleBinding CreateClusterRoleBinding()
        => new()
        {
            ApiVersion = "rbac.authorization.k8s.io/v1",
            Kind = "ClusterRoleBinding",
            Metadata = new V1ObjectMeta
            {
                Name = "traefik-role-binding"
            },
            RoleRef = new V1RoleRef
            {
                ApiGroup = "rbac.authorization.k8s.io",
                Kind = "ClusterRole",
                Name = "traefik-role"
            },
            Subjects =
            [
                new Rbacv1Subject
                {
                    Kind = "ServiceAccount",
                    Name = "traefik-account",
                    NamespaceProperty = "kube-system"
                }
            ]
        };

    public static V1Deployment CreateDeployment()
        => new()
        {
            ApiVersion = "apps/v1",
            Kind = "Deployment",
            Metadata = new V1ObjectMeta
            {
                Name = "traefik-deployment",
                NamespaceProperty = "kube-system",
                Labels = new Dictionary<string, string>
                {
                    ["app"] = "traefik"
                }
            },
            Spec = new V1DeploymentSpec
            {
                Replicas = 1,
                Selector = new V1LabelSelector
                {
                    MatchLabels = new Dictionary<string, string>
                    {
                        ["app"] = "traefik"
                    }
                },
                Template = new V1PodTemplateSpec
                {
                    Metadata = new V1ObjectMeta
                    {
                        Labels = new Dictionary<string, string>
                        {
                            ["app"] = "traefik"
                        }
                    },
                    Spec = new V1PodSpec
                    {
                        ServiceAccountName = "traefik-account",
                        Containers =
                        [
                            new()
                            {
                                Name = "traefik",
                                Image = "traefik:v3.3",
                                Args =
                                [
                                    "--api.insecure",
                                    "--providers.kubernetesingress",
                                    "--entrypoints.web.address=:80",
                                    "--entrypoints.websecure.address=:443",
                                    "--entrypoints.dashboard.address=:8081"
                                ],
                                Ports =
                                [
                                    new()
                                    {
                                        Name = "web",
                                        ContainerPort = 80
                                    },
                                    new V1ContainerPort
                                    {
                                        Name = "dashboard",
                                        ContainerPort = 8080
                                    }
                                ]
                            }
                        ]
                    }
                }
            }
        };

    public static V1IngressClass CreateIngressClass()
        => new()
        {
            ApiVersion = "networking.k8s.io/v1",
            Kind = "IngressClass",
            Metadata = new V1ObjectMeta
            {
                Name = "traefik"
            },
            Spec = new V1IngressClassSpec
            {
                Controller = "traefik.io/ingress-controller"
            }
        };

    public static V1Service CreateTraefikService()
    => new()
    {
        ApiVersion = "v1",
        Kind = "Service",
        Metadata = new V1ObjectMeta
        {
            Name = "traefik-service",
            NamespaceProperty = "kube-system",
            Labels = new Dictionary<string, string>
            {
                ["app"] = "traefik"
            }
        },
        Spec = new V1ServiceSpec
        {
            Selector = new Dictionary<string, string>
            {
                ["app"] = "traefik"
            },
            Ports = new List<V1ServicePort>
            {
                new()
                {
                    Name = "web",
                    Protocol = "TCP",
                    Port = 80,
                    TargetPort = 80,
                    NodePort = 32080 // Specify a NodePort for local access
                },
                new()
                {
                    Name = "dashboard",
                    Protocol = "TCP",
                    Port = 8080,
                    TargetPort = 8080
                }
            },
            Type = "NodePort" // Use LoadBalancer if running on cloud
        }
    };

    public static async Task Deploy(k8s.Kubernetes k8sClient)
    {
        await k8sClient.CreateNamespacedServiceAccountAsync(CreateServiceAccount(), "kube-system");
        await k8sClient.CreateClusterRoleAsync(CreateClusterRole());
        await k8sClient.CreateClusterRoleBindingAsync(CreateClusterRoleBinding());
        await k8sClient.CreateNamespacedDeploymentAsync(CreateDeployment(), "kube-system");
        await k8sClient.CreateNamespacedServiceAsync(CreateTraefikService(), "kube-system");
        await k8sClient.CreateIngressClassAsync(CreateIngressClass());
    }
}
