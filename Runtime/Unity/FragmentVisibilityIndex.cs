using System;
using System.Collections.Generic;
using UnityEngine;

namespace FragmentsUnity
{
    /// <summary>Maps element local ids to the GameObjects drawing them so filtering can hide and show whole elements.</summary>
    [DisallowMultipleComponent]
    public sealed class FragmentVisibilityIndex : MonoBehaviour
    {
        [SerializeField] private List<int> _localIds = new List<int>();
        [SerializeField] private List<GameObject> _targets = new List<GameObject>();
        [SerializeField] private bool _elementGranular;

        private Dictionary<int, List<GameObject>> _targetsByLocalId;
        private readonly HashSet<int> _hiddenLocalIds = new HashSet<int>();

        /// <summary>True when at least one element is registered; mirrors SupportsFiltering at FragmentsActor.cpp:1071.</summary>
        public bool HasEntries
        {
            get { return _localIds.Count > 0 && _targets.Count > 0; }
        }

        /// <summary>True when each registered object draws one element alone; mirrors SupportsElementFiltering at FragmentsActor.cpp:1076.</summary>
        public bool SupportsElementFiltering
        {
            get { return _elementGranular; }
        }

        /// <summary>Records whether the build gave each element its own object; merged builds pass false.</summary>
        public void SetElementGranular(bool elementGranular)
        {
            _elementGranular = elementGranular;
        }

        /// <summary>True while any element is hidden; mirrors FragmentsActor.h:156.</summary>
        public bool IsFilterActive
        {
            get { return _hiddenLocalIds.Count > 0; }
        }

        /// <summary>Adds one drawable object for an element; an element may own many objects. Mirrors FragmentsActor.cpp:1081-1088.</summary>
        public void Register(int localId, GameObject target)
        {
            if (localId < 0 || target == null)
            {
                return;
            }

            _localIds.Add(localId);
            _targets.Add(target);

            if (_targetsByLocalId != null)
            {
                AddToLookup(localId, target);
            }
        }

        public bool IsHidden(int localId)
        {
            return _hiddenLocalIds.Contains(localId);
        }

        /// <summary>Shows or hides every object registered for these ids; mirrors FragmentsActor.cpp:1147-1173.</summary>
        public void SetVisible(IEnumerable<int> localIds, bool visible)
        {
            if (localIds == null)
            {
                return;
            }

            foreach (int localId in localIds)
            {
                if (!Lookup.TryGetValue(localId, out List<GameObject> targets))
                {
                    continue;
                }

                ApplyVisibility(targets, visible);

                if (visible)
                {
                    _hiddenLocalIds.Remove(localId);
                }
                else
                {
                    _hiddenLocalIds.Add(localId);
                }
            }
        }

        /// <summary>Hides everything registered except these ids; mirrors ApplyIsolation at FragmentsActor.cpp:1175-1211.</summary>
        public void Isolate(IEnumerable<int> localIds)
        {
            var visibleLocalIds = new HashSet<int>(localIds ?? Array.Empty<int>());
            _hiddenLocalIds.Clear();

            foreach (KeyValuePair<int, List<GameObject>> pair in Lookup)
            {
                if (visibleLocalIds.Contains(pair.Key))
                {
                    continue;
                }
                _hiddenLocalIds.Add(pair.Key);
                ApplyVisibility(pair.Value, false);
            }

            // A merged chunk backs many elements, so the requested ones are shown after the hide pass instead of racing it.
            foreach (int localId in visibleLocalIds)
            {
                if (Lookup.TryGetValue(localId, out List<GameObject> targets))
                {
                    ApplyVisibility(targets, true);
                }
            }
        }

        /// <summary>Restores everything this index hid; mirrors ClearFilter at FragmentsActor.cpp:1243-1256.</summary>
        public void Clear()
        {
            // The serialized targets outlive a domain reload, so sweeping them restores even after the hidden set is lost.
            foreach (GameObject target in _targets)
            {
                ApplyVisibility(target, true);
            }

            _hiddenLocalIds.Clear();
        }

        private Dictionary<int, List<GameObject>> Lookup
        {
            get
            {
                if (_targetsByLocalId == null)
                {
                    _targetsByLocalId = new Dictionary<int, List<GameObject>>();
                    // The two serialized lists are editable apart in the Inspector, so only paired entries are indexed.
                    int paired = Math.Min(_localIds.Count, _targets.Count);
                    for (int entry = 0; entry < paired; entry++)
                    {
                        AddToLookup(_localIds[entry], _targets[entry]);
                    }
                }
                return _targetsByLocalId;
            }
        }

        private void AddToLookup(int localId, GameObject target)
        {
            if (localId < 0 || target == null)
            {
                return;
            }

            if (!_targetsByLocalId.TryGetValue(localId, out List<GameObject> targets))
            {
                targets = new List<GameObject>();
                _targetsByLocalId[localId] = targets;
            }
            targets.Add(target);
        }

        private void ApplyVisibility(List<GameObject> targets, bool visible)
        {
            foreach (GameObject target in targets)
            {
                ApplyVisibility(target, visible);
            }
        }

        private void ApplyVisibility(GameObject target, bool visible)
        {
            if (target == null)
            {
                return;
            }

            Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
            Collider[] colliders = target.GetComponentsInChildren<Collider>(true);

            if (renderers.Length == 0 && colliders.Length == 0)
            {
                target.SetActive(visible);
                return;
            }

            foreach (Renderer renderer in renderers)
            {
                // forceRenderingOff is independent of enabled, so the volume categories the build hid stay hidden.
                renderer.forceRenderingOff = !visible;
            }

            foreach (Collider collider in colliders)
            {
                collider.enabled = visible;
            }
        }
    }
}
