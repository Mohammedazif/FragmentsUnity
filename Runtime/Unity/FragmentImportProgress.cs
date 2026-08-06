using System;

namespace FragmentsUnity
{
    /// <summary>Cancellable progress channel a scene build reports through; a null reporter never cancels.</summary>
    public sealed class FragmentImportProgress
    {
        public const int StepsBetweenReports = 256;

        private const float NoProgress = 0f;
        private const float CompleteProgress = 1f;

        private readonly FragmentProgressReporter _reporter;
        private string _stage = string.Empty;
        private float _stageStart = NoProgress;
        private float _stageEnd = CompleteProgress;
        private float _reportedProgress;
        private int _stageStepTotal;
        private int _completedSteps;
        private int _stepsSinceReport;

        public FragmentImportProgress(FragmentProgressReporter reporter)
        {
            _reporter = reporter;
        }

        public bool CancellationRequested { get; private set; }

        public bool BeginStage(string stage, float spanStart, float spanEnd, int stepTotal)
        {
            _stage = stage;
            _stageStart = Clamp(spanStart);
            _stageEnd = Math.Max(_stageStart, Clamp(spanEnd));
            _stageStepTotal = Math.Max(0, stepTotal);
            _completedSteps = 0;
            _stepsSinceReport = 0;
            return Report(stage, _stageStart);
        }

        /// <summary>Returns false once the build has been cancelled.</summary>
        public bool Report(string stage, float normalizedProgress)
        {
            if (CancellationRequested)
            {
                return false;
            }

            _reportedProgress = Math.Max(_reportedProgress, Clamp(normalizedProgress));
            if (_reporter != null && !_reporter(stage, _reportedProgress))
            {
                CancellationRequested = true;
            }
            return !CancellationRequested;
        }

        /// <summary>Reaches the reporter every StepsBetweenReports steps and once the stage completes.</summary>
        public bool ReportSteps(int steps)
        {
            if (CancellationRequested)
            {
                return false;
            }

            if (steps <= 0)
            {
                return true;
            }

            _completedSteps += steps;
            _stepsSinceReport += steps;
            if (_stepsSinceReport < StepsBetweenReports && _completedSteps < _stageStepTotal)
            {
                return true;
            }

            _stepsSinceReport = 0;
            float fraction = _stageStepTotal > 0
                ? Clamp(_completedSteps / (float)_stageStepTotal)
                : CompleteProgress;
            return Report(_stage, _stageStart + fraction * (_stageEnd - _stageStart));
        }

        private static float Clamp(float progress)
        {
            return Math.Clamp(progress, NoProgress, CompleteProgress);
        }
    }
}
