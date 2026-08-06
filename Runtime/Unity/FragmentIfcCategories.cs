using System;

namespace FragmentsUnity
{
    internal static class FragmentIfcCategories
    {
        internal const string Project = "IFCPROJECT";
        internal const string Site = "IFCSITE";
        internal const string Building = "IFCBUILDING";
        internal const string BuildingStorey = "IFCBUILDINGSTOREY";
        internal const string Space = "IFCSPACE";
        internal const string OpeningElement = "IFCOPENINGELEMENT";
        internal const string Opening = "IFCOPENING";
        internal const string Annotation = "IFCANNOTATION";

        internal static bool Matches(string category, string expected)
        {
            return string.Equals(category, expected, StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsSpatialContainer(string category)
        {
            return Matches(category, Project)
                || Matches(category, Site)
                || Matches(category, Building)
                || Matches(category, BuildingStorey)
                || Matches(category, Space);
        }

        internal static bool IsInvisibleVolume(string category)
        {
            return Matches(category, Space)
                || Matches(category, Site)
                || Matches(category, Building)
                || Matches(category, OpeningElement)
                || Matches(category, Annotation)
                || Matches(category, Opening);
        }
    }
}
