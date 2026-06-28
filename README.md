# 근전도 신호와 제어를 활용한 VR 촉각 구현 시스템
**팀명: 고잉메리호** | 건국대학교 전기전자공학부 | 2026 캡스톤디자인

---

## 프로젝트 개요

노트북 웹캠으로 손의 움직임을 실시간 추적하고, EMG 신호로 집는 힘을 감지하여 서보모터로 손가락에 촉각 피드백을 제공하는 VR 햅틱 글러브 시스템입니다.

```
웹캠 → MediaPipe (Unity 내장) → Unity (3D 손 렌더링 + 물체 집기)
                                        ↓ USB Serial
                                 Arduino (EMG + 서보모터)
```

---

## 개발 환경

| 항목 | 버전 |
|------|------|
| Unity | 6000.3.18f1 (URP) |
| MediaPipe Unity Plugin | 0.16.3 |
| 렌더 파이프라인 | Universal Render Pipeline (URP) |

---

## 초기 세팅 방법

### 1. 저장소 클론
```bash
git clone https://github.com/[팀저장소주소].git
cd [프로젝트폴더]
```

### 2. MediaPipe 모델 파일 다운로드
`hand_landmarker.task` 파일은 용량 문제로 저장소에 포함되지 않습니다.
아래 경로에 직접 다운로드하세요:

**다운로드 위치:**
```
Assets/StreamingAssets/hand_landmarker.task
```

**다운로드 URL (브라우저 주소창에 붙여넣기):**
```
https://storage.googleapis.com/mediapipe-models/hand_landmarker/hand_landmarker/float16/1/hand_landmarker.task
```

### 3. Unity Hub에서 프로젝트 열기
- Unity Hub → Open → 클론한 폴더 선택
- Unity 6000.3.18f1 버전으로 열기
- Library 폴더 자동 생성 (수 분 소요)

### 4. 씬 열기
```
Assets → Samples → MediaPipe Unity Plugin → 0.16.3
→ Official Solutions → Scenes → Hand Landmark Detection
→ Hand Landmark Detection.unity 더블클릭
```

### 5. Physics 설정 확인
```
Edit → Project Settings → Physics → Layer Collision Matrix
```
아래와 같이 설정되어 있어야 합니다:

| | Finger | GrabbableObject | Default |
|---|---|---|---|
| **Finger** | ☐ | ✅ | ☐ |

### 6. Input System 설정
```
Edit → Project Settings → Player → Other Settings
→ Active Input Handling → Both
```

---

## 씬 구성

```
Hierarchy
├── Main Camera
├── Directional Light
├── Main Canvas (비활성화)
├── EventSystem
├── Solution          ← MediaPipe HandLandmarkerRunner
├── HandVisualizer    ← 3D 손 시각화 + 집기 감지
├── Cube              ← 집을 수 있는 물체 (GrabbableObject Layer)
└── Plane             ← 바닥
```

---

## Layer 설정

| Layer | 번호 | 용도 |
|-------|------|------|
| Finger | 6 | 손가락 Capsule |
| GrabbableObject | 7 | 집을 수 있는 물체 |

---

## Inspector 설정값

### HandVisualizer
| 항목 | 기본값 | 설명 |
|------|--------|------|
| Hand Depth | 3 | 카메라에서 손까지 거리 |
| Smooth Speed | 60 | 손 추적 부드러움 |
| Reference Hand Size | 0.35 | 기준 손 크기 |
| Depth Strength | 1.5 | 깊이 변화 강도 |

### FingerCollisionDetector (Cube에 부착)
| 항목 | 기본값 | 설명 |
|------|--------|------|
| Follow Speed | 20 | 물체가 손 따라오는 속도 |
| Min Y | 0.25 | 바닥 최솟값 (Cube Scale 0.5 기준) |

---

## 실행 방법

**Play 버튼만 누르면 바로 실행됩니다!** (Python 불필요)

```
① Unity Play 버튼 클릭
② 웹캠 권한 허용
③ 웹캠 앞에 손을 올리면 3D 손 추적 시작
④ 손으로 Cube에 접근 후 집기 가능
```

---

## 파일 구조

```
Assets/
├── Scripts/
│   ├── HandVisualizer.cs       ← 손 시각화 + Capsule 생성
│   ├── FingerCollisionDetector.cs ← 집기 감지 + 물리 처리
│   └── FingerBone.cs           ← Capsule 손가락 번호 저장
├── StreamingAssets/
│   └── hand_landmarker.task    ← 별도 다운로드 필요
└── Samples/
    └── MediaPipe Unity Plugin/ ← 플러그인 샘플
```

---

## 트러블슈팅

| 증상 | 원인 | 해결 |
|------|------|------|
| 웹캠이 안 켜짐 | 권한 없음 | 웹캠 접근 허용 |
| `NullReferenceException: Bootstrap` | AppSettings 씬 없음 | Samples에서 AppSettings 씬 복구 |
| 손이 안 보임 | 모델 파일 없음 | hand_landmarker.task 다운로드 확인 |
| 물체가 날아감 | Layer 충돌 설정 문제 | Physics Layer Matrix 재설정 |
| Input System 에러 | 설정 문제 | Active Input Handling → Both |

---

## 팀원

| 역할 | 이름 | 담당 |
|------|------|------|
| 팀장 | 최호민 | |
| 팀원 | 김동휘 | |
| 팀원 | 권민규 | |
| 팀원 | 배건우 | Unity / MediaPipe |
| 팀원 | 이서우 | |

지도교수: 김선용 교수님 | 산업체 멘토: 한화 비전 연구원 김나연
