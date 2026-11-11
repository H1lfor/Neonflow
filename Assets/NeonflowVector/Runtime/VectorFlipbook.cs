using UnityEngine;
using UnityEngine.UI;

namespace Neonflow.VectorUI
{
    // Use either these frames or an Animator clip on an Image, never both writers.
    public class VectorFlipbook : MonoBehaviour
    {
        public Image target;
        public Sprite[] frames;
        public float fps = 40;
        public bool loop;
        public bool playOnEnable;
        public bool externalClock;
        public bool reducedMotion;
        bool playing;
        double start;
        int last = -1;
        void OnEnable()
        {
            if (playOnEnable)
                Play();
            else
                SetFrame(0);
        }

        public void Play()
        {
            start = Time.unscaledTimeAsDouble;
            playing = true;
            last = -1;
            SetFrame(0);
        }

        public void Stop()
        {
            playing = false;
            SetFrame(0);
        }

        public void SampleSeconds(double elapsed)
        {
            if (frames == null || frames.Length == 0 || fps <= 0)
                return;
            int n = Mathf.Max(0, (int)System.Math.Floor(elapsed * fps));
            if (loop)
                n %= frames.Length;
            else
                n = Mathf.Min(n, frames.Length - 1);
            SetFrame(reducedMotion ? 0 : n);
        }

        public void SamplePhase(float phase)
        {
            if (frames == null || frames.Length == 0)
                return;
            SetFrame(reducedMotion ? 0 : Mathf.Clamp(Mathf.FloorToInt(phase * frames.Length), 0, frames.Length - 1));
        }

        void Update()
        {
            if (playing && !externalClock)
            {
                double elapsed = Time.unscaledTimeAsDouble - start;
                SampleSeconds(elapsed);
                if (!loop && frames != null && elapsed >= frames.Length / fps)
                    playing = false;
            }
        }

        void SetFrame(int n)
        {
            if (!target || frames == null || frames.Length == 0 || n == last)
                return;
            last = n;
            target.sprite = frames[n];
        }
    }
}
