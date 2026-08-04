using System;

namespace FragmentsUnity
{
    /// <summary>Cancellable progress channel a scene build reports through; a null reporter reports nothing and never cancels.</summary>
    public sealed class FragmentImportProgress
    {
        /// <summary>Kept coarse so a per-instance loop cannot repaint the editor progress bar once per body.</summary>
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

        /// <summary>Names the stage, the slice of overall progress it fills, and how many steps fill that slice.</summary>
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

        /// <summary>Reports overall progress under a stage name and returns false once the build has been cancelled.</summary>
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

        /// <summary>Advances the current stage by that many of its steps, reaching the reporter every StepsBetweenReports steps.</summary>
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
