# 공포 게임 졸업 프로젝트 — 진행 요약

> **답변 규칙: 영어로 답하지 않는다.** 진행 안내, 최종 답변, 표, 제목까지 사용자에게 보이는 모든 글은 한국어로 쓴다.
> 특히 파일을 여러 개 다룬 긴 작업을 마친 뒤의 마지막 답변이 영어로 나가는 실수가 반복됐으니, 보내기 전에 꼭 확인한다.

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

## 4. 현재 상태 (2026-10-23 기준)

> 위 2·3장은 초기 설계 기록이라 파일 위치와 일부 내용이 지금과 다르다. 지금 상태는 이 장을 기준으로 본다.

### 스크립트 폴더 (`Assets/_Game/Scripts/`)
| 폴더 | 내용 |
|---|---|
| `Core/` | `GameManager`, `StageLayout`(라운드마다 배치), **`GameTuning`**(수치 설정 에셋) |
| `Goap/Basic/` | `Goap`(엔진), `SquadBlackboard`(공유 지식), `ChaserContext`(행동에 넘기는 문서) |
| `Goap/SingleChaser/` | `HorrorChaserAgent`(두뇌), `ChaserGoals`, `ChaserActions`, `ChaserLocomotion`, `ChaserAlert`(경계), `ChaserAnimator`, `EnemyEyes`, `Jumpscare` |
| `Detection/` | `VisionCensor`(시야), `Sound`·`SoundLibrary`·`SoundEmitter`(소리) |
| `Dimension/` | `DimensionController`(차원 이동), `DimensionMember`(차원 이름표), Fake 차원 화면 효과들 |
| `Effects/` | 화면 가장자리 오염, CCTV 노이즈, 나무 투명화, 경계 벽 등 |
| `Objects/` | `Campfire`, `Torch`, `FireSource`, `Stone`(미끼 돌), `Exit`, `Generator` |
| `Player/` | 이동, 카메라(`Follow`), 횃불·돌 들기, 숨소리, 발소리, 보호복 오염 |
| `Audio/`, `UI/`, `Debugging/`, `Editor/`, `Astar/` | 이름 그대로 |

- 다중 추격자 데모(`SquadAgent.cs`, `Actions.cs`)는 **삭제됨** (Git 기록에는 남아 있음)
- **수치는 `Resources/GameTuning` 에셋 한 곳에서 조절한다** (소리 값만 `SoundLibrary`). 적은 차원별(Real/Fake)로 값을 따로 둔다

### ✅ 적 AI (단일 추격자를 여러 마리로)
- **경계는 적마다** (`ChaserAlert`): `Level`(읽기 전용), 입구는 `RaiseAlert(level)` 하나(높으면 올림 / 같으면 타이머만 0 / 낮으면 무시), 시간이 지나면 한 단계씩 내려감
  - 시야: 본 적만 `RaiseAlert(Alerted)` / 소리: 반경 안에서 들은 적 모두 `RaiseAlert(소리 단계)` / 신호에 합류한 적: Suspicious 유지
  - 이동 속도는 `ctx.Alert.Level`로 결정 (Calm 5 / Suspicious 9 / Alerted 13 m/s)
- **직접 본 적만 추격**: `playerVisible = bb.IsSeeingPlayerNow(enemy)` (블랙보드의 `_seeingEnemies` 집합)
- **발견 신호(포효)**: 플레이어를 새로 발견하면(`ReportSighting`이 true, 사건 번호 `spotCount` 증가) `HorrorChaserAgent.Roar()`가 `roarRadius`(40m) 안의 **같은 차원** 적에게 `ReceiveSignal`을 보낸다 → 받은 적은 `ctx.Joined = true`
  - 목표 `InvestigateSignal`(우선순위 75): `MoveToSignal` → `SearchSignalArea`
  - 아무도 `joinTimeout`(30초) 동안 플레이어를 못 보면 합류를 풀고 돌아간다
- **발견 지점 수색 지도** (블랙보드): 사건마다 발견 지점 주변에 수색 지점들을 만든다(`EnsureSearchPlan`, 도망 방향 고려). 합류한 적은 아무도 맡지 않은 가장 가까운 지점을 맡고(`ClaimSearchPoint`) 확인하며, 지나가며 본 지점도 확인 처리된다(`MarkSearchPointsSeen`)
- **추격 겹침**: 여러 적이 함께 볼 때 `ChaseRank`로 순위를 매긴다. 0위(가장 가까운 적)는 곧장 쫓고, 나머지는 `interceptLeadTime`(1.5초) 뒤 플레이어가 있을 자리로 좌우 `interceptSpacing`(6m)씩 벌려 앞질러 간다. 플레이어가 `interceptDirectRadius`(10m) 안이면 모두 곧장 달려든다
- 목표 우선순위: `CatchPlayer`(100) > `InvestigateSignal`(75) > `InvestigateSound`(50) > `Wander`(1)

### ✅ 차원
- 어디서든 F 키로 이동. **재사용 대기 15초, Fake 체류 최대 8초**(지나면 Real로 튕겨 나옴), 도착 소리, 이동 직후 감지·잡기 유예
- Fake에 들어가면 플레이어 자리에서 **오염의 물결**이 퍼져 땅·나무·바위에 얼룩이 드러남 (`FakeContaminationEffect` + 직접 만든 후처리 셰이더)

### ✅ 그 밖의 최근 변경
- 미끼 돌(`Stone`, `StoneHolder`): 줍고 던져서 소리로 적을 유인. 줍기·던지기 애니메이션은 제거
- 횃불 메커니즘 변경, 숨기기와 온기 게이지
- 저장소: `CLAUDE.md`는 루트, 대화 기록 hook은 프로젝트 설정(`.claude/settings.json`) 하나, PC 전용 설정(`.claude/settings.local.json`)은 Git 제외

### 결정한 것
- **블랙보드 = 적들이 함께 아는 사실**(누가 보나, 마지막 위치·방향·속도, 수색 지도, 소리 정보) / **`ChaserAlert` = 적 각자의 긴장 상태** / **`ChaserContext` = 적 각자의 할 일 상태**(합류 여부, 추격 순위 등)
- 경계 관련 이름: 컴포넌트 `ChaserAlert`, 속성 `Level`, 문서 필드 `ctx.Alert`, 입구 `RaiseAlert`
- `DimensionMember` 이름은 유지 (차원별 벽·지형 등 적이 아닌 것에도 쓸 예정)
- 두 PC 작업: 시작 시 pull → 끝낼 때 이 문서 갱신 + 커밋·푸시. 대화 기록(`문서\ClaudeLogs\*.md`)은 PC마다 따로 쌓이고 필요하면 직접 옮김

### ⚠️ 알려진 문제 / 확인 필요
- `SoundEmitter`의 `GetComponentInParent<ChaserAlert>()?.RaiseAlert` → Unity 컴포넌트에는 `?.` 대신 `!= null` 확인 권장
- `VisionCensor`가 시야 검사 때마다 `GetComponent<ChaserAlert>()` 호출 → `Start`에서 한 번 구해 두기 권장
- 소리 정보(`HasSound` 등)는 여전히 블랙보드에 하나라, 소리를 **듣지 못한** 같은 차원 적도 (느린 속도로) 조사하러 감
- 적 시야 레이캐스트가 지형을 장애물로 보는지 확인 필요 (`obstacleLayer`에 지형 레이어가 없으면 봉우리 너머가 보임)
- 차원 이동 도착 소리가 감지 유예 중에도 바로 적에게 전달됨
- `Generator.cs`가 아직 Stage 프리팹에서 쓰이고 있음 (발전기를 없앤 설계라면 정리 대상인지 확인)
- 블랙보드의 `TryClaimRole`/`ReleaseRole`은 다중 추격자 데모 삭제 후 쓰는 곳이 없음
- 2장(코드 구조)·`HorrorChaserAgent`/`ChaserGoals`의 영어 주석은 "세 목표" 시절 설명 그대로

---

## 5. 핵심 설계 원칙 (대화에서 확립)

- **행동이 다르면 상속(Action), 데이터만 다르면 값 채우기(Goal, Sound)** — 실행 코드 유무가 기준
- **엔진(범용) vs 사용자(게임전용) 분리** — Goap.cs는 fact/goal 이름을 모르는 범용, BuildWorldState/GoalIsRelevant는 이름을 아는 게임전용
- **클래스 수 ≠ 알고리즘 깊이** — 깊이는 계산의 정교함(소리 전파 모델, 커스텀 휴리스틱)에서 나옴
- **감지: 싼 계산부터**(거리→각도→차폐), 주기적 실행(매프레임 아님)
- **튜닝 값은 `GameTuning` 에셋 한 곳에** (예전: 각 컴포넌트의 SerializeField). 연결·문구·레이어·키 같은 것만 컴포넌트 인스펙터에 남긴다
- **상태 전환 감지(edge detection)** — 지속 상태가 아니라 변화 순간에 반응
- **지금 필요한 만큼만, 필요하면 확장**
- **블랙보드는 공유 지식, 경계는 개인 상태** — 같은 정보를 알아도 직접 보거나 들은 적만 긴장한다
- **값을 바꾸는 입구는 하나로** — 경계는 `RaiseAlert`로만 올리고, 내부 변경용 `SetAlert`는 private

---

## 6. 다음 작업 후보

다중 추격자 작업(발견 신호, 수색 지도, 추격 겹침)은 끝났다. 다음은 아래에서 고른다.

1. **실제 플레이로 AI 튜닝**: 포효 반경(40m), 합류 해제 시간(30초), 앞질러 가기 값, 경계 감쇠 시간을 `GameTuning`에서 조절하며 난이도 확인
2. **소리 기억을 적별로 나누기**: `SoundEmitter`의 반복문은 이미 누가 들었는지 알고 있으므로, 들은 적에게만 소리 위치를 전달 (위 "알려진 문제" 해결)
3. **놓친 자리 수색**: 직접 보던 적이 플레이어를 놓쳤을 때도 수색 지도를 쓰게 하면, 횃불·돌·차원 이동으로 추격이 순식간에 끝나는 문제가 줄어듦
4. **차원 이동 도착 소리 조정**: 유예를 2초로 늘리고, 소리를 유예가 끝날 때 내는 안
5. **차원별 벽**: 차원별 레이어·충돌, 오브젝트 보이기/숨기기, 차원별 길찾기 격자 (지형 분리는 그 뒤)
6. **문서·코드 정리**: 2장 갱신, 오래된 영어 주석, `TryClaimRole`/`ReleaseRole`, `Generator` 정리 여부
