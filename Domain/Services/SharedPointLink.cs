using System;
using System.Collections.Generic;
using System.Linq;

namespace Nivtropy.Domain.Services
{
    public sealed class SharedPointLink
    {
        public SharedPointLink(IEnumerable<int> runIndexes, bool isEnabled)
        {
            RunIndexes = runIndexes?.Distinct().ToArray() ?? Array.Empty<int>();
            IsEnabled = isEnabled;
        }

        public IReadOnlyList<int> RunIndexes { get; }

        public bool IsEnabled { get; }

        public bool IsUsedInRun(int runIndex) => RunIndexes.Contains(runIndex);
    }
}
