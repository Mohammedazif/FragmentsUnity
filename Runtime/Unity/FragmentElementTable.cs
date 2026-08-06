using System;
using System.Collections.Generic;
using UnityEngine;

namespace FragmentsUnity
{
    /// <summary>Maps a raycast triangle index on a merged mesh back to the element local id that produced it.</summary>
    [DisallowMultipleComponent]
    public sealed class FragmentElementTable : MonoBehaviour
    {
        [SerializeField] private int[] _triangleStarts = Array.Empty<int>();
        [SerializeField] private int[] _localIds = Array.Empty<int>();

        public void SetTable(int[] triangleStarts, int[] localIds)
        {
            _triangleStarts = triangleStarts ?? Array.Empty<int>();
            _localIds = localIds ?? Array.Empty<int>();
        }

        /// <summary>The distinct local ids, in first-triangle order.</summary>
        public List<int> GetDistinctLocalIds()
        {
            var distinct = new List<int>();
            var seen = new HashSet<int>();
            foreach (int localId in _localIds)
            {
                if (localId >= 0 && seen.Add(localId))
                {
                    distinct.Add(localId);
                }
            }
            return distinct;
        }

        /// <summary>The last part starting at or before the triangle; -1 before the first part.</summary>
        public int FindLocalId(int triangleIndex)
        {
            if (triangleIndex < 0 || _triangleStarts.Length == 0)
            {
                return -1;
            }

            int low = 0;
            int high = _triangleStarts.Length - 1;
            int found = -1;

            while (low <= high)
            {
                int mid = (low + high) / 2;
                if (_triangleStarts[mid] <= triangleIndex)
                {
                    found = mid;
                    low = mid + 1;
                }
                else
                {
                    high = mid - 1;
                }
            }

            return found >= 0 && found < _localIds.Length ? _localIds[found] : -1;
        }
    }
}
