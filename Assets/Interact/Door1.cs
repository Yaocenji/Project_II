using System;
using UnityEngine;
using ProjectII.Manager;

namespace ProjectII.Interact
{
    public class Door1 : InteractableBase
    {
        [Header("该门可以打开")]
        public bool doorCanOpen = true;
        [Header("FMOD")]
        [Tooltip("开门音效 FMOD 发射器引用")]
        public FMODUnity.StudioEventEmitter openEmitter;
        [Tooltip("关门音效 FMOD 发射器引用")]
        public FMODUnity.StudioEventEmitter closeEmitter;
        
        public float closeAngle = 0;
        public float openAngle = 90;
        
        private HingeJoint2D hinge2D;
        
        private bool isOpen = false;

        private float beginAngle;

        private void Start()
        {
            hinge2D = GetComponent<HingeJoint2D>();
            beginAngle = transform.eulerAngles.z;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            GameObject obj = other.gameObject;
            if (GameSceneManager.Instance.CurrentPlayerCharacter.gameObject == obj)
            {
                EnterInteractableRange();
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            GameObject obj = other.gameObject;
            if (GameSceneManager.Instance.CurrentPlayerCharacter.gameObject == obj)
            {
                ExitInteractableRange();
            }
        }

        protected override void OnInteract()
        {
            if (!doorCanOpen)
                return;
            
            if (!isOpen)
            {
                var newLimit = new JointAngleLimits2D();
                newLimit.max = closeAngle;
                newLimit.min = -openAngle;
                hinge2D.limits = newLimit;
                isOpen = true;
                // 给一个向上的力
                if (openAngle > 0)
                {
                    GetComponent<Rigidbody2D>().AddForce(0.5f * transform.up, ForceMode2D.Impulse);
                }
                else
                {
                    GetComponent<Rigidbody2D>().AddForce(-0.5f * transform.up, ForceMode2D.Impulse);
                }
                
                openEmitter.Play();
            }
            else
            {
                var newLimit = new JointAngleLimits2D();
                newLimit.max = 0;
                newLimit.min = 0;
                hinge2D.limits = newLimit;
                isOpen = false;
                
                closeEmitter.Play();
            }
        }

    }
}
