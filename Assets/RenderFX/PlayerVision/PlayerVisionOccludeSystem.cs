using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using RadianceCascadesWorldBVH;

namespace ProjectII.Render
{
    /// <summary>
    /// 单例管理器，统一维护玩家视野遮挡排除的 flag buffer。
    /// PlayerVisionNoOcclude 和 PlayerVisionExcludeList 都向此注册，
    /// 每帧 LateUpdate 末尾重建 flag 数组并上传 Shader。
    /// </summary>
    [DefaultExecutionOrder(100)] // 晚于 NoOcclude 和 ExcludeList 的注册
    public class PlayerVisionOccludeSystem : MonoBehaviour
    {
        public static PlayerVisionOccludeSystem Instance { get; private set; }

        // 静态排除：PlayerVisionNoOcclude 注册
        private readonly HashSet<RCWBObject> m_StaticExcludes = new HashSet<RCWBObject>();
        // 动态排除：PlayerVisionExcludeList 每帧覆盖写入
        private readonly HashSet<RCWBObject> m_DynamicExcludes = new HashSet<RCWBObject>();

        private ComputeBuffer m_FlagBuffer;
        private int[] m_Flags = new int[64];

        private static readonly int ShaderPropFlags = Shader.PropertyToID("_PlayerVision_OccludeFlags");
        private static readonly int ShaderPropCount = Shader.PropertyToID("_PlayerVision_OccludeFlagsCount");

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            m_FlagBuffer?.Release();
            m_FlagBuffer = null;
            Shader.SetGlobalInt(ShaderPropCount, 0);
        }

        // ── 静态排除注册（PlayerVisionNoOcclude 调用） ──

        public void RegisterStatic(RCWBObject obj)
        {
            if (obj != null) m_StaticExcludes.Add(obj);
        }

        public void UnregisterStatic(RCWBObject obj)
        {
            m_StaticExcludes.Remove(obj);
        }

        // ── 动态排除（PlayerVisionExcludeList 每帧调用，覆盖上一帧） ──

        public void SetDynamicExcludes(IEnumerable<RCWBObject> objs)
        {
            m_DynamicExcludes.Clear();
            foreach (var obj in objs)
                if (obj != null) m_DynamicExcludes.Add(obj);
        }

        // ── 每帧上传 ──

        private void LateUpdate()
        {
            var core = PolygonManagerCore.Instance;
            if (core == null) return;

            List<RCWBObject> allObjects = core.RcwObjects;
            int matCount = allObjects.Count;
            if (matCount == 0) return;

            // 扩容 flag 数组
            if (m_Flags.Length < matCount)
                m_Flags = new int[Mathf.NextPowerOfTwo(matCount)];

            // 全部初始化为 1（参与遮挡）
            for (int i = 0; i < matCount; i++)
                m_Flags[i] = 1;

            // 静态排除
            foreach (var obj in m_StaticExcludes)
            {
                int idx = allObjects.IndexOf(obj);
                if (idx >= 0 && idx < matCount) m_Flags[idx] = 0;
            }

            // 动态排除
            foreach (var obj in m_DynamicExcludes)
            {
                int idx = allObjects.IndexOf(obj);
                if (idx >= 0 && idx < matCount) m_Flags[idx] = 0;
            }

            // 确保 buffer 容量
            if (m_FlagBuffer == null || m_FlagBuffer.count < matCount)
            {
                m_FlagBuffer?.Release();
                m_FlagBuffer = new ComputeBuffer(Mathf.NextPowerOfTwo(matCount), Marshal.SizeOf<int>());
            }

            m_FlagBuffer.SetData(m_Flags, 0, 0, matCount);
            Shader.SetGlobalBuffer(ShaderPropFlags, m_FlagBuffer);
            Shader.SetGlobalInt(ShaderPropCount, matCount);
        }
    }
}
