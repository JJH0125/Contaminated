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

## 4. 현재 상태 (2026-10-19 기준)

> 아래 2·3장은 초기 설계 기록이라 파일 위치와 일부 내용이 지금과 다르다(스크립트는 `Assets/_Game/Scripts/` 아래).
> 지금 상태는 이 장을 기준으로 본다.

### ✅ 완성
- 2D + 3D A* 길찾기, GOAP 엔진 + 세 목표 단일 추격자(`HorrorChaserAgent`)
- 시야 감지(`VisionCensor`): 같은 차원 → 거리(불빛 안이면 라운드에 따라 최대 2.5배) → 시야각 → 가려짐, 0.2초마다
- 소리 시스템: `Sound`(값 채우기) + `SoundLibrary`(ScriptableObject) + `SoundEmitter`
- 차원 이동: 어디서든 F 키(재사용 대기 12초, 이동 후 감지·잡기 유예 1초)
- 적: 차원별 3마리

### ✅ 2026-10-19에 한 작업
1. **블랙보드: 보고 있는 적을 집합으로** (`SquadBlackboard._seeingEnemies`, 10-12 작업)
   - `ReportSighting(적, 위치)`는 넣고, `ReportLostSight(적)`은 그 적만 뺀다
   - `PlayerCurrentlyVisible` = 집합이 비어 있지 않은가 (읽기 전용 속성)
2. **`ChaserContext`에 이름표 추가**: `Member`(`DimensionMember`). 쓰이지 않던 기본값과 `Agent`(SquadAgent) 필드는 삭제
3. **경계 단계를 적마다 갖게 함** (커밋 `94cc1ae`)
   - 새 컴포넌트 `ChaserAlert`(Goap/SingleChaser): `Level`(읽기 전용), 내려가는 시간 두 값, `Update`에서 한 단계씩 감소
   - 올리는 입구는 `RaiseAlert(level)` 하나: 높으면 올림, 같으면 타이머만 0, 낮으면 무시 (→ 추격 중 경계가 잠깐 내려가던 버그 해결)
   - `AlertLevel` 열거형은 `ChaserAlert.cs`에 클래스 밖으로 선언 (`SquadBlackboard.AlertLevel` 아님)
   - 시야: 본 적만 `RaiseAlert(Alerted)` / 소리: 반경 안에서 들은 적 **모두** `RaiseAlert(소리의 단계)`, 블랙보드 보고는 한 번
   - `ChaserContext.Alert`로 행동에 전달, 이동 속도는 `ctx.Alert.Level`로 결정 (`SearchSoundArea`는 이동이 없어 경계와 무관)
4. **다중 추격자 데모(`SquadAgent.cs`, `Actions.cs`)를 `#if false`로 컴파일에서 제외** (보관용)
5. **저장소 정리**: `CLAUDE.md`를 루트로 이동, `Assets/.claude` 삭제, `.gitignore`에 `/.claude/settings.local.json` 추가, 대화 기록 hook은 프로젝트 설정(`.claude/settings.json`) 하나로 통일

### 결정한 것
- **블랙보드 = 적들이 함께 아는 사실**(누가 보나, 마지막 위치, 소리 정보, 조사 완료 목록) / **`ChaserAlert` = 적 각자의 긴장 상태**
- 경계 관련 이름: 컴포넌트 `ChaserAlert`, 단계 속성 `Level`, 문서 필드 `ctx.Alert`, 입구 `RaiseAlert`
- `DimensionMember` 이름은 유지 (차원별 벽·지형 등 적이 아닌 것에도 쓸 예정)
- 두 PC 작업: 시작 시 pull → 끝낼 때 이 문서 갱신 + 커밋·푸시. 대화 기록(`문서\ClaudeLogs\*.md`)은 PC마다 따로 쌓이고 필요하면 직접 옮김

### ⚠️ 알려진 문제 / 확인 필요
- **Stage 프리팹의 적들에 `ChaserAlert`를 붙이고 저장해야 함** (안 붙이면 시야에서 null 에러)
- `SoundEmitter`의 `GetComponentInParent<ChaserAlert>()?.RaiseAlert` → `?.` 대신 `!= null` 확인 권장
- `VisionCensor`가 0.2초마다 `GetComponent<ChaserAlert>()` 호출 → `Start`에서 한 번 구해 두기 권장
- 소리 정보(`HasSound` 등)는 여전히 공유라, 소리를 듣지 못한 같은 차원 적도 (Calm 속도로) 조사하러 감
- 적 시야 레이캐스트가 지형을 장애물로 보지 않음 (봉우리 너머가 보임)
- 차원 이동 도착 소리가 유예 1초 동안에도 바로 적에게 전달됨 (유예를 2초로, 소리를 유예 끝에 내는 안 검토 중)

---

## 5. 핵심 설계 원칙 (대화에서 확립)

- **행동이 다르면 상속(Action), 데이터만 다르면 값 채우기(Goal, Sound)** — 실행 코드 유무가 기준
- **엔진(범용) vs 사용자(게임전용) 분리** — Goap.cs는 fact/goal 이름을 모르는 범용, BuildWorldState/GoalIsRelevant는 이름을 아는 게임전용
- **클래스 수 ≠ 알고리즘 깊이** — 깊이는 계산의 정교함(소리 전파 모델, 커스텀 휴리스틱)에서 나옴
- **감지: 싼 계산부터**(거리→각도→차폐), 주기적 실행(매프레임 아님)
- **튜닝 값은 SerializeField로** 빼서 인스펙터 조절
- **상태 전환 감지(edge detection)** — 지속 상태가 아니라 변화 순간에 반응
- **지금 필요한 만큼만, 필요하면 확장**
- **블랙보드는 공유 지식, 경계는 개인 상태** — 같은 정보를 알아도 직접 보거나 들은 적만 긴장한다
- **값을 바꾸는 입구는 하나로** — 경계는 `RaiseAlert`로만 올리고, 내부 변경용 `SetAlert`는 private

---

## 6. 다음 작업

### 1순위: 근처 적에게 발견 지점 알리기
지금은 한 적에게 들키면 같은 차원의 다른 적도 맵 어디에 있든 곧장 추격한다
(`HorrorChaserAgent.BuildWorldState`의 `playerVisible = bb.PlayerCurrentlyVisible && CanSeePlayerNow()`).
목표: **본 적만 추격**, 보지 못했지만 **같은 차원에서 발견한 적 근처에 있는 적은 발견 지점으로 이동해 수색**, 다른 차원은 영향 없음.

| 단계 | 파일 | 할 일 |
|---|---|---|
| 1 | `SquadBlackboard` | 묻는 함수 두 개: "이 적이 보고 있는가"(`Contains`), "이 적 근처(반경)에서 같은 차원의 **다른** 적이 보고 있는가" |
| 2 | `ChaserContext` | "제보받은 위치" 칸: 있는가(`bool`), 위치(`Vector3`). 블랙보드 `LastPlayerPosition`은 다른 차원 소리가 덮어쓸 수 있어 **복사**해 둔다 |
| 3 | `HorrorChaserAgent` | 반경 `[SerializeField]` 추가, `playerVisible`을 1번의 첫 함수로, 둘째 함수가 true면 2번 칸에 위치 복사, 새 fact와 `GoalIsRelevant` 조건 |
| 4 | `ChaserGoals` | 새 목표 (우선순위 100과 50 사이, 예: 75). `ChaserGoals` 주석의 "last-known position" 설명도 정리 |
| 5 | `ChaserActions` | `MoveToSound`/`SearchSoundArea`를 본뜬 행동 두 개, 끝나면 2번 칸을 비움. `Build()`에 등록 |

**정할 값**: "근처" 반경(시야 22m → 30~40m 추천), 제보받은 적의 경계 단계(Alerted 13m/s / Suspicious 9m/s)

### 그다음 후보
- **놓친 자리 수색**: 본 적이 플레이어를 놓친 순간에도 2번 칸에 마지막 위치를 적으면, 횃불 던지기·차원 이동으로 추격이 순식간에 끝나는 문제가 줄어듦
- 소리 기억을 적별로 나누기 (위 "알려진 문제" 참고)
- 차원별 벽 → 차원별 레이어, 오브젝트 보이기/숨기기, 차원별 길찾기 격자 (지형 분리는 그 뒤)
