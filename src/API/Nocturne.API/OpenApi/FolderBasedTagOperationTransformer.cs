using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Nocturne.API.OpenApi;

/// <summary>
/// Microsoft OpenAPI operation transformer that derives tags from controller namespaces.
/// Mirrors the NSwag <see cref="FolderBasedTagOperationProcessor"/> for runtime Scalar docs.
/// </summary>
public sealed class FolderBasedTagOperationTransformer : IOpenApiOperationTransformer
{
    private const string ControllersPrefix = "Nocturne.API.Controllers.";

    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        var controllerType = (context.Description.ActionDescriptor as ControllerActionDescriptor)
            ?.ControllerTypeInfo.AsType();

        if (controllerType is null)
            return Task.CompletedTask;

        // Honour explicit [Tags] — skip convention logic.
        var tagsAttribute = controllerType
            .GetCustomAttributes(typeof(TagsAttribute), inherit: true)
            .FirstOrDefault() as TagsAttribute;

        if (tagsAttribute is not null)
            return Task.CompletedTask;

        var tag = DeriveTag(controllerType);

        operation.Tags.Clear();
        operation.Tags.Add(new OpenApiTagReference(tag));

        return Task.CompletedTask;
    }

    private static string DeriveTag(Type controllerType)
    {
        var ns = controllerType.Namespace ?? string.Empty;
        var controllerName = StripControllerSuffix(controllerType.Name);

        if (!ns.StartsWith(ControllersPrefix, StringComparison.Ordinal))
            return controllerName;

        var segment = ns[ControllersPrefix.Length..];

        // V1 / V2 / V3 → version tag
        if (segment is "V1" or "V2" or "V3")
            return segment;

        // Exactly V4 (no subfolder) → controller name
        if (segment == "V4")
            return controllerName;

        // V4.<Group>[.Deeper] → the <Group> segment
        if (segment.StartsWith("V4.", StringComparison.Ordinal))
        {
            var remainder = segment["V4.".Length..];
            var dotIndex = remainder.IndexOf('.');
            return dotIndex < 0 ? remainder : remainder[..dotIndex];
        }

        // Anything else at root → controller name
        return controllerName;
    }

    private static string StripControllerSuffix(string name) =>
        name.EndsWith("Controller", StringComparison.Ordinal)
            ? name[..^"Controller".Length]
            : name;
}
