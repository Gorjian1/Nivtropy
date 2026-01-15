using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Nivtropy.Application.DTOs;
using Nivtropy.Application.Commands;
using Nivtropy.Application.Commands.Handlers;
using Nivtropy.Application.Queries;
using Nivtropy.Domain.Model;
using Nivtropy.Infrastructure.Persistence;
using Nivtropy.Legacy.Adapters;
using Nivtropy.Presentation.Models;
using Nivtropy.ViewModels.Base;

namespace Nivtropy.ViewModels
{
    /// <summary>
    /// ViewModel для работы с нивелирной сетью через новую архитектуру (DDD + граф).
    /// Фаза 2: Использует Commands/Queries/Handlers (Application Layer).
    /// </summary>
    public class NetworkViewModel : ViewModelBase
    {
        private readonly INetworkRepository _repository;
        private readonly CalculateHeightsHandler _calculateHandler;
        private readonly GetNetworkSummaryHandler _summaryHandler;

        private Guid _networkId;
        private LevelingNetwork? _network;
        private NetworkAdapter? _adapter;

        public NetworkViewModel(
            INetworkRepository repository,
            CalculateHeightsHandler calculateHandler,
            GetNetworkSummaryHandler summaryHandler)
        {
            _repository = repository;
            _calculateHandler = calculateHandler;
            _summaryHandler = summaryHandler;
        }

        /// <summary>Текущая нивелирная сеть</summary>
        public LevelingNetwork? Network
        {
            get => _network;
            private set
            {
                _network = value;
                _adapter = value != null ? new NetworkAdapter(value) : null;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsNetworkLoaded));
            }
        }

        /// <summary>Есть ли загруженная сеть</summary>
        public bool IsNetworkLoaded => _network != null;

        /// <summary>Ходы для отображения (через адаптер)</summary>
        public ObservableCollection<LineSummary> Runs { get; } = new();

        /// <summary>Станции для отображения (через адаптер)</summary>
        public ObservableCollection<TraverseRow> Rows { get; } = new();

        /// <summary>Сводка по сети (DTO)</summary>
        public NetworkSummaryDto? Summary { get; private set; }

        /// <summary>Невязки ходов</summary>
        public ObservableCollection<RunClosureDto> Closures { get; } = new();

        /// <summary>Создать новую сеть (Фаза 2: через репозиторий)</summary>
        public async Task CreateNewNetworkAsync(string name = "Новый проект")
        {
            Network = new LevelingNetwork(name);
            _networkId = Network.Id;
            await _repository.SaveAsync(Network);
            await RefreshUIAsync();
        }

        /// <summary>Установить высоту репера (Фаза 2)</summary>
        public async Task SetBenchmarkHeightAsync(string pointCode, double heightMeters)
        {
            if (_network == null) return;

            _network.SetBenchmarkHeight(
                new Domain.ValueObjects.PointCode(pointCode),
                Domain.ValueObjects.Height.Known(heightMeters));

            await _repository.SaveAsync(_network);
            await RefreshUIAsync();
        }

        /// <summary>Вычислить высоты (Фаза 2: через Handler)</summary>
        public async Task CalculateHeightsAsync()
        {
            if (_networkId == Guid.Empty) return;

            // Используем Handler (Application Layer)
            var result = await _calculateHandler.HandleAsync(
                new CalculateHeightsCommand(_networkId));

            // Обновляем невязки
            Closures.Clear();
            foreach (var closure in result.Closures)
                Closures.Add(closure);

            await RefreshUIAsync();
        }

        /// <summary>Обновить UI (Фаза 2: через Query Handler)</summary>
        private async Task RefreshUIAsync()
        {
            // Получаем актуальную сеть из репозитория
            _network = await _repository.GetByIdAsync(_networkId);
            _adapter = _network != null ? new NetworkAdapter(_network) : null;

            if (_network == null || _adapter == null)
            {
                Runs.Clear();
                Rows.Clear();
                Summary = null;
                return;
            }

            // Обновляем Legacy коллекции (для обратной совместимости)
            Runs.Clear();
            foreach (var run in _adapter.GetLineSummaries())
                Runs.Add(run);

            Rows.Clear();
            foreach (var row in _adapter.GetAllTraverseRows())
                Rows.Add(row);

            // Обновляем DTO через Query Handler
            Summary = await _summaryHandler.HandleAsync(
                new GetNetworkSummaryQuery(_networkId));
            OnPropertyChanged(nameof(Summary));
        }
    }
}
