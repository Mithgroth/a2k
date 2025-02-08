using k8s;
using k8s.Models;

namespace a2k.Shared.Models.Kubernetes;

public class Traefik(k8s.Kubernetes k8sClient)
{
    private V1ServiceAccount ServiceAccount
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

    private V1ClusterRole ClusterRole
        => new()
        {
            ApiVersion = "rbac.authorization.k8s.io/v1",
            Kind = "ClusterRole",
            Metadata = new V1ObjectMeta
            {
                Name = "traefik-role"
            },
            Rules =
            [
                // Allow access to services, endpoints, secrets
                new()
                {
                    ApiGroups = [""],
                    Resources = ["services", "endpoints", "secrets"],
                    Verbs = ["get", "list", "watch"]
                },
                // Allow access to ingress resources
                new()
                {
                    ApiGroups = ["networking.k8s.io"],
                    Resources = ["ingresses"],
                    Verbs = ["get", "list", "watch"]
                },
                // Allow access to ingressclasses
                new()
                {
                    ApiGroups = ["networking.k8s.io"],
                    Resources = ["ingressclasses"],
                    Verbs = ["get", "list", "watch"]
                },
                // Allow access to endpointslices
                new()
                {
                    ApiGroups = ["discovery.k8s.io"],
                    Resources = ["endpointslices"],
                    Verbs = ["get", "list", "watch"]
                },
                // Allow access to nodes
                new()
                {
                    ApiGroups = [""],
                    Resources = ["nodes"],
                    Verbs = ["get", "list", "watch"]
                }
            ]
        };

    private V1ClusterRoleBinding ClusterRoleBinding
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

    private V1Deployment Deployment
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

    private V1IngressClass IngressClass
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

    private V1Service Service
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
            Ports =
            [
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
            ],
            Type = "NodePort" // Use LoadBalancer if running on cloud
        }
    };

    public async Task Deploy()
    {
        await k8sClient.CreateNamespacedServiceAccountAsync(ServiceAccount, "kube-system");
        await k8sClient.CreateClusterRoleAsync(ClusterRole);
        await k8sClient.CreateClusterRoleBindingAsync(ClusterRoleBinding);
        await k8sClient.CreateNamespacedDeploymentAsync(Deployment, "kube-system");
        await k8sClient.CreateNamespacedServiceAsync(Service, "kube-system");
        await k8sClient.CreateIngressClassAsync(IngressClass);
    }
}
