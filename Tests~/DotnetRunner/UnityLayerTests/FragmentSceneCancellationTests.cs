using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace FragmentsUnity.Tests
{
    [TestFixture]
    public sealed class FragmentSceneCancellationTests
    {
        private const string ModelName = "CancellationModel";
        private const string NodeCategory = "IFCWALL";
        private const string NodeNamePrefix = "Wall";
        private const string RootNodeName = "Site";
        private const int RootNodeLocalId = -1;

        private const int GeometryIndex = 0;
        private const int MaterialIndex = 0;
        private const int InstanceCount = FragmentImportProgress.StepsBetweenReports * 4;

        private const int CancelOnFirstCall = 1;
        private const int CancelOnFirstLoopReport = 3;

        private const float NoProgress = 0f;
        private const float CompleteProgress = 1f;
        private const float Tolerance = 1e-5f;

        private const int NoObjects = 0;

        [Test]
        public void Build_WithoutAProgressChannel_IsNeverCancelled([Values] FragmentImportMode mode)
        {
            FragmentSceneBuildResult scene = FragmentSceneBuilder.Build(
                FlatModel(), FragmentSceneFixture.Options(mode), null);

            Assert.Multiple(() =>
            {
                Assert.That(scene.Cancelled, Is.False);
                Assert.That(scene.Root, Is.Not.Null);
                Assert.That(scene.Meshes, Is.Not.Empty);
            });
        }

        [Test]
        public void Build_ReporterCancellingOnTheFirstReport_DiscardsEverythingItHadBuilt(
            [Values] FragmentImportMode mode)
        {
            var recorder = new ProgressRecorder(CancelOnFirstCall);

            FragmentSceneBuildResult scene = Build(FlatModel(), mode, recorder, null);

            Assert.Multiple(() =>
            {
                Assert.That(scene.Cancelled, Is.True);
                Assert.That(scene.Root, Is.Null);
                Assert.That(scene.Meshes, Is.Empty);
                Assert.That(scene.Materials, Is.Empty);
                Assert.That(scene.SpawnedNodes, Is.EqualTo(NoObjects));
                Assert.That(scene.MergedChunks, Is.EqualTo(NoObjects));
            });
        }

        [Test]
        public void Build_ReporterCancellingPartWay_StopsTheInstanceLoopAtThatReport()
        {
            var recorder = new ProgressRecorder(CancelOnFirstLoopReport);

            FragmentSceneBuildResult scene = Build(
                FlatModel(), FragmentImportMode.Instanced, recorder, null);

            Assert.Multiple(() =>
            {
                Assert.That(scene.Cancelled, Is.True);
                Assert.That(recorder.Values, Has.Count.EqualTo(CancelOnFirstLoopReport));
            });
        }

        [Test]
        public void Build_ReporterCancellingPartWay_StopsWalkingTheSpatialTree()
        {
            var recorder = new ProgressRecorder(CancelOnFirstLoopReport);

            FragmentSceneBuildResult scene = Build(
                HierarchyModel(), FragmentImportMode.HierarchyPerBody, recorder, null);

            Assert.Multiple(() =>
            {
                Assert.That(scene.Cancelled, Is.True);
                Assert.That(recorder.Values, Has.Count.EqualTo(CancelOnFirstLoopReport));
            });
        }

        [Test]
        public void Build_Cancelled_LogsTheCancellationInsteadOfTheBuildSummary()
        {
            var recorder = new ProgressRecorder(CancelOnFirstLoopReport);
            var messages = new List<string>();

            Build(FlatModel(), FragmentImportMode.Instanced, recorder,
                (severity, message) => messages.Add(message));

            Assert.That(messages, Is.EqualTo(new[] { FragmentSceneBuilder.CancelledMessage }));
        }

        [Test]
        public void Build_Completing_LogsTheBuildSummaryAndNoCancellation()
        {
            var recorder = new ProgressRecorder();
            var messages = new List<string>();

            Build(FlatModel(), FragmentImportMode.Instanced, recorder,
                (severity, message) => messages.Add(message));

            Assert.That(messages, Has.None.EqualTo(FragmentSceneBuilder.CancelledMessage));
        }

        [Test]
        public void Build_WithAProgressChannel_ReportsMonotonicallyFromZeroToOne(
            [Values] FragmentImportMode mode)
        {
            var recorder = new ProgressRecorder();

            Build(FlatModel(), mode, recorder, null);

            Assert.Multiple(() =>
            {
                Assert.That(recorder.Values, Is.Ordered);
                Assert.That(recorder.Values.First(), Is.EqualTo(NoProgress).Within(Tolerance));
                Assert.That(recorder.Values.Last(), Is.EqualTo(CompleteProgress).Within(Tolerance));
            });
        }

        [Test]
        public void Build_WithAProgressChannel_NamesMoreThanOneStage([Values] FragmentImportMode mode)
        {
            var recorder = new ProgressRecorder();

            Build(HierarchyModel(), mode, recorder, null);

            Assert.Multiple(() =>
            {
                Assert.That(recorder.Stages, Has.All.Matches<string>(stage => !string.IsNullOrEmpty(stage)));
                Assert.That(recorder.Stages.Distinct().Count(), Is.GreaterThan(1));
            });
        }

        [Test]
        public void Build_WithAProgressChannel_ReportsFarFewerTimesThanThereAreInstances()
        {
            var recorder = new ProgressRecorder();

            Build(FlatModel(), FragmentImportMode.Instanced, recorder, null);

            Assert.That(recorder.Values, Has.Count.LessThan(InstanceCount));
        }

        private static FragmentSceneBuildResult Build(
            FragmentImportResult result,
            FragmentImportMode mode,
            ProgressRecorder recorder,
            System.Action<FragmentImportSeverity, string> log)
        {
            var options = new FragmentSceneBuildOptions
            {
                Mode = mode,
                Progress = new FragmentImportProgress(recorder.Report)
            };
            return FragmentSceneBuilder.Build(result, options, log);
        }

        private static FragmentImportResult FlatModel()
        {
            var result = new FragmentImportResult { ModelName = ModelName };
            result.Geometries.Add(FragmentSceneFixture.Triangle(GeometryIndex));
            for (int localId = 0; localId < InstanceCount; localId++)
            {
                result.Instances.Add(FragmentSceneFixture.Instance(localId, GeometryIndex, MaterialIndex));
            }
            return result;
        }

        private static FragmentImportResult HierarchyModel()
        {
            FragmentImportResult result = FlatModel();
            FragmentSpatialNode root = FragmentSceneFixture.Node(RootNodeLocalId, NodeCategory, RootNodeName);
            foreach (FragmentInstance instance in result.Instances)
            {
                root.Children.Add(
                    FragmentSceneFixture.Node(instance.LocalId, NodeCategory, NodeNamePrefix + instance.LocalId));
            }
            result.SpatialRoot = root;
            return result;
        }

        private sealed class ProgressRecorder
        {
            private readonly int _cancelOnCall;

            internal ProgressRecorder() : this(int.MaxValue)
            {
            }

            internal ProgressRecorder(int cancelOnCall)
            {
                _cancelOnCall = cancelOnCall;
            }

            internal List<string> Stages { get; } = new List<string>();

            internal List<float> Values { get; } = new List<float>();

            internal bool Report(string stage, float normalizedProgress)
            {
                Stages.Add(stage);
                Values.Add(normalizedProgress);
                return Values.Count < _cancelOnCall;
            }
        }
    }
}
