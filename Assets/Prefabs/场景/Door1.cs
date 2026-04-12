using System;
using System.Collections;
using System.Collections.Generic;
using ProjectII.Manager;
using UnityEngine;

namespace ProjectII.SceneItems
{
    public class Door1 : MonoBehaviour
    {
        private bool canInteract = false;
        
        

        private void OnTriggerEnter2D(Collider2D other)
        {
            GameObject obj = other.gameObject;
            if (GameSceneManager.Instance.CurrentPlayerCharacter.gameObject == obj)
            {
                Debug.Log("玩家接近门");
                canInteract = true;
            }
        }
    }

}