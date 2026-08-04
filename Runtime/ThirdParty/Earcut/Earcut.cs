// earcut.hpp - polygon triangulation
// https://github.com/mapbox/earcut.hpp
//
// ISC License
//
// Copyright (c) 2015, Mapbox
//
// Permission to use, copy, modify, and/or distribute this software for any purpose
// with or without fee is hereby granted, provided that the above copyright notice
// and this permission notice appear in all copies.
//
// THE SOFTWARE IS PROVIDED "AS IS" AND THE AUTHOR DISCLAIMS ALL WARRANTIES WITH
// REGARD TO THIS SOFTWARE INCLUDING ALL IMPLIED WARRANTIES OF MERCHANTABILITY AND
// FITNESS. IN NO EVENT SHALL THE AUTHOR BE LIABLE FOR ANY SPECIAL, DIRECT,
// INDIRECT, OR CONSEQUENTIAL DAMAGES OR ANY DAMAGES WHATSOEVER RESULTING FROM LOSS
// OF USE, DATA OR PROFITS, WHETHER IN AN ACTION OF CONTRACT, NEGLIGENCE OR OTHER
// TORTIOUS ACTION, ARISING OUT OF OR IN CONNECTION WITH THE USE OR PERFORMANCE OF
// THIS SOFTWARE.
//
// C# port of the FragmentsUE-modified earcut.hpp (Delaunay refiner removed upstream; caller-owned work budget added).

using System;
using System.Collections.Generic;

namespace FragmentsUnity
{
    /// <summary>Ear-clipping polygon triangulator (Mapbox earcut) with an optional caller-owned work budget.</summary>
    public static class Earcut
    {
        /// <summary>Triangulates rings[0] (outer) with rings[1..] as holes; each ring is flat [x0, y0, x1, y1, ...]; indices address the concatenated vertex sequences.</summary>
        public static List<int> Triangulate(List<List<double>> rings)
        {
            var solver = new Solver();
            solver.Triangulate(rings);
            return solver.Indices;
        }

        /// <summary>Triangulates while drawing on a shared model-wide work budget; the spent remainder is written back and on exhaustion the triangles produced so far are returned.</summary>
        public static List<int> Triangulate(List<List<double>> rings, ref long workBudget, out bool budgetExhausted)
        {
            var solver = new Solver();
            solver.LimitWork(workBudget);
            solver.Triangulate(rings);
            workBudget = solver.RemainingWorkBudget;
            budgetExhausted = solver.BudgetExhausted;
            return solver.Indices;
        }

        private sealed class Solver
        {
            public readonly List<int> Indices = new List<int>();
            public bool BudgetExhausted;
            public long RemainingWorkBudget => _workBudget;

            private const int HashingVertexThreshold = 80;  // mirrors earcut.hpp:264
            private const int NodesPerBlock = 16;           // K in earcut.hpp:246
            private const double ZOrderRange = 32767.0;     // 15-bit z-order coordinate range
            private const int NodeIndexMask = int.MaxValue; // 31-bit index bitfield, earcut.hpp:81

            // Shared allowance, counted in inner-loop steps. Untrusted geometry can drive
            // splitEarcut's nested diagonal search — whose validity test walks the whole
            // ring — into O(n^3), so the allowance is threaded in from the caller and shared
            // across every face in the model. When it runs out the solver stops where it
            // stands and returns what it has. No budget means unlimited (upstream behaviour).
            private bool _limited;
            private long _workBudget;

            private int _vertices;
            private bool _hashing;
            private bool _filteredOut;
            private double _minX, _maxX;
            private double _minY, _maxY;
            private double _invSize;

            private readonly List<Node> _holeQueue = new List<Node>();
            private readonly List<Node> _sortBuffer = new List<Node>();

            private double[] _blockBBox;
            private Node[] _blockHead;
            private Node[] _blockStop;
            private int _numBlocks;
            private bool _indexActive;

            public void LimitWork(long budget)
            {
                _limited = true;
                _workBudget = budget;
            }

            // Returns false once the allowance is spent, at which point every loop that
            // consults it unwinds.
            private bool SpendWork(long units)
            {
                if (!_limited) return true;
                _workBudget -= units;
                if (_workBudget <= 0)
                {
                    BudgetExhausted = true;
                    return false;
                }
                return true;
            }

            private sealed class Node
            {
                public readonly double X;
                public readonly double Y;
                public readonly int I;
                public bool Steiner;

                public Node Prev;
                public Node Next;

                public int Z;

                public Node PrevZ;
                public Node NextZ;

                public Node(int index, double x, double y)
                {
                    X = x;
                    Y = y;
                    I = index & NodeIndexMask;
                }
            }

            private readonly struct Triangle
            {
                public readonly double Ax, Ay;
                public readonly double Bx, By;
                public readonly double Cx, Cy;
                public readonly double MinX, MinY, MaxX, MaxY;

                public Triangle(Node a, Node b, Node c)
                {
                    Ax = a.X;
                    Ay = a.Y;
                    Bx = b.X;
                    By = b.Y;
                    Cx = c.X;
                    Cy = c.Y;
                    MinX = Math.Min(Ax, Math.Min(Bx, Cx));
                    MinY = Math.Min(Ay, Math.Min(By, Cy));
                    MaxX = Math.Max(Ax, Math.Max(Bx, Cx));
                    MaxY = Math.Max(Ay, Math.Max(By, Cy));
                }

                public bool InBBox(double px, double py)
                {
                    return px >= MinX && px <= MaxX && py >= MinY && py <= MaxY;
                }

                public bool ContainsPoint(double px, double py)
                {
                    return (Cx - px) * (Ay - py) >= (Ax - px) * (Cy - py) &&
                           (Ax - px) * (By - py) >= (Bx - px) * (Ay - py) &&
                           (Bx - px) * (Cy - py) >= (Cx - px) * (By - py);
                }

                public bool ContainsPointExceptFirst(double px, double py)
                {
                    return !(Ax == px && Ay == py) && ContainsPoint(px, py);
                }
            }

            public void Triangulate(List<List<double>> rings)
            {
                Indices.Clear();
                _vertices = 0;

                if (rings.Count == 0) return;

                int threshold = HashingVertexThreshold;
                int len = 0;

                int numRings = rings.Count;
                for (int i = 0; threshold >= 0 && i < numRings; i++)
                {
                    int ringLen = rings[i].Count / 2;
                    threshold -= ringLen;
                    len += ringLen;
                }

                int reserve = len + rings[0].Count / 2;
                if (Indices.Capacity < reserve) Indices.Capacity = reserve;

                Node outerNode = BuildLinkedList(rings[0], true);
                if (outerNode == null || outerNode.Prev == outerNode.Next) return;

                if (rings.Count > 1) outerNode = EliminateHoles(rings, outerNode);

                _hashing = threshold < 0;
                if (_hashing)
                {
                    Node p = outerNode.Next;
                    _minX = _maxX = outerNode.X;
                    _minY = _maxY = outerNode.Y;
                    do
                    {
                        double x = p.X;
                        double y = p.Y;
                        _minX = Math.Min(_minX, x);
                        _minY = Math.Min(_minY, y);
                        _maxX = Math.Max(_maxX, x);
                        _maxY = Math.Max(_maxY, y);
                        p = p.Next;
                    } while (p != outerNode);

                    _invSize = Math.Max(_maxX - _minX, _maxY - _minY);
                    _invSize = _invSize != 0.0 ? (ZOrderRange / _invSize) : 0.0;
                }

                EarcutLinked(outerNode);

                _holeQueue.Clear();
            }

            private Node BuildLinkedList(List<double> ring, bool clockwise)
            {
                double sum = 0;
                int len = ring.Count / 2; // flat interleaved ring; a trailing unpaired double is ignored
                Node last = null;

                // calculate original winding order of a polygon ring
                for (int i = 0, j = len > 0 ? len - 1 : 0; i < len; j = i++)
                {
                    double p10 = ring[i * 2];
                    double p11 = ring[i * 2 + 1];
                    double p20 = ring[j * 2];
                    double p21 = ring[j * 2 + 1];
                    sum += (p20 - p10) * (p11 + p21);
                }

                // link points into circular doubly-linked list in the specified winding order
                if (clockwise == (sum > 0))
                {
                    for (int i = 0; i < len; i++) last = InsertNode(_vertices + i, ring[i * 2], ring[i * 2 + 1], last);
                }
                else
                {
                    for (int i = len; i-- > 0;) last = InsertNode(_vertices + i, ring[i * 2], ring[i * 2 + 1], last);
                }

                if (last != null && AreEqual(last, last.Next))
                {
                    RemoveNode(last);
                    last = last.Next;
                }

                _vertices += len;

                return last;
            }

            // Remove collinear or coincident points; removability depends only on a node's immediate
            // neighbors, so we sweep forward and re-check the predecessor after each removal. With no `end`
            // we sweep the whole ring, lapping until nothing is removable (the fixpoint the clipper needs).
            // With an explicit `end` we heal only the dirty window around a bridge/diagonal cut, stopping at
            // `end` rather than lapping — O(window) instead of O(ring).
            private Node FilterPoints(Node start, Node end = null)
            {
                if (start == null) return start;
                bool full = end == null;
                if (full) end = start;

                Node p = start;
                bool again;
                do
                {
                    again = false;
                    if (p != p.Next && !p.Steiner && (AreEqual(p, p.Next) || Area(p.Prev, p, p.Next) == 0))
                    {
                        if (full || p == end) end = p.Prev; // pull the stop bound back past the removal
                        _filteredOut = true;
                        RemoveNode(p);
                        p = p.Prev; // re-check the predecessor
                        again = true;
                    }
                    else if (full || p != end)
                    {
                        p = p.Next;
                        again = !full; // local heal: keep looping until the sweep reaches end
                    }
                } while (again || p != end);

                return end;
            }

            // main ear slicing loop which triangulates a polygon (given as a linked list)
            private void EarcutLinked(Node ear)
            {
                if (ear == null) return;

                // interlink polygon nodes in z-order
                if (_hashing) IndexCurve(ear);

                Node stop = ear;
                bool cured = false;

                // iterate through ears, slicing them one by one
                while (ear.Prev != ear.Next)
                {
                    if (!SpendWork(1)) return;
                    Node prev = ear.Prev;
                    Node next = ear.Next;

                    // reflex check is hoisted here to avoid constructing the Triangle for reflex corners
                    if (Area(prev, ear, next) < 0 && (_hashing ? IsEarHashed(ear) : IsEar(ear)))
                    {
                        // cut off the triangle
                        Indices.Add(prev.I);
                        Indices.Add(ear.I);
                        Indices.Add(next.I);

                        RemoveNode(ear);

                        ear = next;
                        stop = next;

                        continue;
                    }

                    ear = next;

                    // if we looped through the whole remaining polygon and can't find any more ears
                    if (ear == stop)
                    {
                        // try filtering collinear/coincident points and slicing again — repeat as long as
                        // filtering actually removes nodes, since each removal can expose new ears
                        _filteredOut = false;
                        ear = FilterPoints(ear);
                        if (_filteredOut)
                        {
                            stop = ear;
                            continue;
                        }

                        // filtering is exhausted: cure small local self-intersections once, then retry
                        if (!cured)
                        {
                            ear = CureLocalIntersections(ear);
                            stop = ear;
                            cured = true;
                            continue;
                        }

                        // as a last resort, try splitting the remaining polygon into two
                        SplitEarcut(ear);
                        break;
                    }
                }
            }

            // check whether a polygon node forms a valid ear with adjacent nodes
            private bool IsEar(Node ear)
            {
                Node a = ear.Prev;
                Node b = ear;
                Node c = ear.Next;

                // reflex check is hoisted into the EarcutLinked caller
                var tri = new Triangle(a, b, c);

                // now make sure we don't have other points inside the potential ear
                Node p = ear.Next.Next;

                while (p != ear.Prev)
                {
                    if (tri.InBBox(p.X, p.Y) && tri.ContainsPointExceptFirst(p.X, p.Y) && Area(p.Prev, p, p.Next) >= 0)
                        return false;
                    p = p.Next;
                }

                return true;
            }

            private bool IsEarHashed(Node ear)
            {
                Node a = ear.Prev;
                Node b = ear;
                Node c = ear.Next;

                // reflex check is hoisted into the EarcutLinked caller
                var tri = new Triangle(a, b, c);

                // z-order range for the current triangle bbox;
                int minZ = ComputeZOrder(tri.MinX, tri.MinY);
                int maxZ = ComputeZOrder(tri.MaxX, tri.MaxY);

                // first look for points inside the triangle in increasing z-order
                Node p = ear.NextZ;

                while (p != null && p.Z <= maxZ)
                {
                    if (p != ear.Next && tri.InBBox(p.X, p.Y) && tri.ContainsPointExceptFirst(p.X, p.Y) &&
                        Area(p.Prev, p, p.Next) >= 0)
                        return false;
                    p = p.NextZ;
                }

                // then look for points in decreasing z-order
                p = ear.PrevZ;

                while (p != null && p.Z >= minZ)
                {
                    if (p != ear.Next && tri.InBBox(p.X, p.Y) && tri.ContainsPointExceptFirst(p.X, p.Y) &&
                        Area(p.Prev, p, p.Next) >= 0)
                        return false;
                    p = p.PrevZ;
                }

                return true;
            }

            // go through all polygon nodes and cure small local self-intersections
            private Node CureLocalIntersections(Node start)
            {
                Node p = start;
                bool cured = false;
                do
                {
                    Node a = p.Prev;
                    Node b = p.Next.Next;

                    // a self-intersection where edge (v[i-1],v[i]) intersects (v[i+1],v[i+2]);
                    // includeBoundary=false so a mere collinear touch isn't treated as a crossing
                    if (SegmentsIntersect(a, p, p.Next, b, false) && IsLocallyInside(a, b) && IsLocallyInside(b, a))
                    {
                        Indices.Add(a.I);
                        Indices.Add(p.I);
                        Indices.Add(b.I);

                        // remove two nodes involved
                        RemoveNode(p);
                        RemoveNode(p.Next);

                        p = start = b;
                        cured = true;
                    }
                    p = p.Next;
                } while (p != start);

                return cured ? FilterPoints(p) : p;
            }

            // try splitting polygon into two and triangulate them independently
            private void SplitEarcut(Node start)
            {
                // look for a valid diagonal that divides the polygon into two
                Node a = start;
                do
                {
                    Node b = a.Next.Next;
                    while (b != a.Prev)
                    {
                        if (!SpendWork(1)) return;
                        if (a.I != b.I && IsValidDiagonal(a, b))
                        {
                            // split the polygon in two by the diagonal
                            Node c = SplitPolygon(a, b);

                            // filter colinear points around the cuts
                            a = FilterPoints(a, a.Next);
                            c = FilterPoints(c, c.Next);

                            // run earcut on each half
                            EarcutLinked(a);
                            EarcutLinked(c);
                            return;
                        }
                        b = b.Next;
                    }
                    a = a.Next;
                } while (a != start);
            }

            // link every hole into the outer loop, producing a single-ring polygon without holes
            private Node EliminateHoles(List<List<double>> rings, Node outerNode)
            {
                int len = rings.Count;

                _holeQueue.Clear();
                for (int i = 1; i < len; i++)
                {
                    Node list = BuildLinkedList(rings[i], false);
                    if (list != null)
                    {
                        if (list == list.Next) list.Steiner = true;
                        _holeQueue.Add(GetLeftmost(list));
                    }
                }
                // compareXYSlope: sort by x, then y, then slope. When two holes' leftmost points coincide, the
                // slope tiebreak makes the bridge land on the shared vertex instead of bridging the wrong hole.
                _holeQueue.Sort(CompareHoleStarts);

                // block-bbox index for FindHoleBridge, grown append-only as holes merge. Seed it with the
                // outer ring, then append each merged hole.
                BuildBlockIndex(_vertices, _holeQueue.Count);
                IndexSegment(outerNode, outerNode);

                // process holes from left to right; _indexActive lets RemoveNode keep block bboxes live as
                // FilterPoints heals edges during merges (see GrowBlock)
                _indexActive = true;
                for (int i = 0; i < _holeQueue.Count; i++)
                {
                    outerNode = EliminateHole(_holeQueue[i], outerNode);
                }
                _indexActive = false;

                // collapse collinear/coincident points across the whole merged ring once before clipping
                return FilterPoints(outerNode);
            }

            private static int CompareHoleStarts(Node a, Node b)
            {
                if (a.X != b.X) return a.X < b.X ? -1 : 1;
                if (a.Y != b.Y) return a.Y < b.Y ? -1 : 1;
                double adx = a.Next.X - a.X, ady = a.Next.Y - a.Y;
                double bdx = b.Next.X - b.X, bdy = b.Next.Y - b.Y;
                bool aDegenerate = adx == 0 && ady == 0;
                bool bDegenerate = bdx == 0 && bdy == 0;
                if (aDegenerate != bDegenerate) return aDegenerate ? -1 : 1;
                double lhs = ady * bdx;
                double rhs = bdy * adx;
                if (lhs != rhs) return lhs < rhs ? -1 : 1;
                return 0;
            }

            // find a bridge between vertices that connects hole with an outer ring and link it
            private Node EliminateHole(Node hole, Node outerNode)
            {
                Node bridge = FindHoleBridge(hole, outerNode);
                if (bridge == null)
                {
                    return outerNode;
                }

                Node bridgeReverse = SplitPolygon(bridge, hole);

                // index the merged-in segment before filtering: in ring order the splice runs
                // bridge -> hole -> bridgeReverse -> bridge2 -> (bridge's old next), covering the hole's edges
                // and both new slit edges. FilterPoints below only drops collinear/coincident points, so these
                // bboxes stay valid (conservative) supersets.
                Node bridge2 = bridgeReverse.Next;
                IndexSegment(bridge, bridge2.Next);

                // heal collinear/coincident points around the two new slit edges
                FilterPoints(bridgeReverse, bridgeReverse.Next);
                return FilterPoints(bridge, bridge.Next);
            }

            // David Eberly's algorithm for finding a bridge between hole and outer polygon
            private Node FindHoleBridge(Node hole, Node outerNode)
            {
                Node p = outerNode;
                double hx = hole.X;
                double hy = hole.Y;
                double qx = double.MinValue;
                Node m = null;

                // find a segment intersected by a ray from the hole's leftmost vertex to the left;
                // segment's endpoint with lesser x will be potential connection vertex,
                // unless they intersect at a vertex, then choose the vertex
                if (AreEqual(hole, p)) return p;

                // scan blocks; skip any whose bbox can't hold a crossing that beats qx and lies left of hx
                // (the prune Morton order can't express — explicit per-axis [minY,maxY]/[minX,maxX])
                for (int b = 0, g = 0; b < _numBlocks; b++, g += 4)
                {
                    if (hy < _blockBBox[g + 1] || hy > _blockBBox[g + 3] || _blockBBox[g] > hx || _blockBBox[g + 2] <= qx)
                        continue;

                    // ensure the walk's exclusive bound is live so we don't overrun into other blocks
                    Node stop = GetLiveBlockStop(b);
                    p = GetLiveBlockHead(b);
                    do
                    {
                        if (p.Prev.Next == p) // skip nodes removed by FilterPoints (stale in the index)
                        {
                            if (AreEqual(hole, p.Next))
                                return p.Next;
                            if (hy <= p.Y && hy >= p.Next.Y && p.Next.Y != p.Y)
                            {
                                double x = p.X + (hy - p.Y) * (p.Next.X - p.X) / (p.Next.Y - p.Y);
                                if (x <= hx && x > qx)
                                {
                                    qx = x;
                                    m = p.X < p.Next.X ? p : p.Next;
                                    if (x == hx) return m; // hole touches outer segment; pick leftmost endpoint
                                }
                            }
                        }
                        p = p.Next;
                    } while (p != stop);
                }

                if (m == null) return null;

                // look for points inside the triangle of hole vertex, segment intersection and endpoint;
                // if there are no points found, we have a valid connection;
                // otherwise choose the vertex of the minimum angle with the ray as connection vertex

                double mx = m.X;
                double my = m.Y;
                double tminY = Math.Min(hy, my); // the triangle's y span; x span is [mx, hx]
                double tmaxY = Math.Max(hy, my);
                double tanMin = double.MaxValue;

                // scan the same blocks; skip any whose bbox can't overlap the triangle's [mx,hx]x[tminY,tmaxY] box
                for (int b = 0, g = 0; b < _numBlocks; b++, g += 4)
                {
                    if (_blockBBox[g + 2] < mx || _blockBBox[g] > hx || _blockBBox[g + 3] < tminY || _blockBBox[g + 1] > tmaxY)
                        continue;

                    Node stop = GetLiveBlockStop(b);
                    p = GetLiveBlockHead(b);
                    do
                    {
                        if (p.Prev.Next == p && hx >= p.X && p.X >= mx && hx != p.X && // skip dead nodes
                            IsPointInTriangle(hy < my ? hx : qx, hy, mx, my, hy < my ? qx : hx, hy, p.X, p.Y))
                        {
                            double tanCur = Math.Abs(hy - p.Y) / (hx - p.X); // tangential

                            // if hole point sits on p's horizontal edge (T-junction touch): the bridge runs
                            // along that edge — IsLocallyInside rejects it as collinear, but it's valid
                            if ((IsLocallyInside(p, hole) || (p.Y == hy && p.Next.Y == hy && p.Next.X > hx)) &&
                                (tanCur < tanMin ||
                                 (tanCur == tanMin && (p.X > m.X || (p.X == m.X && SectorContainsSector(m, p))))))
                            {
                                m = p;
                                tanMin = tanCur;
                            }
                        }
                        p = p.Next;
                    } while (p != stop);
                }

                return m;
            }

            // Block-bbox index buffers: size once from the input upper bound and reuse across calls.
            private void BuildBlockIndex(int maxNodes, int numHoles)
            {
                // upper bound: every input node indexed once, +2 bridge nodes per hole, plus a partial
                // trailing block per appended segment (outer ring + one per hole)
                int maxBlocks = (maxNodes + 2 * numHoles + NodesPerBlock - 1) / NodesPerBlock + numHoles + 2;
                if (_blockBBox == null || _blockBBox.Length < maxBlocks * 4) _blockBBox = new double[maxBlocks * 4];
                if (_blockHead == null || _blockHead.Length < maxBlocks)
                {
                    _blockHead = new Node[maxBlocks];
                    _blockStop = new Node[maxBlocks];
                }
                _numBlocks = 0;
            }

            // index the ring run head..stop (exclusive) as ceil(len / NodesPerBlock) blocks; head == stop means
            // the whole ring. each block's bbox covers both endpoints of every edge it owns.
            private void IndexSegment(Node head, Node stop)
            {
                Node p = head;
                do
                {
                    int b = _numBlocks++;
                    _blockHead[b] = p;
                    double bMinX = double.MaxValue;
                    double bMinY = double.MaxValue;
                    double bMaxX = double.MinValue;
                    double bMaxY = double.MinValue;
                    int k = 0;
                    do
                    {
                        Node c = p.Next; // edge p->c; bbox must bound both endpoints
                        p.Z = b; // reuse z as the owning block during EliminateHoles (see GrowBlock)
                        if (p.X < bMinX) bMinX = p.X;
                        if (p.X > bMaxX) bMaxX = p.X;
                        if (p.Y < bMinY) bMinY = p.Y;
                        if (p.Y > bMaxY) bMaxY = p.Y;
                        if (c.X < bMinX) bMinX = c.X;
                        if (c.X > bMaxX) bMaxX = c.X;
                        if (c.Y < bMinY) bMinY = c.Y;
                        if (c.Y > bMaxY) bMaxY = c.Y;
                        p = c;
                    } while (++k < NodesPerBlock && p != stop);
                    _blockStop[b] = p;
                    int g = b * 4;
                    _blockBBox[g] = bMinX;
                    _blockBBox[g + 1] = bMinY;
                    _blockBBox[g + 2] = bMaxX;
                    _blockBBox[g + 3] = bMaxY;
                } while (p != stop);
            }

            // when FilterPoints heals an edge head->tail (removing the collinear node between them), the healed
            // edge can extend past head's frozen block bbox if its old far endpoint lived in another block; grow
            // head's block bbox to cover tail so the leftward-ray prune can't false-skip it.
            private void GrowBlock(Node head, Node tail)
            {
                int g = head.Z * 4;
                if (tail.X < _blockBBox[g]) _blockBBox[g] = tail.X;
                if (tail.Y < _blockBBox[g + 1]) _blockBBox[g + 1] = tail.Y;
                if (tail.X > _blockBBox[g + 2]) _blockBBox[g + 2] = tail.X;
                if (tail.Y > _blockBBox[g + 3]) _blockBBox[g + 3] = tail.Y;
            }

            // the block's head node can be removed by FilterPoints during merges; advance it to the next live
            // node so the walk doesn't start on (and immediately terminate at) a dead node. For the single
            // full-ring seed block (head == stop) the same forward advance keeps them equal, so the do-while
            // still laps the whole ring instead of collapsing to an empty walk.
            private Node GetLiveBlockHead(int b)
            {
                Node head = _blockHead[b];
                while (head.Prev.Next != head) head = head.Next;
                _blockHead[b] = head;
                return head;
            }

            private Node GetLiveBlockStop(int b)
            {
                Node stop = _blockStop[b];
                while (stop.Prev.Next != stop) stop = stop.Next;
                _blockStop[b] = stop;
                return stop;
            }

            // whether sector in vertex m contains sector in vertex p in the same coordinates
            private static bool SectorContainsSector(Node m, Node p)
            {
                return Area(m.Prev, m, p.Prev) < 0 && Area(p.Next, m, m.Next) < 0;
            }

            // interlink polygon nodes in z-order
            private void IndexCurve(Node start)
            {
                Node p = start;

                do
                {
                    // always (re)compute: z may still hold a block index left over from EliminateHoles
                    p.Z = ComputeZOrder(p.X, p.Y);
                    p.PrevZ = p.Prev;
                    p.NextZ = p.Next;
                    p = p.Next;
                } while (p != start);

                p.PrevZ.NextZ = null;
                p.PrevZ = null;

                SortLinked(p);
            }

            // Sort the z-linked ring by z-order: materialize node refs into a scratch buffer, sort, relink.
            // List.Sort mirrors the C++ std::sort — both unstable, so tie order among equal z keys is
            // implementation-defined in both.
            private Node SortLinked(Node list)
            {
                // list is a null-terminated nextZ chain (see IndexCurve); walk it into the scratch buffer
                _sortBuffer.Clear();
                for (Node p = list; p != null; p = p.NextZ) _sortBuffer.Add(p);

                _sortBuffer.Sort(CompareZOrderKeys);

                // relink in sorted order
                Node prev = null;
                for (int i = 0; i < _sortBuffer.Count; i++)
                {
                    Node p = _sortBuffer[i];
                    p.PrevZ = prev;
                    if (prev != null) prev.NextZ = p;
                    prev = p;
                }
                prev.NextZ = null;
                return _sortBuffer[0];
            }

            private static int CompareZOrderKeys(Node a, Node b)
            {
                return a.Z.CompareTo(b.Z);
            }

            // z-order of a vertex given coords and size of the data bounding box
            private int ComputeZOrder(double x, double y)
            {
                // coords are transformed into non-negative 15-bit integer range
                int ix = (int)((x - _minX) * _invSize);
                int iy = (int)((y - _minY) * _invSize);

                ix = (ix | (ix << 8)) & 0x00FF00FF;
                ix = (ix | (ix << 4)) & 0x0F0F0F0F;
                ix = (ix | (ix << 2)) & 0x33333333;
                ix = (ix | (ix << 1)) & 0x55555555;

                iy = (iy | (iy << 8)) & 0x00FF00FF;
                iy = (iy | (iy << 4)) & 0x0F0F0F0F;
                iy = (iy | (iy << 2)) & 0x33333333;
                iy = (iy | (iy << 1)) & 0x55555555;

                return ix | (iy << 1);
            }

            // find the leftmost node of a polygon ring
            private static Node GetLeftmost(Node start)
            {
                Node p = start;
                Node leftmost = start;
                do
                {
                    if (p.X < leftmost.X || (p.X == leftmost.X && p.Y < leftmost.Y)) leftmost = p;
                    p = p.Next;
                } while (p != start);

                return leftmost;
            }

            // check if a point lies within a convex triangle
            private static bool IsPointInTriangle(
                double ax, double ay, double bx, double by, double cx, double cy, double px, double py)
            {
                return (cx - px) * (ay - py) >= (ax - px) * (cy - py) &&
                       (ax - px) * (by - py) >= (bx - px) * (ay - py) &&
                       (bx - px) * (cy - py) >= (cx - px) * (by - py);
            }

            // check if a diagonal between two polygon nodes is valid (lies in polygon interior)
            private bool IsValidDiagonal(Node a, Node b)
            {
                // degenerate zero-length case
                bool zeroLength = AreEqual(a, b) && Area(a.Prev, a, a.Next) > 0 && Area(b.Prev, b, b.Next) > 0;
                return a.Next.I != b.I &&
                       (zeroLength ||
                        (IsLocallyInside(a, b) && IsLocallyInside(b, a) &&                    // locally visible
                         (Area(a.Prev, a, b.Prev) != 0.0 || Area(a, b.Prev, b) != 0.0))) &&   // no opposite-facing sectors
                       !IntersectsPolygon(a, b) &&                                            // doesn't intersect other edges
                       (zeroLength || IsMiddleInside(a, b));                                  // diagonal inside polygon
            }

            // signed area of a triangle
            private static double Area(Node p, Node q, Node r)
            {
                return (q.Y - p.Y) * (r.X - q.X) - (q.X - p.X) * (r.Y - q.Y);
            }

            // check if two points are equal
            private static bool AreEqual(Node p1, Node p2)
            {
                return p1.X == p2.X && p1.Y == p2.Y;
            }

            // check if two segments intersect; by default includes collinear boundary touches
            private static bool SegmentsIntersect(Node p1, Node q1, Node p2, Node q2, bool includeBoundary = true)
            {
                double o1 = Area(p1, q1, p2);
                double o2 = Area(p1, q1, q2);
                double o3 = Area(p2, q2, p1);
                double o4 = Area(p2, q2, q1);

                // general case: the two segments straddle each other (proper crossing)
                if (((o1 > 0 && o2 < 0) || (o1 < 0 && o2 > 0)) && ((o3 > 0 && o4 < 0) || (o3 < 0 && o4 > 0))) return true;

                if (!includeBoundary) return false;

                if (o1 == 0 && IsOnSegment(p1, p2, q1)) return true; // p1, q1 and p2 are collinear and p2 lies on p1q1
                if (o2 == 0 && IsOnSegment(p1, q2, q1)) return true; // p1, q1 and q2 are collinear and q2 lies on p1q1
                if (o3 == 0 && IsOnSegment(p2, p1, q2)) return true; // p2, q2 and p1 are collinear and p1 lies on p2q2
                if (o4 == 0 && IsOnSegment(p2, q1, q2)) return true; // p2, q2 and q1 are collinear and q1 lies on p2q2

                return false;
            }

            // for collinear points p, q, r, check if point q lies on segment pr
            private static bool IsOnSegment(Node p, Node q, Node r)
            {
                return q.X <= Math.Max(p.X, r.X) && q.X >= Math.Min(p.X, r.X) &&
                       q.Y <= Math.Max(p.Y, r.Y) && q.Y >= Math.Min(p.Y, r.Y);
            }

            // check if a polygon diagonal intersects any polygon segments
            private bool IntersectsPolygon(Node a, Node b)
            {
                // diagonal bbox; an edge whose bbox can't overlap it can't intersect it, so
                // skip the orientation test for those (the common case — the diagonal is short)
                double diagMinX = Math.Min(a.X, b.X);
                double diagMaxX = Math.Max(a.X, b.X);
                double diagMinY = Math.Min(a.Y, b.Y);
                double diagMaxY = Math.Max(a.Y, b.Y);

                Node p = a;
                do
                {
                    // This walk is the O(n) factor inside SplitEarcut's O(n^2) search, so it is
                    // where the cubic term actually accrues. Rejecting the diagonal on
                    // exhaustion is the conservative answer: it cannot invent a bad split, and
                    // SplitEarcut's own check unwinds on the next step.
                    if (!SpendWork(1)) return true;
                    Node n = p.Next;
                    if ((p.X > diagMaxX && n.X > diagMaxX) || (p.X < diagMinX && n.X < diagMinX) ||
                        (p.Y > diagMaxY && n.Y > diagMaxY) || (p.Y < diagMinY && n.Y < diagMinY))
                    {
                        p = n;
                        continue;
                    }
                    if (p.I != a.I && n.I != a.I && p.I != b.I && n.I != b.I && SegmentsIntersect(p, n, a, b)) return true;
                    p = n;
                } while (p != a);

                return false;
            }

            // check if a polygon diagonal is locally inside the polygon
            private static bool IsLocallyInside(Node a, Node b)
            {
                return Area(a.Prev, a, a.Next) < 0 ? Area(a, b, a.Next) >= 0 && Area(a, a.Prev, b) >= 0
                                                   : Area(a, b, a.Prev) < 0 || Area(a, a.Next, b) < 0;
            }

            // check if the middle vertex of a polygon diagonal is inside the polygon
            private static bool IsMiddleInside(Node a, Node b)
            {
                Node p = a;
                bool inside = false;
                double px = (a.X + b.X) / 2;
                double py = (a.Y + b.Y) / 2;
                do
                {
                    Node n = p.Next;
                    if (((p.Y > py) != (n.Y > py)) && (px < (n.X - p.X) * (py - p.Y) / (n.Y - p.Y) + p.X)) inside = !inside;
                    p = n;
                } while (p != a);

                return inside;
            }

            // link two polygon vertices with a bridge; if the vertices belong to the same ring, it splits
            // polygon into two; if one belongs to the outer ring and another to a hole, it merges it into a
            // single ring
            private static Node SplitPolygon(Node a, Node b)
            {
                var a2 = new Node(a.I, a.X, a.Y);
                var b2 = new Node(b.I, b.X, b.Y);
                Node an = a.Next;
                Node bp = b.Prev;

                a.Next = b;
                b.Prev = a;

                a2.Next = an;
                an.Prev = a2;

                b2.Next = a2;
                a2.Prev = b2;

                bp.Next = b2;
                b2.Prev = bp;

                return b2;
            }

            // create a node and optionally link it with previous one (in a circular doubly linked list)
            private Node InsertNode(int i, double x, double y, Node last)
            {
                var p = new Node(i, x, y);

                if (last == null)
                {
                    p.Prev = p;
                    p.Next = p;
                }
                else
                {
                    p.Next = last.Next;
                    p.Prev = last;
                    last.Next.Prev = p;
                    last.Next = p;
                }
                return p;
            }

            private void RemoveNode(Node p)
            {
                p.Next.Prev = p.Prev;
                p.Prev.Next = p.Next;

                if (p.PrevZ != null) p.PrevZ.NextZ = p.NextZ;
                if (p.NextZ != null) p.NextZ.PrevZ = p.PrevZ;

                // keep the hole-bridge index's block bboxes covering the healed prev->next edge
                if (_indexActive) GrowBlock(p.Prev, p.Next);
            }
        }
    }
}
