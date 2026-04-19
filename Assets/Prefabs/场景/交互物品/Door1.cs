using System;
using UnityEngine;
using ProjectII.Manager;

namespace ProjectII.SceneItems
{
    public class Door1 : InteractableBase
    {
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
                Debug.Log("玩家接近门");
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
            Debug.Log("玩家与门交互");
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
            }
            else
            {
                var newLimit = new JointAngleLimits2D();
                newLimit.max = 0;
                newLimit.min = 0;
                hinge2D.limits = newLimit;
                isOpen = false;
            }
        }

        private void Update()
        {
            
        }
    }
}
