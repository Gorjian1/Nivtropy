using System;
using System.Collections.ObjectModel;
using Nivtropy.Application.DTOs;
using Nivtropy.Application.Mappers;
using Nivtropy.Domain.Model;
using Nivtropy.Domain.Services;
using Nivtropy.Legacy.Adapters;
using Nivtropy.Models;
using Nivtropy.ViewModel.Base;

namespace Nivtropy.ViewModel
{
    /// <summary>
    /// ViewModel для работы с нивелирной сетью через новую архитектуру (DDD + граф).
    /// Proof-of-concept для демонстрации миграции на новую архитектуру.
    /// </summary>
    public class NetworkViewModel : ViewModelBase
    {
        private readonly IHeightPropagator _heightPropagator;
        private readonly IClosureDistributor _closureDistributor;
        private readonly INetworkMapper _networkMapper;

        private LevelingNetwork? _network;
        private NetworkAdapter? _adapter;

        public NetworkViewModel(
            IHeightPropagator heightPropagator,
            IClosureDistributor closureDistributor,
            INetworkMapper networkMapper)
        {
            _heightPropagator = heightPropagator;
            _closureDistributor = closureDistributor;
            _networkMapper = networkMapper;
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

        /// <summary>Создать новую пустую сеть</summary>
        public void CreateNewNetwork(string name = "Новый проект")
        {
            Network = new LevelingNetwork(name);
            RefreshUI();
        }

        /// <summary>Установить высоту репера</summary>
        public void SetBenchmarkHeight(string pointCode, double heightMeters)
        {
            if (_network == null) return;

            _network.SetBenchmarkHeight(
                new Domain.ValueObjects.PointCode(pointCode),
                Domain.ValueObjects.Height.Known(heightMeters));

            RefreshUI();
        }

        /// <summary>Вычислить высоты</summary>
        public void CalculateHeights()
        {
            if (_network == null) return;

            // 1. Вычисляем невязки и распределяем поправки
            foreach (var run in _network.Runs)
            {
                if (!run.IsActive) continue;

                // Вычисляем допуск (упрощённо: 10мм на корень из длины в км)
                var lengthKm = run.TotalLength.Kilometers;
                var toleranceMm = 10.0 * Math.Sqrt(lengthKm);

                run.CalculateClosure(toleranceMm);

                if (run.Closure?.IsWithinTolerance == true)
                {
                    _closureDistributor.DistributeClosureWithSections(run);
                }
            }

            // 2. Распространяем высоты от реперов
            _heightPropagator.PropagateHeights(_network);

            RefreshUI();
        }

        /// <summary>Обновить UI данные</summary>
        private void RefreshUI()
        {
            if (_network == null || _adapter == null)
            {
                Runs.Clear();
                Rows.Clear();
                Summary = null;
                return;
            }

            // Обновляем через адаптер (для обратной совместимости)
            Runs.Clear();
            foreach (var run in _adapter.GetLineSummaries())
                Runs.Add(run);

            Rows.Clear();
            foreach (var row in _adapter.GetAllTraverseRows())
                Rows.Add(row);

            // Обновляем DTO (новый подход)
            Summary = _networkMapper.ToSummaryDto(_network);
            OnPropertyChanged(nameof(Summary));
        }
    }
}
