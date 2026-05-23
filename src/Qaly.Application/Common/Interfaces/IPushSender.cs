using System.Threading;
using System.Threading.Tasks;

namespace Qaly.Application.Common.Interfaces
{
    public interface IPushSender
    {
        Task SendAsync(System.Guid userId, string title, string message, object? data = null, CancellationToken ct = default);
    }
}
