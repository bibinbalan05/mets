using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Mets.Replenishment.Api.Background;

public class ValidationJobQueue
{
    private readonly Channel<Guid> _queue;

    public ValidationJobQueue()
    {
        _queue = Channel.CreateUnbounded<Guid>();
    }

    public async ValueTask EnqueueJobAsync(Guid requestId)
    {
        await _queue.Writer.WriteAsync(requestId);
    }

    public async ValueTask<Guid> DequeueAsync(CancellationToken cancellationToken)
    {
        return await _queue.Reader.ReadAsync(cancellationToken);
    }
}
