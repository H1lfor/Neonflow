using UnityEngine;

namespace Neonflow
{
    // Anatomical names, +Z forward. Mirroring belongs to the presentation controller.
    public sealed class RobotRig : MonoBehaviour
    {
        public string characterId;
        public Transform[] bones = new Transform[(int)HumanBodyBones.LastBone];
        public float poseScale, shoulderHeight;
        public Transform Bone(HumanBodyBones id) => bones[(int)id];
        public void Ready() => Pose(new[] { new Vector2(-.85f,-.4f),new Vector2(-1.1f,-1.05f),new Vector2(.85f,-.4f),new Vector2(1.1f,-1.05f) },null,null,"ready");
        public void Pose(Vector2[] screenPoints, string left, string right, string pose, float health = 100, float robotAccuracy = 1)
        {
            for (int side = 0; side < 2; side++)
            {
                // A front-facing model's anatomical right is the left side of the screen.
                bool anatomicalLeft = side == 1;
                float sign = anatomicalLeft ? -1 : 1;
                var shoulder = Bone(anatomicalLeft ? HumanBodyBones.LeftShoulder : HumanBodyBones.RightShoulder);
                var upper = Bone(anatomicalLeft ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm);
                var lower = Bone(anatomicalLeft ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm);
                var hand = Bone(anatomicalLeft ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
                var rootShoulder = transform.InverseTransformPoint(shoulder.position);
                rootShoulder.y = shoulderHeight + (pose == "shoulders_up" ? .045f : 0);
                shoulder.position = transform.TransformPoint(rootShoulder);
                Vector3 elbow = new Vector3(-screenPoints[side * 2].x * poseScale, shoulderHeight + screenPoints[side * 2].y * poseScale, .015f);
                Vector3 wrist = new Vector3(-screenPoints[side * 2 + 1].x * poseScale, shoulderHeight + screenPoints[side * 2 + 1].y * poseScale, .08f);
                if (pose == "cross_chest") { elbow.z = .14f; wrist.z = side == 0 ? .24f : .32f; }
                if (pose == "clap") { wrist.x = sign * .025f; wrist.z = .31f; elbow.z = .1f; }
                if (robotAccuracy < .5f) { wrist.y -= .22f; wrist.x += sign * .08f; }
                if (health <= 0) { elbow = new Vector3(sign * .31f, shoulderHeight - .27f, 0); wrist = new Vector3(sign * .29f, shoulderHeight - .55f, .05f); }
                SolveArm(upper, lower, hand, elbow, wrist, sign);
                string gesture = side == 0 ? left : right;
                hand.rotation = transform.rotation * Quaternion.Euler(0, pose == "clap" ? sign * 65 : 0, gesture == "point_side" ? 0 : sign * 90);
                if (screenPoints[side * 2 + 1].y < -.8f && gesture != "point_side")
                    hand.rotation = transform.rotation * Quaternion.FromToRotation(Vector3.right * sign, transform.InverseTransformDirection(hand.position - lower.position));
                Fingers(anatomicalLeft, gesture ?? "open");
            }
            Bone(HumanBodyBones.Head).localRotation = Quaternion.Euler(health <= 0 ? 20 : 0, 0, 0);
        }

        void SolveArm(Transform upper, Transform lower, Transform hand, Vector3 pole, Vector3 target, float sign)
        {
            Vector3 start = transform.InverseTransformPoint(upper.position);
            float a = lower.localPosition.magnitude, b = hand.localPosition.magnitude;
            Vector3 delta = target - start;
            float d = Mathf.Clamp(delta.magnitude, Mathf.Abs(a - b) + .001f, a + b - .001f);
            Vector3 axis = delta.sqrMagnitude > .00001f ? delta.normalized : Vector3.down;
            float along = (a * a - b * b + d * d) / (2 * d);
            float height = Mathf.Sqrt(Mathf.Max(0, a * a - along * along));
            Vector3 bend = pole - start;
            bend -= axis * Vector3.Dot(bend, axis);
            if (bend.sqrMagnitude < .00001f) bend = Vector3.Cross(axis, Vector3.forward);
            Vector3 elbow = start + axis * along + bend.normalized * height;
            Vector3 wrist = start + axis * d;
            upper.rotation = transform.rotation * Quaternion.FromToRotation(Vector3.right * sign, elbow - start);
            lower.rotation = transform.rotation * Quaternion.FromToRotation(Vector3.right * sign, wrist - elbow);
        }

        public void Fingers(bool left, string gesture)
        {
            float sign = left ? -1 : 1;
            var extended = PoseEvaluator.Fingers(gesture);
            int first = (int)(left ? HumanBodyBones.LeftThumbProximal : HumanBodyBones.RightThumbProximal);
            for (int digit = 0; digit < 5; digit++)
                for (int joint = 0; joint < 3; joint++)
                {
                    var bone = bones[first + digit * 3 + joint];
                    float curl = extended[digit] ? 0 : joint == 0 ? 72 : joint == 1 ? 92 : 62;
                    float spread = 0;
                    if (digit == 0 && joint == 0)
                    {
                        spread = extended[0] ? gesture == "thumb_up" ? 0 : gesture == "l_shape" ? 90 : 48 : -72;
                        if (!extended[0]) curl = 30;
                    }
                    else if (digit > 0 && joint == 0 && extended[digit]) spread = (2.5f - digit) * 3;
                    if (digit == 0 && joint > 0 && !extended[0]) curl = joint == 1 ? 50 : 45;
                    bone.localRotation = Quaternion.Euler(0, -sign * curl, sign * spread);
                }
        }
    }
}
