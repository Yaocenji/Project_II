using System;
using UnityEngine;
using ProjectII.Manager;

namespace ProjectII.SceneItems
{
    public class Door1 : InteractableBase
    {
        public GameObject door;

        public float closeAngle = 0;
        public float openAngle = 90;
        private float targetAngle;

        private void Start()
        {
            targetAngle = closeAngle;
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
            // TODO: 开门逻辑
        }

        private void Update()
        {
            
        }
    }
}
