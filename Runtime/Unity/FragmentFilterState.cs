using System;
using System.Collections.Generic;
using System.Globalization;

namespace FragmentsUnity
{
    /// <summary>Decides what the filter window lists and which elements each of its commands acts on.</summary>
    public sealed class FragmentFilterState
    {
        private const string NoModelStatus = "Select a Fragments model in the scene to filter it.";
        private const string WholeModelStatus = "Showing the whole model.";
        private const string HidingStatusFormat = "Hiding {0} elements.";

        private const string NoHierarchyWarning =
            "This model was built without per-element objects, so there is nothing to hide. "
            + "Re-import it in one of the hierarchy modes to filter it.";

        private const string MergedWarning =
            "This model was built in a merged mode, where a welded chunk hides as a unit, so filtering can only "
            + "hide whole chunks. Isolating a storey on the sample model left about a third of the out-of-storey "
            + "elements still drawn. Re-import per Body, per Element or Instanced to filter exactly.";

        private static readonly int[] NoLocalIds = Array.Empty<int>();

        private readonly List<FragmentFilterRow> _storeys = new List<FragmentFilterRow>();
        private readonly List<FragmentFilterRow> _categories = new List<FragmentFilterRow>();
        private readonly HashSet<int> _hiddenLocalIds = new HashSet<int>();
        private readonly List<int> _modelLocalIds = new List<int>();

        private FragmentModel _model;
        private FragmentVisibilityIndex _index;

        public FragmentModel Model
        {
            get { return _model; }
        }

        public bool HasModel
        {
            get { return _model != null; }
        }

        public IReadOnlyList<FragmentFilterRow> Storeys
        {
            get { return _storeys; }
        }

        public IReadOnlyList<FragmentFilterRow> Categories
        {
            get { return _categories; }
        }

        /// <summary>The storey the last isolate kept, or null when the view is not a storey isolation.</summary>
        public string IsolatedStorey { get; private set; }

        /// <summary>The category the last isolate kept, or null when the view is not a category isolation.</summary>
        public string IsolatedCategory { get; private set; }

        public int HiddenCount
        {
            get { return _hiddenLocalIds.Count; }
        }

        /// <summary>True when the build registered elements to hide; mirrors SupportsFiltering at FragmentsActor.cpp:1071.</summary>
        public bool SupportsFiltering
        {
            get { return _index != null && _index.HasEntries; }
        }

        /// <summary>True when an element hides on its own instead of taking a merged chunk with it.</summary>
        public bool SupportsElementFiltering
        {
            get { return _index != null && _index.SupportsElementFiltering; }
        }

        /// <summary>One line describing what is on screen; mirrors GetStatusText at SFragmentsFilterPanel.cpp:397.</summary>
        public string StatusMessage
        {
            get
            {
                if (_model == null)
                {
                    return NoModelStatus;
                }

                return _hiddenLocalIds.Count == 0
                    ? WholeModelStatus
                    : string.Format(CultureInfo.CurrentCulture, HidingStatusFormat, _hiddenLocalIds.Count);
            }
        }

        /// <summary>Why this model cannot be filtered element by element, or null when it can.</summary>
        public string WarningMessage
        {
            get
            {
                if (_model == null)
                {
                    return null;
                }
                if (!SupportsFiltering)
                {
                    return NoHierarchyWarning;
                }
                return SupportsElementFiltering ? null : MergedWarning;
            }
        }

        /// <summary>Points the state at the selected model, rebuilding the rows only when the model changed.</summary>
        public void SetModel(FragmentModel model)
        {
            if (_model == model)
            {
                return;
            }

            _model = model;
            IsolatedStorey = null;
            IsolatedCategory = null;
            _hiddenLocalIds.Clear();
            Refresh();
        }

        /// <summary>Re-reads the model's counts and the scene's current visibility; mirrors SFragmentsFilterPanel.cpp:237.</summary>
        public void Refresh()
        {
            _storeys.Clear();
            _categories.Clear();
            _modelLocalIds.Clear();
            _index = null;

            if (_model == null)
            {
                _hiddenLocalIds.Clear();
                return;
            }

            _index = _model.GetComponent<FragmentVisibilityIndex>();

            BuildRows(_model.GetStoreyCounts(), _storeys, _model.FindByStorey);
            BuildRows(_model.GetCategoryCounts(), _categories, _model.FindByCategory);

            foreach (FragmentItemMetadata item in _model.Data.Items)
            {
                if (item != null && item.LocalId >= 0)
                {
                    _modelLocalIds.Add(item.LocalId);
                }
            }

            SyncHiddenFromScene();
            RefreshRowVisibility();
        }

        /// <summary>Elements to keep when isolating one storey; mirrors SFragmentsFilterPanel.cpp:361.</summary>
        public IReadOnlyList<int> IsolateStorey(string storeyName)
        {
            IReadOnlyList<int> localIds = FindRowLocalIds(_storeys, storeyName);
            HideAllExcept(localIds);
            IsolatedStorey = storeyName;
            return localIds;
        }

        /// <summary>Elements to keep when isolating one category; mirrors SFragmentsFilterPanel.cpp:371.</summary>
        public IReadOnlyList<int> IsolateCategory(string category)
        {
            IReadOnlyList<int> localIds = FindRowLocalIds(_categories, category);
            HideAllExcept(localIds);
            IsolatedCategory = category;
            return localIds;
        }

        /// <summary>False while an attribute isolate would match nothing and so hide the whole model.</summary>
        public bool CanIsolateAttribute(string attributeName)
        {
            return _model != null && !string.IsNullOrEmpty(attributeName);
        }

        /// <summary>Elements to keep when isolating an attribute or property value; mirrors FragmentsActor.cpp:1228.</summary>
        public IReadOnlyList<int> IsolateAttribute(string attributeName, string attributeValue, bool exactMatch)
        {
            if (!CanIsolateAttribute(attributeName))
            {
                return NoLocalIds;
            }

            List<int> localIds = _model.FindByAttribute(attributeName, attributeValue, exactMatch);
            HideAllExcept(localIds);
            return localIds;
        }

        /// <summary>Elements to show or hide when a storey row is toggled; mirrors SFragmentsFilterPanel.cpp:338.</summary>
        public IReadOnlyList<int> ToggleStorey(string storeyName, bool visible)
        {
            return Toggle(FindRowLocalIds(_storeys, storeyName), visible);
        }

        /// <summary>Elements to show or hide when a category row is toggled; mirrors SFragmentsFilterPanel.cpp:338.</summary>
        public IReadOnlyList<int> ToggleCategory(string category, bool visible)
        {
            return Toggle(FindRowLocalIds(_categories, category), visible);
        }

        /// <summary>Brings every row back on screen; mirrors SFragmentsFilterPanel.cpp:379.</summary>
        public void ClearAll()
        {
            _hiddenLocalIds.Clear();
            IsolatedStorey = null;
            IsolatedCategory = null;
            RefreshRowVisibility();
        }

        private void BuildRows(
            Dictionary<string, int> counts, List<FragmentFilterRow> rows, Func<string, List<int>> findLocalIds)
        {
            foreach (KeyValuePair<string, int> pair in counts)
            {
                rows.Add(new FragmentFilterRow(pair.Key, pair.Value, findLocalIds(pair.Key)));
            }

            rows.Sort(CompareRows);
        }

        // Biggest first, then alphabetically; mirrors SFragmentsFilterPanel.cpp:39-42
        private static int CompareRows(FragmentFilterRow left, FragmentFilterRow right)
        {
            return left.Count != right.Count
                ? right.Count.CompareTo(left.Count)
                : string.CompareOrdinal(left.Name, right.Name);
        }

        // The scene may have been filtered from script or another window; mirrors SFragmentsFilterPanel.h:23
        private void SyncHiddenFromScene()
        {
            if (_index == null || !_index.HasEntries)
            {
                return;
            }

            _hiddenLocalIds.Clear();
            foreach (int localId in _modelLocalIds)
            {
                if (_index.IsHidden(localId))
                {
                    _hiddenLocalIds.Add(localId);
                }
            }
        }

        private IReadOnlyList<int> Toggle(IReadOnlyList<int> localIds, bool visible)
        {
            IsolatedStorey = null;
            IsolatedCategory = null;

            for (int index = 0; index < localIds.Count; index++)
            {
                if (visible)
                {
                    _hiddenLocalIds.Remove(localIds[index]);
                }
                else
                {
                    _hiddenLocalIds.Add(localIds[index]);
                }
            }

            RefreshRowVisibility();
            return localIds;
        }

        private void HideAllExcept(IReadOnlyList<int> keptLocalIds)
        {
            var kept = new HashSet<int>(keptLocalIds);

            _hiddenLocalIds.Clear();
            foreach (int localId in _modelLocalIds)
            {
                if (!kept.Contains(localId))
                {
                    _hiddenLocalIds.Add(localId);
                }
            }

            IsolatedStorey = null;
            IsolatedCategory = null;
            RefreshRowVisibility();
        }

        private void RefreshRowVisibility()
        {
            RefreshRowVisibility(_storeys);
            RefreshRowVisibility(_categories);
        }

        private void RefreshRowVisibility(List<FragmentFilterRow> rows)
        {
            foreach (FragmentFilterRow row in rows)
            {
                row.IsVisible = IsWhollyVisible(row.LocalIds);
            }
        }

        // A storey and a category overlap, so one toggle changes what both report; mirrors SFragmentsFilterPanel.cpp:357
        private bool IsWhollyVisible(IReadOnlyList<int> localIds)
        {
            for (int index = 0; index < localIds.Count; index++)
            {
                if (_hiddenLocalIds.Contains(localIds[index]))
                {
                    return false;
                }
            }
            return true;
        }

        private static IReadOnlyList<int> FindRowLocalIds(List<FragmentFilterRow> rows, string name)
        {
            foreach (FragmentFilterRow row in rows)
            {
                if (string.Equals(row.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return row.LocalIds;
                }
            }
            return NoLocalIds;
        }
    }
}
