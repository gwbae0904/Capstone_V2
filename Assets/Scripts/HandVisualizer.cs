using System.Collections.Generic;
using Mediapipe.Tasks.Vision.HandLandmarker;
using Mediapipe.Unity.Sample.HandLandmarkDetection;
using UnityEngine;

public class HandVisualizer : MonoBehaviour
{
    [Header("HandLandmarkerRunner 연결")]
    public HandLandmarkerRunner runner;

    [Header("손 시각화 설정")]
    public float sphereSize  = 0.02f;
    public float handDepth   = 3f;
    public float scaleZ      = 0.3f;
    public float smoothSpeed = 60f;

    [Header("깊이 추정 설정")]
    public float referenceHandSize = 0.35f;
    public float depthStrength     = 1.5f;

    private static readonly Color SkinColor     = new Color(0.95f, 0.75f, 0.55f);
    private static readonly Color SkinColorDark = new Color(0.85f, 0.65f, 0.48f);

    private static readonly (int, int)[] Connections = new (int, int)[]
    {
        (0,1),(1,2),(2,3),(3,4),
        (0,5),(5,6),(6,7),(7,8),
        (0,9),(9,10),(10,11),(11,12),
        (0,13),(13,14),(14,15),(15,16),
        (0,17),(17,18),(18,19),(19,20),
        (5,9),(9,13),(13,17),(0,17),
    };

    // 마디별 두께 (뿌리 굵고 끝 얇게)
    private static readonly float[] BoneRadius = new float[]
    {
        0.020f, 0.018f, 0.016f, 0.013f,  // 엄지
        0.022f, 0.018f, 0.016f, 0.013f,  // 검지
        0.022f, 0.020f, 0.017f, 0.014f,  // 중지
        0.021f, 0.018f, 0.016f, 0.013f,  // 약지
        0.019f, 0.016f, 0.014f, 0.011f,  // 소지
        0.028f, 0.028f, 0.026f, 0.024f,  // 손바닥
    };

    private static readonly int[] LandmarkFinger = new int[]
    {
        0, 0,0,0,0, 1,1,1,1, 2,2,2,2, 3,3,3,3, 4,4,4,4
    };

    private Transform[] _joints;
    private Transform[] _capsules;
    private Mesh        _palmMeshData;
    private GameObject  _palmObj;
    private Camera      _cam;

    private readonly object _lock = new();
    private List<(float x, float y, float z)> _pendingLandmarks = null;

    void Awake()
    {
        _cam = Camera.main;
        CreateJoints();
        CreateBones();
        CreatePalm();
    }

    void Start()
    {
        if (runner == null)
        {
            Debug.LogError("[HandVisualizer] Runner가 연결되지 않았습니다!");
            return;
        }
        StartCoroutine(SubscribeWhenReady());
    }

    System.Collections.IEnumerator SubscribeWhenReady()
    {
        yield return new UnityEngine.WaitForSeconds(1f);
        runner.OnLandmarkOutput += OnLandmarkResult;
        Debug.Log("[HandVisualizer] Runner 이벤트 구독 완료");
    }

    void Update()
    {
        List<(float x, float y, float z)> lms = null;
        lock (_lock)
        {
            if (_pendingLandmarks != null)
            {
                lms = _pendingLandmarks;
                _pendingLandmarks = null;
            }
        }
        if (lms != null)
            ApplyLandmarks(lms);

        UpdateBones();
        UpdatePalm();
    }

    public void OnLandmarkResult(HandLandmarkerResult result)
    {
        if (result.handLandmarks == null || result.handLandmarks.Count == 0)
            return;

        var landmarks = result.handLandmarks[0].landmarks;
        var lms = new List<(float x, float y, float z)>();
        foreach (var lm in landmarks)
            lms.Add((lm.x, lm.y, lm.z));

        lock (_lock) { _pendingLandmarks = lms; }
    }

    void ApplyLandmarks(List<(float x, float y, float z)> lms)
    {
        if (lms.Count < 21) return;

        float t = Time.deltaTime * smoothSpeed;

        float dx = lms[9].x - lms[0].x;
        float dy = lms[9].y - lms[0].y;
        float handSize     = Mathf.Sqrt(dx * dx + dy * dy);
        float sizeRatio    = Mathf.Clamp(handSize / referenceHandSize, 0.3f, 3f);
        float dynamicDepth = handDepth / (sizeRatio * depthStrength);

        var worldPos = new Vector3[21];
        for (int i = 0; i < 21; i++)
        {
            worldPos[i] = _cam.ViewportToWorldPoint(
                new Vector3(lms[i].x, 1f - lms[i].y, dynamicDepth + lms[i].z * scaleZ)
            );
        }

        this.transform.position = Vector3.Lerp(this.transform.position, worldPos[0], t);

        for (int i = 0; i < 21; i++)
        {
            if (_joints[i] == null) continue;
            _joints[i].localPosition = Vector3.Lerp(
                _joints[i].localPosition, worldPos[i] - worldPos[0], t);
        }
    }

    void CreateJoints()
    {
        _joints = new Transform[21];
        for (int i = 0; i < 21; i++)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = $"Joint_{i:D2}";
            go.transform.parent        = this.transform;
            go.transform.localScale    = Vector3.one * sphereSize;
            go.transform.localPosition = Vector3.zero;

            var rend = go.GetComponent<Renderer>();
            var mat  = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = SkinColorDark;
            rend.material = mat;

            Destroy(go.GetComponent<Collider>());
            _joints[i] = go.transform;
        }
    }

    void CreateBones()
    {
        _capsules = new Transform[Connections.Length];
        int fingerLayer = LayerMask.NameToLayer("Finger");

        // Capsule에도 마찰력 추가
        var physicsMat = new PhysicsMaterial("FingerMaterial")
        {
            dynamicFriction = 0.8f,
            staticFriction  = 1.0f,
            bounciness      = 0f,
            frictionCombine = PhysicsMaterialCombine.Maximum,
        };

        for (int i = 0; i < Connections.Length; i++)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = $"Bone_{i:D2}";
            go.transform.parent        = this.transform;
            go.transform.localPosition = Vector3.zero;

            if (fingerLayer >= 0) go.layer = fingerLayer;

            var rend = go.GetComponent<Renderer>();
            var mat  = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = SkinColor;
            rend.material = mat;

            // 마찰력 적용
            go.GetComponent<Collider>().material = physicsMat;

            // 손가락 번호 저장 (집기 감지용)
            var fingerBone = go.AddComponent<FingerBone>();
            fingerBone.fingerIndex = LandmarkFinger[Connections[i].Item1];

            // Kinematic Rigidbody 추가 (빠른 이동에도 충돌 처리)
            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic   = true;
            rb.useGravity    = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            _capsules[i] = go.transform;
        }
        Debug.Log("[HandVisualizer] 손가락 Capsule 생성 완료");
    }

    void CreatePalm()
    {
        _palmObj = new GameObject("Palm");
        _palmObj.transform.parent = this.transform;

        int fingerLayer = LayerMask.NameToLayer("Finger");
        if (fingerLayer >= 0) _palmObj.layer = fingerLayer;

        _palmMeshData = new Mesh { name = "PalmMesh" };
        _palmMeshData.vertices  = new Vector3[5];
        _palmMeshData.triangles = new int[] { 0,2,1, 0,3,2, 0,4,3 };

        var mf = _palmObj.AddComponent<MeshFilter>();
        mf.mesh = _palmMeshData;

        var mr = _palmObj.AddComponent<MeshRenderer>();
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = SkinColor;
        mr.material = mat;
    }

    void UpdateBones()
    {
        if (_joints == null || _capsules == null) return;
        for (int i = 0; i < Connections.Length; i++)
        {
            var (a, b) = Connections[i];
            if (_joints[a] == null || _joints[b] == null) continue;

            Vector3 posA = _joints[a].position;
            Vector3 posB = _joints[b].position;
            Vector3 dir  = posB - posA;
            float   len  = dir.magnitude;
            if (len < 0.0001f) continue;

            float radius = i < BoneRadius.Length ? BoneRadius[i] : 0.018f;

            _capsules[i].position = (posA + posB) * 0.5f;
            _capsules[i].rotation = Quaternion.FromToRotation(Vector3.up, dir);
            _capsules[i].localScale = new Vector3(radius * 2f, len * 0.5f, radius * 2f);
        }
    }

    void UpdatePalm()
    {
        if (_palmMeshData == null || _joints == null) return;

        int[] pts = { 0, 5, 9, 13, 17 };
        var verts = new Vector3[5];
        for (int i = 0; i < 5; i++)
            verts[i] = _joints[pts[i]].position;

        _palmMeshData.vertices = verts;
        _palmMeshData.RecalculateNormals();
    }

    public Transform[] GetJoints() => _joints;
}
