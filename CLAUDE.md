# 공포 게임 졸업 프로젝트 — 진행 요약

> 이 문서는 claude.ai에서 진행한 설계·구현 대화를 Claude Code로 이어가기 위한 요약입니다.
> Claude Code는 이 문서 + 실제 코드 파일을 함께 읽고 맥락을 파악하면 됩니다.

---

## 1. 프로젝트 개요

- **장르**: 3D 쿼터뷰(약 45~60° 카메라, The Last Campfire 류) **스텔스 호러** 게임
- **엔진/언어**: Unity / C# (에디터: VS Code)
- **학술적 핵심 가치**: AI 시스템 (GOAP, A*, 감지). 아트가 아니라 **AI가 차별점**
- **핵심 루프**: 플레이어가 여러 발전기를 켜며(강제 소음 = 노출) 추격자를 피하다 출구로 탈출. 추격자는 **싸울 수 없음**(순수 스텔스 회피)
- **개발 철학**: 로직을 프리미티브로 먼저 완성하고 아트는 마지막. 에셋은 Asset Store/Mixamo/무료 활용 권장

### 차원 메커니즘 (핵심 차별화)
- 두 차원이 하나의 맵을 공유, 플레이어는 지정된 존에서만 차원 이동(제한적, 호러에 적합)
- **각 적은 한 차원에만** 존재. **적은 차원 이동을 이해 못 함**(플레이어가 "사라진" 것으로 인식)
- 차원 이동이 완벽한 도주가 되지 않도록:
  - 경계 수준이 서서히 감쇠(Alerted→Suspicious→Calm)해서 돌아오는 게 위험
  - **발전기 소리는 차원 관통**, **발소리는 차원 내에서만** — 누가 ReportSound를 받는지로 제어(블랙보드가 아니라 감지 시스템이 결정)
- 이 조합(쿼터뷰 호러 + 능동적 차원 이동 + GOAP 다중 추격자 + 차원별 소리 규칙)은 **웹 검색으로 신규성 확인됨**

---

## 2. 현재 코드 구조

### namespace 구성
- `Astar3D` — 3D A* 길찾기 (활성)
- `Astar` — 2D A* (구버전)
- `Squad` — GOAP 판단 + 블랙보드
- `Detection` (예정) — 감지 시스템 (지금 만드는 중)

### Astar3D/ (3D A* 길찾기, 활성)
- `Node.cs` — Vector3, XZ 평면
- `NodeHeap.cs` — 최소 힙
- `PathGrid.cs` — Physics.CheckBox로 walkable 판정 (Grid→PathGrid로 개명, Unity Grid 충돌 회피)
- `Pathfinder.cs` — FindPath(Vector3,Vector3), octile 휴리스틱
- `ChaserAgent.cs` — **DEPRECATED** (ChaserLocomotion+HorrorChaserAgent로 대체됨, 제거 예정)

### SquadAI/ (GOAP, namespace Squad)
**GOAP 엔진 (범용, 재사용 가능):**
- `Goap.cs` — 엔진. 4개 클래스:
  - `WorldState` — Facts(readonly Dictionary<string,bool>). Clone(복제), Apply(효과 병합-덮어쓰기), Matches(goal의 fact만 검사, goal⊆current면 true), DistanceTo(안 맞는 fact 수 = 휴리스틱, admissible)
  - `GoapAction` — abstract. Preconditions/Effects/GetCost/CheckProceduralPrecondition/Perform. **행동마다 Perform 코드가 달라 상속 사용**
  - `Goal` — 순수 데이터(Name/Desired/Priority). **코드 없어 상속 불필요, 값만 채움**
  - `GoapPlanner` — static. 상태공간 A*. List.Sort(작은 상태공간이라 힙 불필요), closedSet 없음(중복 허용, 단순화)

**이 게임 전용 (엔진 사용자):**
- `HorrorChaserAgent.cs` — 세 목표 단일 추격자 두뇌. Update 루프(replan 타이머 0.4s + Perform 실행), BuildWorldState/SelectGoal/GoalIsRelevant/Fact(이 게임 전용이라 여기 있음, Goap.cs 아님), CurrentGoalName/CurrentActionName(디버그용 property). **AdvanceToNextAction() 추출 권장(Update와 Replan에 중복)**
- `ChaserGoals.cs` — 세 목표 값 채우기. CatchPlayer(100)>InvestigateSound(50)>Wander(1). 우선순위에 간격 둠(중간 삽입 대비). 각 Desired에 fact 하나씩
- `ChaserActions.cs` — 네 행동 서브클래스. ReachPlayer/MoveToSound/SearchSoundArea/WanderStep. Perform에서 실제 실행(Effects는 계획용 설계도, 실제 잡기는 Perform)
- `ChaserLocomotion.cs` — Astar3D 래핑. MoveTo(공개 명령: 도착판정+경로계산여부+FollowPath), FollowPath(private 실행: 웨이포인트 따라 rigidbody.MovePosition), LookAround, GetWanderTarget/ClearWanderTarget(배회목표 지연초기화 쌍). y 고정으로 XZ 평면 유지
- `SquadBlackboard.cs` — 씬 싱글톤. AlertLevel enum(Calm/Suspicious/Alerted). 경계 감쇠(alertedToSuspiciousTime=12s, suspiciousToCalmTime=25s, **둘 다 SerializeField로 뺐음**). ReportSighting/ReportLostSight/ReportSound/ClearSound. TimeSinceLastSeen=Mathf.Infinity 초기화("한번도 못 봄"). ReportLostSight는 LastKnownPlayerPos 유지(수색 단서). TryClaimRole/ReleaseRole(다중 추격자 역할 조율, 현재 미사용)

**구버전 협력 데모 (다중 추격자용으로 보존):**
- `SquadAgent.cs` — 성격별(공격/매복/수색) 협력 추격자 두뇌
- `Actions.cs` — 협력용 행동 + GoalSet + ActionFactory

### 컨텍스트 객체
- `ChaserContext` (SquadAgentContext에서 개명) — 행동에 전달되는 파라미터 묶음. Agent/Blackboard/Self/Player/Locomotion/CatchRadius/ArriveRadius. HorrorChaserAgent는 Agent=null(SquadAgent 아니므로). **별도 파일로 분리 권장**(현재 SquadAgent.cs에 얹혀 있어 HorrorChaserAgent가 그 파일에 의존)

---

## 3. 두 층위의 A* (중요 개념)

1. **GoapPlanner** — 상태공간 A*. 노드=월드상태, 엣지=행동. "무엇을 할지" 결정
2. **Pathfinder** — 그리드 A*. 노드=칸, 엣지=이동. "어떻게 갈지" 결정

**동일한 A* 원리**(f=g+h, open set, 역추적)를 **두 다른 문제공간**에 적용. 규모 차이로 구현은 다름(그리드=힙+closedSet+노드갱신 / GOAP=List.Sort+중복허용).

### 이동 흐름
HorrorChaserAgent.Update → currentAction.Perform → ChaserLocomotion.MoveTo → Pathfinder.FindPath(그리드A*)로 _path 계산 → FollowPath로 웨이포인트 따라 rigidbody 이동. Perform은 프레임당 한 걸음, 도착 전 false / 도착 시 true 반환 → 다음 행동으로.

---

## 4. 완성 / 진행 중 / 남은 것

### ✅ 완성
- 2D + 3D A* 길찾기 (비이동 버그 수정 완료: 중복 PathGrid + 잘못된 mask/world-size였음)
- GOAP 엔진 + 세 목표 단일 추격자 (컴파일 OK, 협력 데모와 공존)
- 경계 감쇠 시간 SerializeField화
- SquadAgentContext→ChaserContext 개명

### 🔶 진행 중 — 감지 시스템 (namespace Detection 권장)
**시야 감지 3단계 함수 완성됨** (사용자가 직접 작성, 리뷰 완료):
- 거리 체크 — 단, **플레이어가 유일하므로 OverlapSphere+배열 대신 플레이어 직접 참조로 단순화 권장** (targetLayer도 불필요해짐)
- 각도 체크 `IsInViewAngle` — 방향벡터 y=0 평면화, 0벡터 예외, `viewAngle * 0.5f` 반각 비교(정확)
- 차폐 체크 `HasLineOfSight` — eyeHeight에서 레이캐스트, 대상 몸통(1.2) 겨냥, distance 제한, obstacleLayer. **1.2를 SerializeField로 뺄 것 권장**

**남은 감지 작업:**
- **메인 로직**: 세 함수를 `&&`로 엮어 주기적(0.1~0.2s) 실행 → 통과하면 ReportSighting, 아니면 ReportLostSight
- **상태 전환 감지 필요**: `_wasVisible` 변수로 "직전 보임 + 지금 안 보임"일 때만 ReportLostSight (한번도 못 봤을 때 매프레임 호출 방지). ReportSighting은 위치갱신이라 매번 OK
- **same-dimension 체크**를 관문에 추가(차원 시스템 만들 때)
- HearingSensor (소리 이벤트 방식)

### ⬜ 남은 것 (이후)
- **소리 시스템 확장** (설계 완료, 구현 남음):
  - SoundType을 **클래스(또는 ScriptableObject)로** — 소리는 데이터만 다르므로 상속 아닌 값 채우기(Goal과 같은 원리). 속성: radius, alertLevel, crossesDimension 등
  - 소리 종류별 경계 강도 차등화(발전기=강함/관통, 발소리=약함/차원내)
  - 소리 종류 확장(달리기, 물건소리, 미끼 등). 미끼는 InvestigateSound 활용
- 쿼터뷰 카메라 + 플레이어 컨트롤러
- 차원 메커니즘 구현(전환존, 차원별 적/발전기/소리)
- **다중 추격자**: HorrorChaserAgent 여러개(블랙보드 공유) 또는 SquadAgent 성격 부활
- **거리 기반 AlertLevel 공유**(사용자 아이디어): 플레이어 발견한 적과 일정 거리 내 적만 경계 공유. "같은 차원 + 거리 내" 조건과 결합 가능
- DRY 수정(AdvanceToNextAction 추출), ChaserAgent.cs 제거, ChaserContext 별도 파일화
- (선택) 커스텀 휴리스틱으로 알고리즘 깊이 강화 — admissible 유지하며 위협/차원/예측 반영

---

## 5. 핵심 설계 원칙 (대화에서 확립)

- **행동이 다르면 상속(Action), 데이터만 다르면 값 채우기(Goal, Sound)** — 실행 코드 유무가 기준
- **엔진(범용) vs 사용자(게임전용) 분리** — Goap.cs는 fact/goal 이름을 모르는 범용, BuildWorldState/GoalIsRelevant는 이름을 아는 게임전용
- **클래스 수 ≠ 알고리즘 깊이** — 깊이는 계산의 정교함(소리 전파 모델, 커스텀 휴리스틱)에서 나옴
- **감지: 싼 계산부터**(거리→각도→차폐), 주기적 실행(매프레임 아님)
- **튜닝 값은 SerializeField로** 빼서 인스펙터 조절
- **상태 전환 감지(edge detection)** — 지속 상태가 아니라 변화 순간에 반응
- **지금 필요한 만큼만, 필요하면 확장**

---

## 6. 다음 작업 제안

가장 자연스러운 다음 단계는 **감지 메인 로직 완성**:
1. 거리 체크를 플레이어 직접 참조로 단순화 (targetLayer 제거)
2. 세 함수를 엮는 CheckVision() 작성 — `_wasVisible` 상태 전환 감지 포함
3. 주기적 실행(코루틴 또는 타이머)
4. ReportSighting/ReportLostSight 연결 → 첫 실제 테스트(추격자가 플레이어를 보고 CatchPlayer로 전환해 추격 시작)

이후 소리 시스템(SoundType 클래스/ScriptableObject) 구현.
