using Nivtropy.Domain.Model;

namespace Nivtropy.Domain.Services
{
    public class LeastSquaresNetworkAdjuster : INetworkAdjuster
    {
        public NetworkAdjustmentResult Adjust(LevelingNetwork network)
        {
            // TODO(MNK): Реализовать МНК-уравнивание сети:
            // - неизвестные: высоты точек
            // - наблюдения: dH
            // - уравнения: H_to - H_from = dH + v
            // - закрепления: известные реперы (constraints)
            // - веса: по длинам/классу/сигме (по проекту)
            return new NetworkAdjustmentResult(false, "Сетевое уравнивание (МНК) пока не реализовано.");
        }
    }
}
