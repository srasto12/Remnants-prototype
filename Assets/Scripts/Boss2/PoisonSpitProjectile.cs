using System;
using UnityEngine;

public class PoisonSpitProjectile : MonoBehaviour
{
    // 路徑與運動參數
    private Vector3 _start;
    private Vector3 _end;
    private float _arcHeight;     // 0 = 直線
    private float _speed;         // 公尺/秒
    private float _t;             // 0→1

    private LayerMask _groundMask;
    private Action<Vector3, Vector3> _onHit;

    private Vector3 _prevPos;
    private float _approxLen;       // 路徑估算長度（以決定 t 的前進速度）

    // 初始化
    public void Init(Vector3 startPos, Vector3 endPos, float arcHeight, float speed, LayerMask groundMask, Action<Vector3, Vector3> onHit)
    {
        _start = startPos;
        _end = endPos;
        _arcHeight = Mathf.Max(0f, arcHeight);
        _speed = Mathf.Max(0.1f, speed);
        _groundMask = groundMask;
        _onHit = onHit;

        _t = 0f;
        _prevPos = _start;
        _approxLen = Vector3.Distance(_start, _end) * (_arcHeight > 0f ? 1.1f : 1f); // 弧線稍微放大估長

        transform.position = _start;
        UpdateRotationForward(_end - _start);
    }

    private void Update()
    {
        if (_approxLen <= 0.0001f) { LandAt(_end, Vector3.up); return; }

        // 依據速度換算 t 的增量（速度/總長）
        float dt = (_speed / _approxLen) * Time.deltaTime;
        _t = Mathf.Min(1f, _t + dt);

        // 取得新位置：用二次貝茲曲線（平滑弧線；_arcHeight=0 時等同直線）
        Vector3 newPos = Bezier2(_start, GetControlPoint(_start, _end, _arcHeight), _end, _t);

        // 在移動段上檢查是否撞到地（避免穿過薄地面）
        Vector3 segment = newPos - _prevPos;
        float segLen = segment.magnitude;
        if (segLen > 0f)
        {
            if (Physics.Raycast(_prevPos, segment.normalized, out RaycastHit hit, segLen + 0.05f, _groundMask))
            {
                LandAt(hit.point, hit.normal);
                return;
            }
        }

        transform.position = newPos;
        UpdateRotationForward(newPos - _prevPos);
        _prevPos = newPos;

        // 抵達終點：再往下微量射線找地面法線
        if (_t >= 1f)
        {
            Vector3 probe = newPos + Vector3.up * 0.5f;
            if (Physics.Raycast(probe, Vector3.down, out RaycastHit hit, 2f, _groundMask))
                LandAt(hit.point, hit.normal);
            else
                LandAt(newPos, Vector3.up);
        }
    }

    private void LandAt(Vector3 pos, Vector3 normal)
    {
        transform.position = pos;
        _onHit?.Invoke(pos, normal);
        Destroy(gameObject);
    }

    private static Vector3 Bezier2(Vector3 a, Vector3 c, Vector3 b, float t)
    {
        // 二次貝茲曲線： (1-t)^2 * a + 2(1-t)t * c + t^2 * b
        float u = 1f - t;
        return u * u * a + 2f * u * t * c + t * t * b;
    }

    private static Vector3 GetControlPoint(Vector3 a, Vector3 b, float arcHeight)
    {
        // 控制點放在中點再抬高 arcHeight（只抬 Y，做出拋物線感）
        Vector3 mid = (a + b) * 0.5f;
        mid.y = Mathf.Max(a.y, b.y) + arcHeight;
        return mid;
    }

    private void UpdateRotationForward(Vector3 vel)
    {
        vel.y = 0f; // 視覺上保持水平朝向（想要飛行朝上可註解這行）
        if (vel.sqrMagnitude > 1e-6f)
            transform.rotation = Quaternion.LookRotation(vel.normalized, Vector3.up);
    }
}
