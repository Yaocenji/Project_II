using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace ProjectII.FX
{
    public class FMOD_Snapshot_Trigger : MonoBehaviour
    {
        /// <summary>
        /// 场景效果快照名称
        /// </summary>
        public string snapshotName;
        /// <summary>
        /// 场景效果快照实例
        /// </summary>
        private FMOD.Studio.EventInstance snapshot;

        private void Start()
        {
            snapshot = FMODUnity.RuntimeManager.CreateInstance(snapshotName);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag("Player"))
            { 
                snapshot.start();
                //Debug.Log("Snapshot triggered: " + snapshotName);
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other.CompareTag("Player"))
            { 
                snapshot.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
                //Debug.Log("Snapshot stopped: " + snapshotName);
            }
        }
    }
}
