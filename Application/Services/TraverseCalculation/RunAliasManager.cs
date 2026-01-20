using System;
using System.Collections.Generic;
using Nivtropy.Application.DTOs;

namespace Nivtropy.Application.Services.TraverseCalculation;

internal sealed class RunAliasManager
{
    private readonly Dictionary<(StationDto row, bool isBack), string> _aliasByRowSide;
    private readonly Dictionary<string, string> _aliasToOriginal;
    private readonly Dictionary<string, int> _occurrenceCount;
    private readonly Func<string, bool> _isAnchor;

    private string? _previousForeCode;
    private string? _previousForeAlias;

    public RunAliasManager(Func<string, bool> isAnchor)
    {
        _aliasByRowSide = new Dictionary<(StationDto row, bool isBack), string>(new AliasKeyComparer());
        _aliasToOriginal = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        _occurrenceCount = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        _isAnchor = isAnchor;
    }

    public string RegisterAlias(string code, bool reusePrevious)
    {
        if (_isAnchor(code))
        {
            _aliasToOriginal[code] = code;
            return code;
        }

        if (reusePrevious && _previousForeAlias != null &&
            string.Equals(_previousForeCode, code, StringComparison.OrdinalIgnoreCase))
        {
            _aliasToOriginal[_previousForeAlias] = code;
            return _previousForeAlias;
        }

        var next = _occurrenceCount.TryGetValue(code, out var count) ? count + 1 : 1;
        _occurrenceCount[code] = next;

        var alias = next == 1 ? code : $"{code} ({next})";
        _aliasToOriginal[alias] = code;
        return alias;
    }

    public void RegisterRowAlias(StationDto row, bool isBack, string alias)
    {
        _aliasByRowSide[(row, isBack)] = alias;

        if (!isBack)
        {
            var code = row.ForeCode;
            _previousForeCode = code;
            _previousForeAlias = alias;
        }
    }

    public void ResetPreviousFore()
    {
        _previousForeCode = null;
        _previousForeAlias = null;
    }

    public string? GetAlias(StationDto row, bool isBack)
    {
        return _aliasByRowSide.TryGetValue((row, isBack), out var value) ? value : null;
    }

    public bool IsCopyAlias(string alias)
    {
        return _aliasToOriginal.TryGetValue(alias, out var original)
            && !string.Equals(alias, original, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class AliasKeyComparer : IEqualityComparer<(StationDto row, bool isBack)>
    {
        public bool Equals((StationDto row, bool isBack) x, (StationDto row, bool isBack) y)
        {
            return ReferenceEquals(x.row, y.row) && x.isBack == y.isBack;
        }

        public int GetHashCode((StationDto row, bool isBack) obj)
        {
            return HashCode.Combine(obj.row, obj.isBack);
        }
    }
}
