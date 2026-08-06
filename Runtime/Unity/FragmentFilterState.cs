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

        /// <summary>Null when the view is not a storey isolation.</summary>
        public string IsolatedStorey { get; private set; }

        /// <summary>Null when the view is not a category isolation.</summary>
        public string IsolatedCategory { get; private set; }

        public int HiddenCount
        {
            get { return _hiddenLocalIds.Count; }
        }

        /// <summary>True when the build registered elements to hide.</summary>
        public bool SupportsFiltering
        {
            get { return _index != null && _index.HasEntries; }
        }

        /// <summary>True when an element hides on its own instead of taking a merged chunk with it.</summary>
        public bool SupportsElementFiltering
        {
            get { return _index != null && _index.SupportsElementFiltering; }
        }

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

        /// <summary>Rebuilds the rows only when the model changed.</summary>
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

        public IReadOnlyList<int> IsolateStorey(string storeyName)
        {
            IReadOnlyList<int> localIds = FindRowLocalIds(_storeys, storeyName);
            HideAllExcept(localIds);
            IsolatedStorey = storeyName;
            return localIds;
        }

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

        public IReadOnlyList<int> ToggleStorey(string storeyName, bool visible)
        {
            return Toggle(FindRowLocalIds(_storeys, storeyName), visible);
        }

        public IReadOnlyList<int> ToggleCategory(string category, bool visible)
        {
            return Toggle(FindRowLocalIds(_categories, category), visible);
        }

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

        private static int CompareRows(FragmentFilterRow left, FragmentFilterRow right)
        {
            return left.Count != right.Count
                ? right.Count.CompareTo(left.Count)
                : string.CompareOrdinal(left.Name, right.Name);
        }

        // The scene may have been filtered from script or another window.
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
