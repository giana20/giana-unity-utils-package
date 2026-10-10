using System.Threading;
using UnityEngine;

namespace Giana.UnityUtils
{
    public interface IInitializeStart
    {
        int StartPriority { get; set; }
        Awaitable InitializeStart(CancellationToken ct);
    }
}
