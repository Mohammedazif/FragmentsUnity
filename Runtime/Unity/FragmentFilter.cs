using System.Collections.Generic;
using UnityEngine;

namespace FragmentsUnity
{
    /// <summary>Answers filter requests by querying the model on this GameObject and driving its visibility index.</summary>
    [DisallowMultipleComponent]
    public sealed class FragmentFilter : MonoBehaviour
    {
        private FragmentModel _model;
        private FragmentVisibilityIndex _index;

        /// <summary>Shows only the elements of one IFC category; mirrors FragmentsActor.cpp:1218.</summary>
        public void IsolateByCategory(string category)
        {
            if (CanQueryModel())
            {
                Index.Isolate(Model.FindByCategory(category));
            }
        }

        /// <summary>Shows only one storey and everything it contains; mirrors FragmentsActor.cpp:1223.</summary>
        public void IsolateByStorey(string storeyName)
        {
            if (CanQueryModel())
            {
                Index.Isolate(Model.FindByStorey(storeyName));
            }
        }

        /// <summary>Shows only the elements carrying an attribute or property value; mirrors FragmentsActor.cpp:1228.</summary>
        public void IsolateByAttribute(string name, string value, bool exactMatch)
        {
            if (CanQueryModel())
            {
                Index.Isolate(Model.FindByAttribute(name, value, exactMatch));
            }
        }

        public void IsolateLocalIds(IEnumerable<int> localIds)
        {
            if (HasFilterableElements())
            {
                Index.Isolate(localIds);
            }
        }

        /// <summary>Shows or hides one set of elements; mirrors SetVisibilityByLocalIds at FragmentsActor.h:132.</summary>
        public void SetVisibleLocalIds(IEnumerable<int> localIds, bool visible)
        {
            if (HasFilterableElements())
            {
                Index.SetVisible(localIds, visible);
            }
        }

        public void SetCategoryVisible(string category, bool visible)
        {
            if (CanQueryModel())
            {
                Index.SetVisible(Model.FindByCategory(category), visible);
            }
        }

        public void SetStoreyVisible(string storeyName, bool visible)
        {
            if (CanQueryModel())
            {
                Index.SetVisible(Model.FindByStorey(storeyName), visible);
            }
        }

        /// <summary>Makes everything visible again; mirrors FragmentsActor.cpp:1243.</summary>
        public void ClearFilter()
        {
            if (Index != null)
            {
                Index.Clear();
            }
        }

        private FragmentModel Model
        {
            get
            {
                if (_model == null)
                {
                    _model = GetComponent<FragmentModel>();
                }
                return _model;
            }
        }

        private FragmentVisibilityIndex Index
        {
            get
            {
                if (_index == null)
                {
                    _index = GetComponent<FragmentVisibilityIndex>();
                }
                return _index;
            }
        }

        private bool CanQueryModel()
        {
            if (!HasFilterableElements())
            {
                return false;
            }

            if (Model == null)
            {
                Debug.LogWarning($"[FragmentsUnity] {name} carries no FragmentModel, so there is no metadata to filter by.", this);
                return false;
            }
            return true;
        }

        private bool HasFilterableElements()
        {
            if (Index == null || !Index.HasEntries)
            {
                // mirrors FragmentsActor.cpp:1179-1185
                Debug.LogWarning($"[FragmentsUnity] {name} has no per-element objects to filter. Re-import in one of the hierarchy modes.", this);
                return false;
            }

            if (!Index.SupportsElementFiltering)
            {
                // mirrors FragmentsActor.cpp:1187-1193
                Debug.LogWarning(
                    $"[FragmentsUnity] {name} was imported in a merged mode, so filtering can only isolate whole "
                    + "chunks. Anything finer will hide the chunk it belongs to.",
                    this);
            }
            return true;
        }
    }
}
