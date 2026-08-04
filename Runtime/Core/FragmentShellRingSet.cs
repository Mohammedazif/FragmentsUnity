using System;

namespace FragmentsUnity
{
    /// <summary>Uniform accessor over regular (ushort) and BIG (uint) shell ring tables.</summary>
    internal readonly struct FragmentShellRingSet
    {
        private readonly Schema.Shell _shell;
        private readonly FragmentShellRingKind _kind;

        private FragmentShellRingSet(Schema.Shell shell, FragmentShellRingKind kind)
        {
            _shell = shell;
            _kind = kind;
        }

        public static FragmentShellRingSet ForProfiles(Schema.Shell shell, bool isBigShell)
        {
            return new FragmentShellRingSet(
                shell, isBigShell ? FragmentShellRingKind.BigProfiles : FragmentShellRingKind.Profiles);
        }

        public static FragmentShellRingSet ForHoles(Schema.Shell shell, bool isBigShell)
        {
            return new FragmentShellRingSet(
                shell, isBigShell ? FragmentShellRingKind.BigHoles : FragmentShellRingKind.Holes);
        }

        public int RingCount => _kind switch
        {
            FragmentShellRingKind.Profiles => _shell.ProfilesLength,
            FragmentShellRingKind.Holes => _shell.HolesLength,
            FragmentShellRingKind.BigProfiles => _shell.BigProfilesLength,
            _ => _shell.BigHolesLength
        };

        public int GetIndexCount(int ringIndex) => _kind switch
        {
            FragmentShellRingKind.Profiles => _shell.Profiles(ringIndex).Value.IndicesLength,
            FragmentShellRingKind.Holes => _shell.Holes(ringIndex).Value.IndicesLength,
            FragmentShellRingKind.BigProfiles => _shell.BigProfiles(ringIndex).Value.IndicesLength,
            _ => _shell.BigHoles(ringIndex).Value.IndicesLength
        };

        public int GetRawIndex(int ringIndex, int vertexIndex) => _kind switch
        {
            FragmentShellRingKind.Profiles => _shell.Profiles(ringIndex).Value.Indices(vertexIndex),
            FragmentShellRingKind.Holes => _shell.Holes(ringIndex).Value.Indices(vertexIndex),
            FragmentShellRingKind.BigProfiles => (int)_shell.BigProfiles(ringIndex).Value.Indices(vertexIndex),
            _ => (int)_shell.BigHoles(ringIndex).Value.Indices(vertexIndex)
        };

        public uint GetProfileId(int ringIndex) => _kind switch
        {
            FragmentShellRingKind.Holes => _shell.Holes(ringIndex).Value.ProfileId,
            FragmentShellRingKind.BigHoles => _shell.BigHoles(ringIndex).Value.ProfileId,
            _ => throw new InvalidOperationException("Profile rings carry no profile id.")
        };
    }
}
