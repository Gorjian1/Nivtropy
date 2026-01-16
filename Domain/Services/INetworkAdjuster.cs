using Nivtropy.Domain.Model;

namespace Nivtropy.Domain.Services
{
    public record NetworkAdjustmentResult(bool Performed, string Message);

    public interface INetworkAdjuster
    {
        NetworkAdjustmentResult Adjust(LevelingNetwork network);
    }
}
