using System;
using UnityEngine;

namespace Neonflow
{
    public sealed class RobotView : MonoBehaviour
    {
        public RobotRig Rig { get; private set; }
        public string CharacterId => Rig ? Rig.characterId : "";
        Cue cue;
        float hp = 100;
        Vector2[] positions;
        public double Beat;
        public bool Paused, ReducedMotion;

        public void Build(string characterId = "light") => SetCharacter(characterId);
        public void SetCharacter(string characterId)
        {
            if (Rig && Rig.characterId == characterId) return;
            if (Rig) { Rig.gameObject.SetActive(false); Destroy(Rig.gameObject); }
            var prefab = Resources.Load<RobotRig>("Characters/" + characterId);
            if (!prefab) throw new InvalidOperationException("Missing character prefab: " + characterId);
            Rig = Instantiate(prefab, transform, false);
            Rig.transform.localRotation = Quaternion.Euler(0, 180, 0);
            positions = PoseEvaluator.Template("shoulders_down");
            cue = null;
            hp = 100;
            SampleImmediate();
        }
        public void SetCue(Cue c, float health) { cue = c; hp = health; }
        public void SampleImmediate()
        {
            if (!Rig) return;
            positions = PoseEvaluator.Template(cue?.pose ?? "shoulders_down");
            Apply();
        }
        void Update()
        {
            if (!Rig || Paused) return;
            var desired = PoseEvaluator.Template(cue?.pose ?? "shoulders_down");
            float blend = 1 - Mathf.Exp(-Time.deltaTime * 7);
            for (int i = 0; i < 4; i++) positions[i] = Vector2.Lerp(positions[i], desired[i], blend);
            Apply();
        }
        void Apply()
        {
            Rig.transform.localPosition = new Vector3(0, ReducedMotion ? 0 : (float)Math.Sin(Beat * Math.PI * 2) * .012f, 0);
            Rig.Pose(positions, cue?.left, cue?.right, cue?.pose, hp, cue?.robotAccuracy ?? 1);
        }
    }
}
