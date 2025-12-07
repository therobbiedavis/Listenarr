using System;
using System.Threading.Tasks;
using Listenarr.Domain.Models;

namespace Listenarr.Api.Services
{
    public interface IMoveQueueService
    {
        Task<Guid> EnqueueMoveAsync(int audiobookId, string requestedPath, string? sourcePath = null);
        Task<Guid?> RequeueMoveAsync(Guid jobId);
        bool TryGetJob(Guid id, out MoveJob? job);
        void UpdateJobStatus(Guid id, string status, string? error = null);
        System.Threading.Channels.ChannelReader<MoveJob> Reader { get; }
    }
}

