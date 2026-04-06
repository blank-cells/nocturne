using Nocturne.Core.Models;

namespace Nocturne.Core.Contracts;

public interface IBodyWeightService
{
    Task<IEnumerable<BodyWeight>> GetBodyWeightsAsync(
        int count = 10,
        int skip = 0,
        CancellationToken cancellationToken = default
    );

    Task<BodyWeight?> GetBodyWeightByIdAsync(string id, CancellationToken cancellationToken = default);

    Task<IEnumerable<BodyWeight>> CreateBodyWeightsAsync(
        IEnumerable<BodyWeight> bodyWeights,
        CancellationToken cancellationToken = default
    );

    Task<BodyWeight?> UpdateBodyWeightAsync(
        string id,
        BodyWeight bodyWeight,
        CancellationToken cancellationToken = default
    );

    Task<bool> DeleteBodyWeightAsync(string id, CancellationToken cancellationToken = default);
}
