using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace ProjectII.FX
{
    public class BulletRevolverFX_Tracer : MonoBehaviour
    {
        /// <summary>
        /// LineRenderer 用于绘制曳光弹到
        /// </summary>
        private LineRenderer lineRenderer;

        /// <summary>
        /// 曳光弹道宽度
        /// </summary>
        public float width = 0.05f;
        
        /// <summary>
        /// 曳光弹道起点
        /// </summary>
        private Vector3 startPos;
        /// <summary>
        /// 曳光弹道终点
        /// </summary>
        private Vector3 endPos;

        /// <summary>
        /// 曳光弹道速度
        /// </summary>
        public float speed = 50.0f;

        /// <summary>
        /// 曳光弹弹体长度
        /// </summary>
        public float length = 2.5f;

        /// <summary>
        /// 实时计算的曳光弹位置
        /// </summary>
        private Vector3[] positionData;
        
        bool isPlaying = false;

        private void Start()
        {
            lineRenderer = GetComponent<LineRenderer>();
            lineRenderer.startWidth = width;
            lineRenderer.endWidth = width;
            lineRenderer.positionCount = 2;
            
            positionData = new Vector3[2];
        }

        public void SetTracePosition(Vector3 startPos, Vector3 endPos)
        {
            this.startPos = startPos;
            this.endPos = endPos;
        }

        public void StartTracer()
        {
            if (!isPlaying)
                StartCoroutine(StartTracerIEnumerator());
        }

        IEnumerator StartTracerIEnumerator()
        {
            isPlaying = true;
            // 开始渲染
            lineRenderer.positionCount = 2;
            // 记录一下效果起始的时间
            float beginTime = Time.time;
            
            // 一个点走完的时间
            float pointAllTime = (startPos - endPos).magnitude / speed;
            // 完整曳光一条播出的时间
            float traceAllTime = length / speed;
            
            // 效果的播放时间：
            float playAllTime = pointAllTime + traceAllTime;
            
            for (;;)
            {
                float currTime = Time.time;
                float playTime = currTime - beginTime;
                // 时间到就退出
                if (playTime > playAllTime)
                {
                    break;
                }
                // 否则计算一下，当前的百分比
                float startPercent = Mathf.Clamp01(playTime / pointAllTime);
                float endPercent = Mathf.Clamp01((playTime - traceAllTime) / pointAllTime);
                // 设置点
                positionData[0] = Vector3.Lerp(startPos, endPos, startPercent);
                positionData[1] = Vector3.Lerp(startPos, endPos, endPercent);

                lineRenderer.SetPositions(positionData);
                
                yield return null;
            }
            isPlaying = false;
            // 停止渲染
            lineRenderer.positionCount = 0;
        }
    }
}
