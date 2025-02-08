using a2k.Shared.Models;
using Spectre.Console;

namespace a2k.Shared.Extensions;

public static class ResultExtensions
{
    public static void WriteToConsole(this Result result, TreeNode node)
    {
        node.AddNodes(result.Messages);
    }

    public static void WriteToConsole(this Result result, TreeNode node, LiveDisplayContext ctx)
    {
        result.WriteToConsole(node);
        ctx.Refresh();
    }
}