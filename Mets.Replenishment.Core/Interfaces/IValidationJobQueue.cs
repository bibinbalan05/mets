using System;
using System.Threading;
using System.Threading.Tasks;

namespace Mets.Replenishment.Core.Interfaces;

public interface IValidationJobQueue
{
    ValueTask EnqueueJobAsync(Guid requestId);
    ValueTask<Guid> DequeueAsync(CancellationToken cancellationToken);
}
