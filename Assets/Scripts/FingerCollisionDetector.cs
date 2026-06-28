/*
 * FingerCollisionDetector.cs
 * Physics 기반 집기 감지 (OnCollision 사용)
 */

using System.Collections.Generic;
using UnityEngine;

public class FingerCollisionDetector : MonoBehaviour
{
    [Header("Hand Visualizer 연결")]
    public HandVisualizer handVisualizer;

    [Header("집기 설정")]
    public float followSpeed = 20f;

    [Header("물리 제한")]
    public float minY = 0.25f;

    [Header("디버그")]
    public bool showDebug = true;

    private static readonly string[] FingerNames = { "엄지", "검지", "중지", "약지", "소지" };
    private const int WristIndex = 0;

    private Rigidbody _rb;
    private bool      _isGrabbing = false;
    private Vector3   _grabOffset;

    // 손가락별 닿은 Bone 수 (같은 손가락의 여러 Capsule 처리)
    private Dictionary<int, int> _fingerBoneCount = new Dictionary<int, int>();

    void Start()
    {
        _rb = GetComponent<Rigidbody>();
        if (_rb == null)
            Debug.LogError("[FingerCollisionDetector] Rigidbody가 없습니다!");

        // Cube에 마찰력 추가
        var col = GetComponent<Collider>();
        if (col != null)
        {
            col.material = new PhysicsMaterial("GrabMaterial")
            {
                dynamicFriction = 0.8f,
                staticFriction  = 1.0f,
                bounciness      = 0f,
                frictionCombine = PhysicsMaterialCombine.Maximum,
            };
        }

        // Cube가 빠른 충돌도 감지하도록
        _rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        {
            handVisualizer = FindFirstObjectByType<HandVisualizer>();
            if (handVisualizer != null)
                Debug.Log("[FingerCollisionDetector] HandVisualizer 자동 연결 완료");
        }
    }

    // ── Physics 충돌 이벤트 ───────────────────────────────────────
    void OnCollisionEnter(Collision collision)
    {
        var bone = collision.gameObject.GetComponent<FingerBone>();
        if (bone == null) return;

        int finger = bone.fingerIndex;
        if (!_fingerBoneCount.ContainsKey(finger))
            _fingerBoneCount[finger] = 0;
        _fingerBoneCount[finger]++;

        if (showDebug)
            Debug.Log($"[충돌 시작] {FingerNames[finger]} (닿은 손가락: {_fingerBoneCount.Count}개)");

        CheckGrab();
    }

    void OnCollisionExit(Collision collision)
    {
        var bone = collision.gameObject.GetComponent<FingerBone>();
        if (bone == null) return;

        int finger = bone.fingerIndex;
        if (_fingerBoneCount.ContainsKey(finger))
        {
            _fingerBoneCount[finger]--;
            if (_fingerBoneCount[finger] <= 0)
                _fingerBoneCount.Remove(finger);
        }

        if (showDebug)
            Debug.Log($"[충돌 종료] {FingerNames[finger]} (닿은 손가락: {_fingerBoneCount.Count}개)");

        CheckGrab();
    }

    void CheckGrab()
    {
        int touchingCount = _fingerBoneCount.Count;

        bool newGrabbing;
        if (!_isGrabbing)
            newGrabbing = touchingCount >= 2;  // 집기 시작: 2개 이상
        else
            newGrabbing = touchingCount > 0;   // 해제: 완전히 떨어질 때만

        if (newGrabbing != _isGrabbing)
        {
            _isGrabbing = newGrabbing;
            if (_isGrabbing) OnGrabStart();
            else             OnGrabEnd();
        }
    }

    void Update()
    {
        if (!_isGrabbing) return;

        var joints = handVisualizer?.GetJoints();
        if (joints == null || joints[WristIndex] == null) return;

        // 집는 중: 손목 따라 이동
        Vector3 targetPos = joints[WristIndex].position + _grabOffset;
        _rb.MovePosition(Vector3.Lerp(transform.position, targetPos, Time.deltaTime * followSpeed));

        // Plane 밑으로 내려가지 않도록
        if (transform.position.y < minY)
        {
            Vector3 pos = transform.position;
            pos.y = minY;
            _rb.MovePosition(pos);
            Vector3 vel = _rb.linearVelocity;
            if (vel.y < 0) vel.y = 0;
            _rb.linearVelocity = vel;
        }
    }

    void OnGrabStart()
    {
        var joints = handVisualizer?.GetJoints();
        if (joints != null && joints[WristIndex] != null)
            _grabOffset = transform.position - joints[WristIndex].position;

        _rb.isKinematic    = true;
        _rb.linearVelocity = Vector3.zero;
        Debug.Log("[집기 시작] → 서보모터 ON");
    }

    void OnGrabEnd()
    {
        _rb.isKinematic = false;
        _fingerBoneCount.Clear();
        Debug.Log("[집기 종료] → 서보모터 OFF");
    }

    public bool IsGrabbing => _isGrabbing;
}
