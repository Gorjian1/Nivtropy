using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Nivtropy.Models
{
    public class TraverseRow : INotifyPropertyChanged
    {
        private string _lineName = "?";
        public string LineName
        {
            get => _lineName;
            set => SetField(ref _lineName, value);
        }

        private int _index;
        public int Index
        {
            get => _index;
            set => SetField(ref _index, value);
        }

        private string? _backCode;
        public string? BackCode
        {
            get => _backCode;
            set => SetField(ref _backCode, value);
        }

        private string? _foreCode;
        public string? ForeCode
        {
            get => _foreCode;
            set => SetField(ref _foreCode, value);
        }

        private double? _rb_m;
        public double? Rb_m
        {
            get => _rb_m;
            set
            {
                if (SetField(ref _rb_m, value))
                {
                    OnPropertyChanged(nameof(DeltaH));
                }
            }
        }

        private double? _rf_m;
        public double? Rf_m
        {
            get => _rf_m;
            set
            {
                if (SetField(ref _rf_m, value))
                {
                    OnPropertyChanged(nameof(DeltaH));
                }
            }
        }

        public double? DeltaH => (Rb_m.HasValue && Rf_m.HasValue) ? Rb_m - Rf_m : null;

        private double? _hdBack_m;
        public double? HdBack_m
        {
            get => _hdBack_m;
            set
            {
                if (SetField(ref _hdBack_m, value))
                {
                    OnPropertyChanged(nameof(HdImbalance_m));
                }
            }
        }

        private double? _hdFore_m;
        public double? HdFore_m
        {
            get => _hdFore_m;
            set
            {
                if (SetField(ref _hdFore_m, value))
                {
                    OnPropertyChanged(nameof(HdImbalance_m));
                }
            }
        }

        public double? HdImbalance_m => (HdBack_m.HasValue && HdFore_m.HasValue)
            ? Math.Abs(HdBack_m.Value - HdFore_m.Value)
            : null;

        private bool? _hdWithinTolerance;
        public bool? HdWithinTolerance
        {
            get => _hdWithinTolerance;
            set => SetField(ref _hdWithinTolerance, value);
        }

        private string _statusNote = string.Empty;
        public string StatusNote
        {
            get => _statusNote;
            set => SetField(ref _statusNote, value);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}
