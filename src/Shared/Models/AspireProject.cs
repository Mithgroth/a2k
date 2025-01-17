namespace a2k.Shared.Models;

/// <summary>
/// Represents a .NET project in Aspire environment
/// </summary>
public class AspireProject : AspireResource
{
    public AspireProject(string name, string csProjPath) : base(name)
    {
        Type = AspireResourceType.Project;
        CsProjPath = csProjPath;
    }

    public string CsProjPath { get; set; }
    public Dockerfile? Dockerfile { get; set; }
}