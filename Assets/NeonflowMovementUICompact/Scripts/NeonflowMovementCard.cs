using UnityEngine;
using UnityEngine.UI;

namespace Neonflow.Movements.Compact
{
    public enum MovementCardState { Idle, Current, Next, Success, Miss, Disabled, Hold, Focus }

    // Presentation only. Pose recognition and beat timing come from your game.
    public sealed class NeonflowMovementCard : MonoBehaviour
    {
        public Image box;
        public Image pictogram;
        public Text caption;
        public Image holdFill;
        public Sprite[] stateSprites;
        [SerializeField] MovementCardState currentState;

        public void SetPictogram(Sprite sprite, string label)
        {
            pictogram.sprite = sprite;
            pictogram.preserveAspect = true;
            caption.text = label;
        }

        public void SetState(MovementCardState state)
        {
            currentState = state;
            int index = (int)state;
            if (box != null && stateSprites != null && index < stateSprites.Length)
                box.sprite = stateSprites[index];
            if (holdFill != null) holdFill.transform.parent.gameObject.SetActive(state == MovementCardState.Hold);
        }

        public void SetHoldProgress(float value)
        {
            if (holdFill != null)
            {
                holdFill.fillAmount = Mathf.Clamp01(value);
                holdFill.transform.parent.gameObject.SetActive(currentState == MovementCardState.Hold || value > 0);
            }
        }
    }
}
