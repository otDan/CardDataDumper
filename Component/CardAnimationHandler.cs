using UnityEngine.EventSystems;
using UnityEngine;

namespace CardDataDumper.Component
{
    internal class CardAnimationHandler : MonoBehaviour
    {
        private bool toggled;
        
        public void ToggleAnimation(bool value)
        {
            foreach (Animator animatorComponent in gameObject.GetComponentsInChildren<Animator>())
            {
                if (animatorComponent.enabled == value) continue;
                animatorComponent.enabled = value;
            }

            foreach (PositionNoise positionComponent in gameObject.GetComponentsInChildren<PositionNoise>())
            {
                if (positionComponent.enabled == value) continue;
                positionComponent.enabled = value;
            }

            toggled = value;
        }

        public void SetAnimationPoint(float time)
        {
            foreach (Animator animatorComponent in gameObject.GetComponentsInChildren<Animator>())
            {
                animatorComponent.speed = 0;
                if (animatorComponent.layerCount > 0)
                {
                    animatorComponent.Play(animatorComponent.GetCurrentAnimatorStateInfo(0).fullPathHash, 0, time);
                }
            }
        }

        private void Update()
        {
            ToggleAnimation(toggled);
        }
    }
}
