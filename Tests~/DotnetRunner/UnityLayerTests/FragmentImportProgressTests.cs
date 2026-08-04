using System.Collections.Generic;
using NUnit.Framework;

namespace FragmentsUnity.Tests
{
    [TestFixture]
    public sealed class FragmentImportProgressTests
    {
        private const string Stage = "Building geometry";
        private const string OtherStage = "Merging chunks";

        private const float NoProgress = 0f;
        private const float QuarterProgress = 0.25f;
        private const float HalfProgress = 0.5f;
        private const float CompleteProgress = 1f;
        private const float SpanStart = 0.2f;
        private const float SpanEnd = 0.8f;
        private const float Tolerance = 1e-5f;

        private const int LoopTotal = FragmentImportProgress.StepsBetweenReports * 4;

        [Test]
        public void Report_WithoutAReporter_NeverCancels()
        {
            var progress = new FragmentImportProgress(null);

            Assert.Multiple(() =>
            {
                Assert.That(progress.Report(Stage, HalfProgress), Is.True);
                Assert.That(progress.CancellationRequested, Is.False);
            });
        }

        [Test]
        public void Report_ReporterReturningFalse_RequestsCancellationAndReportsIt()
        {
            var recorder = new ProgressRecorder(1);
            var progress = new FragmentImportProgress(recorder.Report);

            Assert.Multiple(() =>
            {
                Assert.That(progress.Report(Stage, NoProgress), Is.False);
                Assert.That(progress.CancellationRequested, Is.True);
            });
        }

        [Test]
        public void Report_AfterCancellation_NeverReachesTheReporterAgain()
        {
            var recorder = new ProgressRecorder(1);
            var progress = new FragmentImportProgress(recorder.Report);

            progress.Report(Stage, NoProgress);
            progress.Report(Stage, HalfProgress);
            progress.Report(OtherStage, CompleteProgress);

            Assert.That(recorder.Values, Has.Count.EqualTo(1));
        }

        [Test]
        public void Report_ProgressGoingBackwards_IsHeldAtTheHighestValueReported()
        {
            var recorder = new ProgressRecorder();
            var progress = new FragmentImportProgress(recorder.Report);

            progress.Report(Stage, HalfProgress);
            progress.Report(Stage, NoProgress);

            Assert.That(recorder.Values, Is.EqualTo(new[] { HalfProgress, HalfProgress }));
        }

        [Test]
        public void Report_ProgressOutsideTheUnitRange_IsClampedIntoIt()
        {
            var recorder = new ProgressRecorder();
            var progress = new FragmentImportProgress(recorder.Report);

            progress.Report(Stage, -HalfProgress);
            progress.Report(Stage, CompleteProgress + HalfProgress);

            Assert.That(recorder.Values, Is.EqualTo(new[] { NoProgress, CompleteProgress }));
        }

        [Test]
        public void Report_StageName_ReachesTheReporterUnchanged()
        {
            var recorder = new ProgressRecorder();
            var progress = new FragmentImportProgress(recorder.Report);

            progress.Report(Stage, NoProgress);
            progress.Report(OtherStage, HalfProgress);

            Assert.That(recorder.Stages, Is.EqualTo(new[] { Stage, OtherStage }));
        }

        [Test]
        public void BeginStage_ReportsTheStartOfTheSpanUnderTheStageName()
        {
            var recorder = new ProgressRecorder();
            var progress = new FragmentImportProgress(recorder.Report);

            progress.BeginStage(Stage, SpanStart, SpanEnd, LoopTotal);

            Assert.Multiple(() =>
            {
                Assert.That(recorder.Stages, Is.EqualTo(new[] { Stage }));
                Assert.That(recorder.Values[0], Is.EqualTo(SpanStart).Within(Tolerance));
            });
        }

        [Test]
        public void ReportSteps_EveryStepsBetweenReportsItems_ReachesTheReporterOnce()
        {
            var recorder = new ProgressRecorder();
            var progress = new FragmentImportProgress(recorder.Report);
            progress.BeginStage(Stage, NoProgress, CompleteProgress, LoopTotal);
            recorder.Clear();

            for (int completed = 1; completed <= LoopTotal; completed++)
            {
                progress.ReportSteps(1);
            }

            Assert.That(recorder.Values, Has.Count.EqualTo(LoopTotal / FragmentImportProgress.StepsBetweenReports));
        }

        [Test]
        public void ReportSteps_ManyItemsAtOnce_StillReportsOnlyOnce()
        {
            var recorder = new ProgressRecorder();
            var progress = new FragmentImportProgress(recorder.Report);
            progress.BeginStage(Stage, NoProgress, CompleteProgress, LoopTotal);
            recorder.Clear();

            progress.ReportSteps(LoopTotal / 2);

            Assert.That(recorder.Values, Is.EqualTo(new[] { HalfProgress }));
        }

        [Test]
        public void ReportSteps_LastItemOfAShortLoop_StillReachesTheReporter()
        {
            var recorder = new ProgressRecorder();
            var progress = new FragmentImportProgress(recorder.Report);
            progress.BeginStage(Stage, NoProgress, CompleteProgress, 2);
            recorder.Clear();

            progress.ReportSteps(1);
            progress.ReportSteps(1);

            Assert.That(recorder.Values, Is.EqualTo(new[] { CompleteProgress }));
        }

        [Test]
        public void ReportSteps_FirstFewOfAWholeStage_StaysFarFromComplete()
        {
            var recorder = new ProgressRecorder();
            var progress = new FragmentImportProgress(recorder.Report);
            progress.BeginStage(Stage, NoProgress, CompleteProgress, LoopTotal);
            recorder.Clear();

            for (int completed = 1; completed <= FragmentImportProgress.StepsBetweenReports; completed++)
            {
                progress.ReportSteps(1);
            }

            Assert.That(recorder.Values, Is.EqualTo(new[] { QuarterProgress }));
        }

        [Test]
        public void ReportSteps_NoSteps_LeavesTheReporterAlone()
        {
            var recorder = new ProgressRecorder();
            var progress = new FragmentImportProgress(recorder.Report);
            progress.BeginStage(Stage, NoProgress, CompleteProgress, LoopTotal);
            recorder.Clear();

            bool advanced = progress.ReportSteps(0);

            Assert.Multiple(() =>
            {
                Assert.That(advanced, Is.True);
                Assert.That(recorder.Values, Is.Empty);
            });
        }

        [Test]
        public void ReportSteps_WithinAStage_MapsTheLoopFractionOntoTheStageSpan()
        {
            var recorder = new ProgressRecorder();
            var progress = new FragmentImportProgress(recorder.Report);
            progress.BeginStage(Stage, SpanStart, SpanEnd, LoopTotal);
            recorder.Clear();

            for (int completed = 1; completed <= LoopTotal; completed++)
            {
                progress.ReportSteps(1);
            }

            Assert.Multiple(() =>
            {
                Assert.That(recorder.Stages, Has.All.EqualTo(Stage));
                Assert.That(recorder.Values[0], Is.EqualTo(SpanStart + QuarterProgress * (SpanEnd - SpanStart)).Within(Tolerance));
                Assert.That(recorder.Values[recorder.Values.Count - 1], Is.EqualTo(SpanEnd).Within(Tolerance));
            });
        }

        [Test]
        public void ReportSteps_ReporterCancelling_StopsReportingAndFlagsCancellation()
        {
            var recorder = new ProgressRecorder(2);
            var progress = new FragmentImportProgress(recorder.Report);
            progress.BeginStage(Stage, NoProgress, CompleteProgress, LoopTotal);

            bool secondStep = progress.ReportSteps(LoopTotal);
            bool thirdStep = progress.ReportSteps(LoopTotal);

            Assert.Multiple(() =>
            {
                Assert.That(secondStep, Is.False);
                Assert.That(thirdStep, Is.False);
                Assert.That(progress.CancellationRequested, Is.True);
                Assert.That(recorder.Values, Has.Count.EqualTo(2));
            });
        }

        [Test]
        public void ReportSteps_AcrossSuccessiveStages_NeverReportsALowerValueThanBefore()
        {
            var recorder = new ProgressRecorder();
            var progress = new FragmentImportProgress(recorder.Report);

            progress.BeginStage(Stage, SpanStart, CompleteProgress, 4);
            progress.ReportSteps(4);
            progress.BeginStage(OtherStage, NoProgress, HalfProgress, 4);
            progress.ReportSteps(4);

            Assert.That(recorder.Values, Is.Ordered);
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

            internal void Clear()
            {
                Stages.Clear();
                Values.Clear();
            }

            internal bool Report(string stage, float normalizedProgress)
            {
                Stages.Add(stage);
                Values.Add(normalizedProgress);
                return Values.Count < _cancelOnCall;
            }
        }
    }
}
