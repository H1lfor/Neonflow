using System;

namespace Neonflow
{
    public sealed class CalibrationHold
    {
        const double Duration = 3;
        double start = -1, elapsed;
        bool completed;
        public double Remaining => Math.Max(0, Duration - elapsed);

        public void Reset()
        {
            start = -1;
            elapsed = 0;
            completed = false;
        }

        public bool Tick(bool ready, double time)
        {
            if (!ready)
            {
                Reset();
                return false;
            }

            if (completed)
                return false;
            if (start < 0)
                start = time;
            elapsed = Math.Max(0, time - start);
            if (elapsed < Duration)
                return false;
            completed = true;
            return true;
        }
    }
}
