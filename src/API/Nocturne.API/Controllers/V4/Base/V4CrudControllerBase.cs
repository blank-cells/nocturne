using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OpenApi.Remote.Attributes;
using Nocturne.Core.Contracts.V4.Repositories;
using Nocturne.Core.Models.V4;

namespace Nocturne.API.Controllers.V4.Base;

public abstract class V4CrudControllerBase<TModel, TCreateRequest, TUpdateRequest, TRepository>(TRepository repository)
    : V4ReadOnlyControllerBase<TModel, TRepository>(repository)
    where TModel : class, IV4Record
    where TCreateRequest : class
    where TUpdateRequest : class
    where TRepository : IV4Repository<TModel>
{
    protected abstract TModel MapCreateToModel(TCreateRequest request);
    protected abstract TModel MapUpdateToModel(Guid id, TUpdateRequest request, TModel existing);

    /// <summary>Creates a new record and returns it with a `Location` header pointing to the created resource.</summary>
    /// <param name="request">The data used to create the record.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <remarks>
    /// `Timestamp` must be set on the mapped model; requests that resolve to a default timestamp are rejected with `400 Bad Request`.
    ///
    /// On success, responds with `201 Created` and a `Location` header containing the URL of the newly created record.
    /// </remarks>
    [HttpPost]
    [RemoteForm]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public virtual async Task<ActionResult<TModel>> Create([FromBody] TCreateRequest request, CancellationToken ct = default)
    {
        var model = MapCreateToModel(request);

        if (model.Timestamp == default)
            return Problem(detail: "Timestamp must be set", statusCode: 400, title: "Bad Request");

        var created = await Repository.CreateAsync(model, ct);
        created = await OnAfterCreateAsync(created, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>Updates an existing record by ID and returns the updated record.</summary>
    /// <param name="id">The unique identifier of the record to update.</param>
    /// <param name="request">The data to apply to the existing record.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <remarks>
    /// Returns `404 Not Found` if no record with the given <paramref name="id"/> exists.
    ///
    /// `Timestamp` must be set on the mapped model; requests that resolve to a default timestamp are rejected with `400 Bad Request`.
    /// </remarks>
    [HttpPut("{id:guid}")]
    [RemoteForm]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public virtual async Task<ActionResult<TModel>> Update(Guid id, [FromBody] TUpdateRequest request, CancellationToken ct = default)
    {
        var existing = await Repository.GetByIdAsync(id, ct);
        if (existing is null)
            return NotFound();

        var model = MapUpdateToModel(id, request, existing);

        if (model.Timestamp == default)
            return Problem(detail: "Timestamp must be set", statusCode: 400, title: "Bad Request");

        try
        {
            var updated = await Repository.UpdateAsync(id, model, ct);
            return Ok(updated);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>Deletes a record by ID.</summary>
    /// <param name="id">The unique identifier of the record to delete.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <remarks>Returns `204 No Content` on success, or `404 Not Found` if no record with the given <paramref name="id"/> exists.</remarks>
    [HttpDelete("{id:guid}")]
    [RemoteCommand]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public virtual async Task<ActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        try
        {
            await Repository.DeleteAsync(id, ct);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    protected virtual Task<TModel> OnAfterCreateAsync(TModel created, CancellationToken ct) => Task.FromResult(created);
}
