using System;
using System.Collections.Generic;
using System.Linq;

namespace FragmentsUnity.Tests
{
    internal sealed class FragmentBuildLog
    {
        private readonly List<(FragmentImportSeverity Severity, string Message)> _entries =
            new List<(FragmentImportSeverity, string)>();

        internal void Record(FragmentImportSeverity severity, string message)
        {
            _entries.Add((severity, message));
        }

        internal string TextOf(FragmentImportSeverity severity)
        {
            return string.Join(
                Environment.NewLine,
                _entries.Where(entry => entry.Severity == severity).Select(entry => entry.Message));
        }
    }
}
