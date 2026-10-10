using System.Threading;
using UnityEngine;

namespace Giana.UnityUtils
{
    public interface IInitializeAwake
    {
        int AwakePriority { get; set; }
        Awaitable InitializeAwake(CancellationToken ct);
    }
}
