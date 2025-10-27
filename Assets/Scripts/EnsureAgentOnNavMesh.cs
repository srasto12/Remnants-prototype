using UnityEngine;
using UnityEngine.AI;

/// 啟用時自動將 NavMeshAgent 吸附到最近的 NavMesh；找不到就先停用 Agent（避免 SetDestination 報錯）
[DefaultExecutionOrder(-100)]
public class EnsureAgentOnNavMesh : MonoBehaviour
{
    public float searchRadius = 10f;
    public bool logDetails = true;

    NavMeshAgent agent;

    void Awake() { agent = GetComponent<NavMeshAgent>(); }

    void OnEnable()
    {
        if (!agent) return;

        // 先關掉避免同幀內其他腳本就呼叫 SetDestination
        agent.enabled = false;

        if (NavMesh.SamplePosition(transform.position, out var hit, searchRadius, NavMesh.AllAreas))
        {
            agent.enabled = true; // Warp 需要 enabled=true
            bool warped = agent.Warp(hit.position);
            if (!warped)
            {
                if (logDetails) Debug.LogWarning($"[{name}] Warp 失敗，暫停 Agent 以避免報錯。");
                agent.enabled = false;
            }
        }
        else
        {
            if (logDetails) Debug.LogWarning($"[{name}] {searchRadius}m 內找不到 NavMesh，暫停 Agent。");
            agent.enabled = false;
        }
    }
}
