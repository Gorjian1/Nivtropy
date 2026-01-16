using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Nivtropy.Application.DTOs;
using Nivtropy.Application.Commands;
using Nivtropy.Application.Commands.Handlers;
using Nivtropy.Application.Enums;
using Nivtropy.Application.Queries;
using Nivtropy.Application.Mappers;
using Nivtropy.Domain.Model;
using Nivtropy.Infrastructure.Persistence;
using Nivtropy.Presentation.ViewModels.Base;

namespace Nivtropy.Presentation.ViewModels
{
    /// <summary>
    /// ViewModel для работы с нивелирной сетью через новую архитектуру (DDD + граф).
    /// Stage 8: Использует DTOs напрямую без Legacy адаптеров.
    /// </summary>
    public class NetworkViewModel : ViewModelBase
    {
        private readonly INetworkRepository _repository;
        private readonly CalculateHeightsHandler _calculateHandler;
        private readonly GetNetworkSummaryHandler _summaryHandler;
        private readonly INetworkMapper _mapper;

        private Guid _networkId;
        private LevelingNetwork? _network;

        public NetworkViewModel(
            INetworkRepository repository,
            CalculateHeightsHandler calculateHandler,
            GetNetworkSummaryHandler summaryHandler,
            INetworkMapper mapper)
        {
            _repository = repository;
            _calculateHandler = calculateHandler;
            _summaryHandler = summaryHandler;
            _mapper = mapper;
        }

        /// <summary>Текущая нивелирная сеть</summary>
        public LevelingNetwork? Network
        {
            get => _network;
            private set
            {
                _network = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsNetworkLoaded));
            }
        }

        /// <summary>Есть ли загруженная сеть</summary>
        public bool IsNetworkLoaded => _network != null;

        /// <summary>Ходы для отображения (DTOs)</summary>
        public ObservableCollection<RunSummaryDto> Runs { get; } = new();

        /// <summary>Наблюдения для отображения (DTOs)</summary>
        public ObservableCollection<ObservationDto> Observations { get; } = new();

        /// <summary>Сводка по сети (DTO)</summary>
        public NetworkSummaryDto? Summary { get; private set; }

        /// <summary>Невязки ходов</summary>
        public ObservableCollection<RunClosureDto> Closures { get; } = new();

        /// <summary>Создать новую сеть</summary>
        public async Task CreateNewNetworkAsync(string name = "Новый проект")
        {
            Network = new LevelingNetwork(name);
            _networkId = Network.Id;
            await _repository.SaveAsync(Network);
            await RefreshUIAsync();
        }

        /// <summary>Установить высоту репера</summary>
        public async Task SetBenchmarkHeightAsync(string pointCode, double heightMeters)
        {
            if (_network == null) return;

            _network.SetBenchmarkHeight(
                new Domain.ValueObjects.PointCode(pointCode),
                Domain.ValueObjects.Height.Known(heightMeters));

            await _repository.SaveAsync(_network);
            await RefreshUIAsync();
        }

        /// <summary>Вычислить высоты</summary>
        public async Task CalculateHeightsAsync()
        {
            if (_networkId == Guid.Empty) return;

            var result = await _calculateHandler.HandleAsync(
                new CalculateHeightsCommand(_networkId, AdjustmentMode.Local));

            Closures.Clear();
            foreach (var closure in result.Closures)
                Closures.Add(closure);

            await RefreshUIAsync();
        }

        /// <summary>Обновить UI через DTOs</summary>
        private async Task RefreshUIAsync()
        {
            _network = await _repository.GetByIdAsync(_networkId);

            if (_network == null)
            {
                Runs.Clear();
                Observations.Clear();
                Summary = null;
                return;
            }

            // Получаем данные через Query Handler (возвращает DTOs)
            Summary = await _summaryHandler.HandleAsync(
                new GetNetworkSummaryQuery(_networkId));
            OnPropertyChanged(nameof(Summary));

            // Обновляем ходы из Summary
            Runs.Clear();
            if (Summary?.Runs != null)
            {
                foreach (var run in Summary.Runs)
                    Runs.Add(run);
            }

            // Получаем наблюдения через Mapper
            Observations.Clear();
            foreach (var run in _network.Runs)
            {
                foreach (var observation in run.Observations)
                {
                    Observations.Add(_mapper.ToDto(observation));
                }
            }
        }
    }
}
