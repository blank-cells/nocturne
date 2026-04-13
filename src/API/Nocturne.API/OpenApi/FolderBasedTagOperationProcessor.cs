using Microsoft.AspNetCore.Mvc;
using NSwag.Generation.Processors;
using NSwag.Generation.Processors.Contexts;

namespace Nocturne.API.OpenApi;

/// <summary>
/// Derives an OpenAPI tag from the controller's namespace and name.
/// Explicit <see cref="TagsAttribute"/> on the controller takes precedence.
/// </summary>
public sealed class FolderBasedTagOperationProcessor : IOperationProcessor
{
    private const string ControllersPrefix = "Nocturne.API.Controllers.";

    public bool Process(OperationProcessorContext context)
    {
        var controllerType = context.ControllerType;

        // Rule 1: honour explicit [Tags] attribute — skip convention logic entirely.
        var tagsAttribute = controllerType.GetCustomAttributes(typeof(TagsAttribute), inherit: true)
            .FirstOrDefault() as TagsAttribute;

        string tag;

        if (tagsAttribute is not null)
        {
            // Let the existing tags stand — nothing to override.
            return true;
        }
        else
        {
            tag = DeriveTag(controllerType);
        }

        context.OperationDescription.Operation.Tags.Clear();
        context.OperationDescription.Operation.Tags.Add(tag);

        return true;
    }

    private static string DeriveTag(Type controllerType)
    {
        var ns = controllerType.Namespace ?? string.Empty;
        var controllerName = StripControllerSuffix(controllerType.Name);

        if (!ns.StartsWith(ControllersPrefix, StringComparison.Ordinal))
        {
            // Fallback: anything outside the expected prefix uses controller name.
            return controllerName;
        }

        // The portion after "Nocturne.API.Controllers." e.g. "V4.Glucose" or "V1"
        var segment = ns[ControllersPrefix.Length..];

        // Rule 4: V1 / V2 / V3 namespaces → tag is the version string.
        if (segment is "V1" or "V2" or "V3")
        {
            return segment;
        }

        // Rule 2 & 3: V4 namespaces.
        if (segment == "V4")
        {
            // Rule 3: exactly "V4" (no subfolder) → controller name.
            return controllerName;
        }

        if (segment.StartsWith("V4.", StringComparison.Ordinal))
        {
            // Rule 2: "V4.<Group>" or "V4.<Group>.Deeper" → the <Group> segment.
            var remainder = segment["V4.".Length..];
            var dotIndex = remainder.IndexOf('.');
            return dotIndex < 0 ? remainder : remainder[..dotIndex];
        }

        // Rule 6: anything else at root (e.g. "Authentication", "Admin", …) → controller name.
        return controllerName;
    }

    private static string StripControllerSuffix(string name) =>
        name.EndsWith("Controller", StringComparison.Ordinal)
            ? name[..^"Controller".Length]
            : name;
}
