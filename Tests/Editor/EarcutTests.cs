using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace FragmentsUnity.Tests
{
    [TestFixture]
    public sealed class EarcutTests
    {
        private const double AreaTolerance = 1e-9;
        private const int CircleVertexCount = 100;
        private const long GenerousWorkBudget = 1000;

        [Test]
        public void Triangulate_UnitSquareCounterClockwise_CoversUnitArea()
        {
            List<List<double>> rings = UnitSquareCcw();

            List<int> indices = Earcut.Triangulate(rings);

            Assert.AreEqual(6, indices.Count);
            Assert.AreEqual(1.0, TriangulatedArea(rings, indices), AreaTolerance);
        }

        [Test]
        public void Triangulate_UnitSquareClockwise_CoversUnitArea()
        {
            var rings = new List<List<double>> { new List<double> { 0, 0, 0, 1, 1, 1, 1, 0 } };

            List<int> indices = Earcut.Triangulate(rings);

            Assert.AreEqual(6, indices.Count);
            Assert.AreEqual(1.0, TriangulatedArea(rings, indices), AreaTolerance);
        }

        [Test]
        public void Triangulate_SquareWithCenteredSquareHole_CoversRingArea()
        {
            var rings = new List<List<double>>
            {
                new List<double> { 0, 0, 1, 0, 1, 1, 0, 1 },
                new List<double> { 0.25, 0.25, 0.75, 0.25, 0.75, 0.75, 0.25, 0.75 }
            };

            List<int> indices = Earcut.Triangulate(rings);

            Assert.AreEqual(24, indices.Count);
            Assert.AreEqual(0.75, TriangulatedArea(rings, indices), AreaTolerance);
        }

        [Test]
        public void Triangulate_EmptyRingList_ReturnsEmpty()
        {
            List<int> indices = Earcut.Triangulate(new List<List<double>>());

            Assert.That(indices, Is.Empty);
        }

        [Test]
        public void Triangulate_TwoPointRing_ReturnsEmpty()
        {
            var rings = new List<List<double>> { new List<double> { 0, 0, 1, 0 } };

            List<int> indices = Earcut.Triangulate(rings);

            Assert.That(indices, Is.Empty);
        }

        [Test]
        public void Triangulate_SquareWithCollinearMidpoint_CoversUnitArea()
        {
            var rings = new List<List<double>> { new List<double> { 0, 0, 0.5, 0, 1, 0, 1, 1, 0, 1 } };

            List<int> indices = Earcut.Triangulate(rings);

            Assert.AreEqual(0, indices.Count % 3);
            Assert.AreEqual(1.0, TriangulatedArea(rings, indices), AreaTolerance);
        }

        [Test]
        public void TriangulateWithBudget_GenerousBudget_TriangulatesFullyAndDecrements()
        {
            List<List<double>> rings = UnitSquareCcw();
            long workBudget = GenerousWorkBudget;

            List<int> indices = Earcut.Triangulate(rings, ref workBudget, out bool budgetExhausted);

            Assert.IsFalse(budgetExhausted);
            Assert.AreEqual(6, indices.Count);
            Assert.AreEqual(1.0, TriangulatedArea(rings, indices), AreaTolerance);
            Assert.Less(workBudget, GenerousWorkBudget);
            Assert.Greater(workBudget, 0L);
        }

        [Test]
        public void TriangulateWithBudget_BudgetOfOne_ExhaustsWithNothingProduced()
        {
            List<List<double>> rings = UnitSquareCcw();
            long workBudget = 1;

            List<int> indices = Earcut.Triangulate(rings, ref workBudget, out bool budgetExhausted);

            Assert.IsTrue(budgetExhausted);
            Assert.LessOrEqual(workBudget, 0L);
            Assert.That(indices, Is.Empty);
        }

        [Test]
        public void TriangulateWithBudget_SpentBudget_StaysExhaustedOnNextCall()
        {
            long workBudget = 1;
            Earcut.Triangulate(UnitSquareCcw(), ref workBudget, out _);

            List<int> indices = Earcut.Triangulate(UnitSquareCcw(), ref workBudget, out bool budgetExhausted);

            Assert.IsTrue(budgetExhausted);
            Assert.LessOrEqual(workBudget, 0L);
            Assert.That(indices, Is.Empty);
        }

        [Test]
        public void Triangulate_HundredVertexCirclePolygon_ProducesFullTriangulation()
        {
            var rings = new List<List<double>> { BuildCircleRing(CircleVertexCount) };

            List<int> indices = Earcut.Triangulate(rings);

            Assert.AreEqual((CircleVertexCount - 2) * 3, indices.Count);
            double expectedArea = CircleVertexCount * Math.Sin(2.0 * Math.PI / CircleVertexCount) / 2.0;
            Assert.AreEqual(expectedArea, TriangulatedArea(rings, indices), AreaTolerance);
        }

        private static List<List<double>> UnitSquareCcw()
        {
            return new List<List<double>> { new List<double> { 0, 0, 1, 0, 1, 1, 0, 1 } };
        }

        private static List<double> BuildCircleRing(int vertexCount)
        {
            var ring = new List<double>(vertexCount * 2);
            for (int i = 0; i < vertexCount; i++)
            {
                double angle = 2.0 * Math.PI * i / vertexCount;
                ring.Add(Math.Cos(angle));
                ring.Add(Math.Sin(angle));
            }
            return ring;
        }

        private static double TriangulatedArea(List<List<double>> rings, List<int> indices)
        {
            var xs = new List<double>();
            var ys = new List<double>();
            foreach (List<double> ring in rings)
            {
                for (int i = 0; i + 1 < ring.Count; i += 2)
                {
                    xs.Add(ring[i]);
                    ys.Add(ring[i + 1]);
                }
            }

            double doubledSignedArea = 0.0;
            for (int i = 0; i < indices.Count; i += 3)
            {
                int a = indices[i];
                int b = indices[i + 1];
                int c = indices[i + 2];
                doubledSignedArea += (xs[b] - xs[a]) * (ys[c] - ys[a]) - (xs[c] - xs[a]) * (ys[b] - ys[a]);
            }
            return Math.Abs(doubledSignedArea) / 2.0;
        }
    }
}
