namespace Nivtropy.Domain.Services;

using Nivtropy.Domain.Model;

public record NetworkAdjustmentResult(bool Performed, string Message);

public interface INetworkAdjuster
{
    NetworkAdjustmentResult Adjust(LevelingNetwork network);
}
