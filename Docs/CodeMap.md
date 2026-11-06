# 코드 지도

> 기준: 2026-11-03 코드. 경로는 `Assets/` 기준이다. 이름은 모두 실제 코드에 있는 것이다.
> 이름공간: 길찾기는 `Astar3D`, 나머지는 거의 모두 `Squad`. `Dimension` 열거형, `Player`, `Follow`는 이름공간 밖에 있다.

---

## 1. 기능별 표

### GOAP 플래너 (범용 엔진)
게임의 fact·목표 이름을 모르는 부분이다.

| 기능 | 파일 경로 | 클래스 | 핵심 메서드 | 한 줄 설명 |
|---|---|---|---|---|
| 세계 상태 | `_Game/Scripts/Goap/Goap.cs` | `WorldState` | `Matches`, `Apply`, `DistanceTo`, `Clone` | bool 사실 묶음. 목표 비교, 효과 덮어쓰기, 휴리스틱(안 맞는 사실 수) |
| 행동의 틀 | `_Game/Scripts/Goap/Goap.cs` | `GoapAction` | `Perform`, `GetCost`, `CheckProceduralPrecondition` | 조건·효과·비용·실행을 가진 추상 클래스. 행동마다 상속한다 |
| 목표의 틀 | `_Game/Scripts/Goap/Goap.cs` | `Goal` | (없음) | 이름, 우선순위, 원하는 상태만 담은 데이터 |
| 플래너 | `_Game/Scripts/Goap/Goap.cs` | `GoapPlanner` (+ 내부 `PlanNode`) | `Plan`, `BuildPlan` | 상태공간 A\*. 열린 목록은 `List.Sort`, 닫힌 목록 없음. `LastExpanded`로 살펴본 노드 수를 남긴다 |

### 적의 두뇌 (게임 전용)
| 기능 | 파일 경로 | 클래스 | 핵심 메서드 | 한 줄 설명 |
|---|---|---|---|---|
| 두뇌 | `_Game/Scripts/Goap/Enemies/Brain/HorrorChaserAgent.cs` | `HorrorChaserAgent` | `Update`, `Replan`, `ReplanNow`, `BuildWorldState`, `RankGoals`, `GoalIsRelevant`, `RefreshModules`, `Roar`, `ReceiveSignal`, `UpdateJoined` | 0.4초마다 사실을 모아 계획하고, 계획의 행동을 한 프레임씩 실행한다 |
| 할 일 상태 | `_Game/Scripts/Goap/Enemies/Brain/ChaserContext.cs` | `ChaserContext` | `HearSound`, `ClearSound` | 행동에 넘기는 적 각자의 상태. 합류 여부(`Joined`), 추격 순위(`ChaseRank`), 들은 소리(`HasSound`, `SoundPosition`, `SoundTime`), 이동·경계·무리 연결 |
| 무리 | `_Game/Scripts/Goap/Enemies/Brain/ChaserPack.cs` | `ChaserPack` | `TryChooseSpot`, `Add`, `CountLeg` | Real 적이 함께 쓰는 배회 영역. 서로 떨어진 자리를 고르고 중심이 떠돈다 |

### 목표
| 목표 (우선순위) | 파일 경로 | 클래스 | 핵심 메서드 | 한 줄 설명 |
|---|---|---|---|---|
| 기본 목표 4개 | `_Game/Scripts/Goap/Enemies/Brain/ChaserGoals.cs` | `ChaserGoals` | `Build` | `CatchPlayer`(100, `playerCaught`), `InvestigateSignal`(75, `signalInvestigated`), `InvestigateSound`(50, `soundInvestigated`), `Wander`(1, `wandering`) |
| 모닥불 끄기 (60, 부품) | `_Game/Scripts/Goap/Enemies/Brain/ExtinguishFireGoal.cs` | `ExtinguishFireGoal` | `Goal`, `Actions`, `WriteFacts`, `IsRelevant`, `NearestLitFire` | 켜진 불이 있으면 끄러 간다. `fireExtinguished`가 원하는 사실 |
| 목표 부품의 틀 | `_Game/Scripts/Goap/Enemies/Brain/GoapModule.cs` | `GoapModule` | `Goal`, `Actions`, `WriteFacts`, `IsRelevant`, `Notify` | 목표와 행동을 담은 컴포넌트. 켜고 끄면 두뇌가 바로 다시 계획한다 |

### 행동
| 행동 | 파일 경로 | 클래스 | 조건 → 효과 | 한 줄 설명 |
|---|---|---|---|---|
| 행동 목록 | `_Game/Scripts/Goap/Enemies/Brain/ChaserActions.cs` | `ChaserActions` | `Build` | 기본 행동 6개를 만든다 |
| 잡기 | 〃 | `ReachPlayer` | `playerVisible` → `playerCaught` | `ChooseTarget`으로 곧장 쫓거나 앞질러 가고, 잡는 거리에서 잡기 연출을 튼다 |
| 신호 지점으로 | 〃 | `MoveToSignal` | `joinedSignal` → `atSignalLocation` | 발견 지점으로 가서 `EnsureSearchPlan`으로 수색 지도를 만든다 |
| 신호 지점 수색 | 〃 | `SearchSignalArea` | `atSignalLocation` → `signalInvestigated` | 수색 지점을 하나씩 맡아 가서 둘러본다. 남은 지점이 없으면 합류를 푼다 |
| 소리 쪽으로 | 〃 | `MoveToSound` | `heardSound` → `atSoundLocation` | 자기가 들은 소리 위치로 간다. 길이 없으면 자기 기록을 지운다 |
| 소리 자리 둘러보기 | 〃 | `SearchSoundArea` | `atSoundLocation` → `soundInvestigated` | `soundSearchTime`(3초) 둘러본 뒤 자기 기록만 지운다. 둘러보는 중에 새 소리를 들으면 처음부터 |
| 배회 | 〃 | `WanderStep` | (없음) → `wandering` | `ChooseWanderTarget`으로 무리 영역이나 켜진 불 둘레를 고르고, 도착하면 잠시 쉰다 |
| 불 곁으로 | `_Game/Scripts/Goap/Enemies/Brain/ExtinguishFireGoal.cs` | `MoveToFire` (내부 클래스) | `litFireKnown` → `atFire` | 가장 가까운 켜진 불로 간다 |
| 불 끄기 | 〃 | `Extinguish` (내부 클래스) | `atFire` → `fireExtinguished` | `Campfire.AddExtinguish`를 매 프레임 불러 끈다. 진행도는 불에 남는다 |

### 경계, 공유 지식
| 기능 | 파일 경로 | 클래스 | 핵심 메서드 | 한 줄 설명 |
|---|---|---|---|---|
| ChaserAlert | `_Game/Scripts/Goap/Enemies/Brain/ChaserAlert.cs` | `ChaserAlert`, `AlertLevel` | `RaiseAlert`, `SetAlert`(private), `Update`, `Level` | 적 각자의 경계. 올리는 입구는 `RaiseAlert` 하나이고, 시간이 지나면 한 단계씩 내려간다 |
| SquadBlackboard | `_Game/Scripts/Goap/Enemies/Brain/SquadBlackboard.cs` | `SquadBlackboard` (+ `SearchPoint`, `SearchCandidate`) | `ReportSighting`, `ReportLostSight`, `IsSeeingPlayerNow`, `ChaseRank`, `EnsureSearchPlan`, `ClaimSearchPoint`, `CompleteSearchPoint`, `ReleaseSearchPoints`, `MarkSearchPointsSeen` | 적들이 함께 아는 사실. 누가 보고 있나, 마지막 위치·방향, 발견 사건 번호(`spotCount`), 수색 지도. 소리는 없다(들은 적의 `ChaserContext`에) |

### 길찾기 (두 겹 격자)
| 기능 | 파일 경로 | 클래스 | 핵심 메서드 | 한 줄 설명 |
|---|---|---|---|---|
| 칸 | `_Game/Scripts/Astar/Node.cs` | `Node` | `IsWalkableIn` | 칸 하나에 `RealBlocks`, `FakeBlocks`를 따로 센다. 이것이 두 겹이다 |
| 열린 목록 | `_Game/Scripts/Astar/NodeHeap.cs` | `NodeHeap` | (힙 연산) | 길찾기용 최소 힙 |
| PathGrid | `_Game/Scripts/Astar/PathGrid.cs` | `PathGrid` | `BuildGrid`, `NodeFromWorldPoint`, `ClosestWalkableNode`, `GetNeighbors`, `IsWalkable`, `NodesInside`, `NodesWhere`, `AddBlock`, `EscapeBlocked`, `Version` | 격자를 만들고, 장애물이 생기거나 사라지면 차원별 막힘 수를 더하고 빼며 `Version`을 올린다 |
| 길찾기 | `_Game/Scripts/Astar/Pathfinder.cs` | `Pathfinder` | `FindPath`, `Heuristic`, `Distance`, `RetracePath` | 격자 A\*. 차원을 받아 그 차원 기준으로 지날 수 있는 칸만 쓴다. 휴리스틱은 octile |
| 차원별 장애물 | `_Game/Scripts/Dimension/DimensionObstacle.cs` | `DimensionObstacle` | `Setup`, `Blocks`, `Open`, `ApplyCollisions` | 한 차원만 막는 장벽. 격자 칸을 막고, 플레이어 몸의 충돌을 차원에 맞게 켜고 끈다 |
| 적의 이동 | `_Game/Scripts/Goap/Enemies/Body/ChaserLocomotion.cs` | `ChaserLocomotion` | `MoveTo`, `FollowPath`, `LookAround`, `Warp`, `GetWanderTarget`, `SetWanderTarget`, `ClearWanderTarget`, `IsWalkable`, `HasValidPath` | 목표가 바뀌거나 `PathGrid.Version`이 바뀌면 경로를 다시 찾고, 경계에 맞는 속도로 따라간다 |

### 시야와 청각
| 기능 | 파일 경로 | 클래스 | 핵심 메서드 | 한 줄 설명 |
|---|---|---|---|---|
| 시야 | `_Game/Scripts/Detection/VisionSensor.cs` | `VisionSensor` | `DetectVision`, `IsInSameDimension`, `IsInViewDistance`, `ViewDistanceFor`, `IsInViewAngle`, `HasLineOfSight` | 0.2초마다 싼 검사부터(차원 → 거리 → 각도 → 레이캐스트). 결과를 블랙보드와 경계에 알린다 |
| 소리 값 | `_Game/Scripts/Detection/Sound.cs` | `Sound`, `SoundList` | (생성자) | 소리 하나의 반경·경계 단계·차원 관통 여부. `SoundList`에 게임의 소리들이 있다 |
| 소리 값 에셋 | `_Game/Scripts/Detection/SoundLibrary.cs` | `SoundLibrary` | (ScriptableObject) | 소리 값을 조절하는 에셋 |
| 소리 전달 | `_Game/Scripts/Detection/SoundEmitter.cs` | `SoundEmitter` | `Emit`, `CanHear` | 반경 안의 적마다 청각 배율과 차원으로 거른다. 들은 적의 경계를 올리고 `HorrorChaserAgent.HearSound`로 그 적에게만 소리 위치를 적는다 |

### 차원 이동
| 기능 | 파일 경로 | 클래스 | 핵심 메서드 | 한 줄 설명 |
|---|---|---|---|---|
| 차원 이동 | `_Game/Scripts/Dimension/DimensionController.cs` | `DimensionController`, `Dimension` | `Update`, `ShiftPlayerDimension`, `SwitchPlayerDimension`, `MoveOutOfObstacle`, `CanDetectPlayer`, `CompareDimension`, `ShiftTo`, `AddMember` | F 키, 재사용 대기, Fake 체류 제한, 감지 유예, 도착 소리 |
| 차원 이름표 | `_Game/Scripts/Dimension/DimensionMember.cs` | `DimensionMember` | `Setup`, `SetRenderer` | 이 오브젝트가 어느 차원에 있는가. 플레이어와 다른 차원이면 숨긴다 |
| 플레이어의 차원 | `_Game/Scripts/Player/Player.cs` | `Player` | `SwitchMyDimension`, `myDimension` | 플레이어가 지금 있는 차원 |

### 모닥불
| 기능 | 파일 경로 | 클래스 | 핵심 메서드 | 한 줄 설명 |
|---|---|---|---|---|
| 모닥불 | `_Game/Scripts/Objects/Campfire.cs` | `Campfire` | `Light`, `BurnInReal`, `AddExtinguish`, `Extinguish`, `LinkGate`, `ConnectRoot`, `IsLit` | 켜면 덮개·관문을 열고 뿌리를 태우며, Fake 쪽(뿌리, 재의 적이 듣는 소리)은 켜는 순간, Real 쪽(덮개·관문 덤불, 현실의 적이 듣는 소리)은 플레이어가 Real로 돌아온 순간 처리하고 출구 조건을 확인한다 |
| 불 켜기 조작 | `_Game/Scripts/Player/CampfireLighter.cs` | `CampfireLighter` | `Update`, `Nearest`, `Release` | Fake에서 횃불을 들고 E를 1.5초 누르면 `Campfire.Light` |
| 불빛 | `_Game/Scripts/Objects/FireSource.cs` | `FireSource` | `IsLit`(static), `PlaceLight` | 조명을 만들고, "이 자리가 불빛에 드러났나"를 시야에 알려 준다 |

### 횃불
| 기능 | 파일 경로 | 클래스 | 핵심 메서드 | 한 줄 설명 |
|---|---|---|---|---|
| 횃불 들기·숨기기 | `_Game/Scripts/Player/TorchHolder.cs` | `TorchHolder` | `Update`, `StartHide`, `StartDraw`, `SetHidden`, `PutOut`, `IsHiding`(static) | Space로 숨기고, 숨긴 동안 온기 게이지가 준다. Fake 적은 숨긴 횃불을 못 본다 |
| 횃불 표식 | `_Game/Scripts/Objects/Torch.cs` | `Torch` | (없음) | 손에 든 횃불 프리팹의 표식. 불빛은 같은 오브젝트의 `FireSource`가 맡는다 |

### GameTuning
| 기능 | 파일 경로 | 클래스 | 핵심 메서드 | 한 줄 설명 |
|---|---|---|---|---|
| 수치 에셋 | `_Game/Scripts/Core/GameTuning.cs` | `GameTuning` | `Instance`, `EnemyFor` | `Resources/GameTuning` 한 곳에서 수치를 읽는다. 적 값은 `realEnemy`, `fakeEnemy`로 나뉜다 |
| 항목별 묶음 | 〃 | `PlayerTuning`, `TorchTuning`, `StoneTuning`, `BreathingTuning`, `SuitTuning`, `CameraTuning`, `DimensionTuning`, `EnemyTuning`, `EnemyBodyTuning`, `SearchTuning`, `StageTuning`, `RoundSetup`, `MapTuning`, `FakeScreenTuning`, `AshEnemyTuning`, `FakeContaminationTuning`, `ContaminationEdgeTuning`, `CctvTuning`, `FoliageTuning`, `BoundaryTuning`, `AudioTuning` | `StageTuning.Round`, `CameraTuning.Distance` | 기능별 수치. 시연 씬은 `StageTuning.RoundOverride`, `SeedOverride`, `CameraTuning.DistanceOverride`로 에셋을 바꾸지 않고 덮어쓴다 |

---

## 2. 호출 흐름

### ① 적이 플레이어를 발견했을 때
**감지**
`VisionSensor.Update` → `VisionSensor.DetectVision` → `VisionSensor.IsInSameDimension` → `DimensionController.CanDetectPlayer` → `VisionSensor.IsInViewDistance` → `VisionSensor.ViewDistanceFor` → `FireSource.IsLit` → `VisionSensor.IsInViewAngle` → `VisionSensor.HasLineOfSight`

**보인 순간**
→ `AshEnemyBody.OnSpotted` (재의 적) 또는 `VisionSensor.PlaySpottedSound` (Real 적)
→ `SquadBlackboard.ReportSighting`
→ `ChaserAlert.RaiseAlert` (Alerted)
→ (아무도 보지 않던 중 처음 봤으면) `HorrorChaserAgent.Roar` (② 흐름으로)

**판단과 추격** (다음 계획 때, 최대 0.4초 뒤)
`HorrorChaserAgent.Update` → `HorrorChaserAgent.Replan` → `HorrorChaserAgent.BuildWorldState` → `SquadBlackboard.IsSeeingPlayerNow` → `HorrorChaserAgent.RankGoals` → `HorrorChaserAgent.GoalIsRelevant` → `GoapPlanner.Plan` (`CatchPlayer` → `ReachPlayer`)

**실행**
→ `ReachPlayer.Perform` → `ReachPlayer.ChooseTarget` → `SquadBlackboard.ChaseRank` → `ChaserLocomotion.MoveTo` → `Pathfinder.FindPath` → `ChaserLocomotion.FollowPath`

**잡는 거리에 오면**
→ `DimensionController.CanDetectPlayer` (한 번 더 확인) → `AshJumpscare.Play` 또는 `Jumpscare.Play` 또는 `GameManager.GameOver`

### ② 포효를 들은 적이 모일 때
**신호 받기**
`HorrorChaserAgent.Roar` (같은 차원, `roarRadius` 안, 지금 플레이어를 보고 있지 않은 적에게) → `HorrorChaserAgent.ReceiveSignal` (`ChaserContext.Joined` = true) → `ChaserAlert.RaiseAlert` (Suspicious) → `HorrorChaserAgent.ReplanNow` → `HorrorChaserAgent.Replan` → `HorrorChaserAgent.BuildWorldState` (`joinedSignal`) → `GoapPlanner.Plan` (`InvestigateSignal` → `MoveToSignal` → `SearchSignalArea`)

**발견 지점으로 이동**
→ `MoveToSignal.Perform` → `ChaserLocomotion.MoveTo` (`SquadBlackboard.LastSightingPosition`) → 도착하면 `SquadBlackboard.EnsureSearchPlan` → `SearchSignalArea.Perform` (③ 흐름의 수색으로)

**합류한 동안 매 프레임**
`HorrorChaserAgent.Update` → `HorrorChaserAgent.UpdateJoined` → `ChaserAlert.RaiseAlert` (Suspicious 유지). 같은 차원에서 새 사건이 생기면 `HorrorChaserAgent.ReplanNow`

### ③ 플레이어를 놓친 뒤 수색할 때
**놓침 보고**
`VisionSensor.DetectVision` (보이다가 안 보임) → `SquadBlackboard.ReportLostSight`

**직접 보던 적**
→ `HorrorChaserAgent.Replan` → `HorrorChaserAgent.BuildWorldState` (`playerVisible` = false) → `HorrorChaserAgent.GoalIsRelevant` (`CatchPlayer` 빠짐) → `GoapPlanner.Plan`
- 자기가 들은 소리가 있으면 `InvestigateSound` (`MoveToSound` → `SearchSoundArea`), 없으면 `Wander` (`WanderStep`)
- **직접 보던 적은 수색 지도를 쓰지 않는다.** (CLAUDE.md "다음 작업 후보" 3번)

**합류한 적의 수색**
`SearchSignalArea.Perform` → `SquadBlackboard.ClaimSearchPoint` → `SquadBlackboard.MarkSearchPointsSeen` → `ChaserLocomotion.MoveTo` → `ChaserLocomotion.LookAround` → `SquadBlackboard.CompleteSearchPoint` → (다음 지점) `SquadBlackboard.ClaimSearchPoint` …

**수색 끝**
- 남은 지점이 없으면: `SquadBlackboard.ClaimSearchPoint`가 null을 돌려줌 → `SquadBlackboard.ReleaseSearchPoints` (`ChaserContext.Joined` = false)
- 오래 못 보면: `HorrorChaserAgent.UpdateJoined` (`joinTimeout` 지남) → `SquadBlackboard.ReleaseSearchPoints` → `HorrorChaserAgent.ReplanNow`

**긴장 풀림** (모든 적)
`ChaserAlert.Update` → `ChaserAlert.SetAlert` (Alerted → Suspicious → Calm)

### ④ 플레이어가 차원을 바꿨을 때 적의 길찾기
**플레이어 쪽**
`DimensionController.Update` (F 키, 또는 Fake 체류 시간이 다 됨) → `DimensionController.ShiftPlayerDimension` → `DimensionController.SwitchPlayerDimension` → `Player.SwitchMyDimension` → `DimensionMember.SetRenderer` → `DimensionObstacle.ApplyCollisions` → `DimensionController.MoveOutOfObstacle` → `PathGrid.EscapeBlocked` → `SoundEmitter.Emit` (`SoundList.DimensionShift`) → `SoundEmitter.CanHear` → `ChaserAlert.RaiseAlert` → `HorrorChaserAgent.HearSound` (`ChaserContext.HearSound`) → `HorrorChaserAgent.ReplanNow`

**적 쪽**
`VisionSensor.DetectVision` → `DimensionController.CanDetectPlayer` (false) → `SquadBlackboard.ReportLostSight` → `HorrorChaserAgent.Replan` → `ChaserLocomotion.MoveTo` → `Pathfinder.FindPath` (적 자신의 차원) → `PathGrid.ClosestWalkableNode` → `PathGrid.GetNeighbors` → `Node.IsWalkableIn`

- 적의 격자 기준은 **적 자신의 차원**이다. 플레이어가 차원을 바꿔도 적이 지날 수 있는 칸은 그대로이고, 바뀌는 것은 목적지(놓쳤으니 다른 목표)뿐이다.

**격자 자체가 바뀌는 경우** (장벽이 열릴 때)
`Campfire.Light` → `DimensionObstacle.Open` → `PathGrid.AddBlock` (`PathGrid.Version` 증가) → 다음 `ChaserLocomotion.MoveTo`에서 버전이 다르면 → `Pathfinder.FindPath`

### ⑤ 새 목표를 추가할 때 고쳐야 하는 곳
**방법 A — 목표 부품 (권장, 적 코드는 고치지 않음)**
1. `GoapModule`을 상속한 클래스를 만든다 (예: `ExtinguishFireGoal`)
   - `Goal`: 이름, 우선순위, `Desired`
   - `Actions`: `GoapAction`을 상속한 행동들 (`Preconditions`, `Effects`, `Perform`)
   - `WriteFacts`: 계획에 필요한 사실을 적는다
   - `IsRelevant`: 고를 이유가 있는지 판단한다
2. 수치는 `GameTuning`에 둔다 (예: `AshEnemyTuning`의 `extinguishPriority`)
3. 적에게 붙인다: 인스펙터에서 붙이거나, `StageLayout`에서 `AddComponent` (`ExtinguishFireGoal`이 그렇게 붙는다)

실행 흐름: `GoapModule.OnEnable` → `GoapModule.Notify` → `HorrorChaserAgent.RefreshModules` → `HorrorChaserAgent.ReplanNow` → `HorrorChaserAgent.Replan` (부품의 `Goal`, `Actions`를 합침) → `HorrorChaserAgent.BuildWorldState` → `GoapModule.WriteFacts` → `HorrorChaserAgent.GoalIsRelevant` → `GoapModule.IsRelevant` → `GoapPlanner.Plan`

**방법 B — 기본 목표로 넣기**
1. `ChaserGoals`: 우선순위 상수와 `Build`에 목표 추가
2. `ChaserActions`: 행동 클래스 추가, `Build`에 등록
3. `HorrorChaserAgent.BuildWorldState`: 새 사실 추가
4. `HorrorChaserAgent.GoalIsRelevant`: 새 목표의 `case` 추가

**두 방법 공통**
- `AlertIcon`은 목표 이름으로 기호(?, !)를 정한다. 조사나 추격 성격의 목표라면 그 `switch`에도 이름을 넣는다
- `GoapPlanDisplay`(F4)는 따로 고치지 않아도 새 목표와 계획을 보여 준다

---

## 3. 정리 필요 목록

### 이름과 하는 일이 안 맞는 것
| 대상 | 문제 |
|---|---|
| `_Game/Scripts/Astar/` 폴더 | 안의 이름공간은 `Astar3D`이다. 예전 2D `Astar`는 지워졌으니 폴더 이름만 남은 것이다 |
| `Squad` 이름공간 | 예전 협력 데모 때 이름이다. 지금은 감지, 차원, 물체, UI까지 거의 모든 코드가 여기에 있다 |
| `_Game/Scripts/Goap/` 폴더 | 판단(`Brain/`)뿐 아니라 이동, 애니메이션, 잡기 연출, 재의 적 겉모습(`Body/`)까지 들어 있다. "GOAP"보다 "적" 폴더에 가깝다 |
| `_Game/Scripts/Debugging/AlertIcon.cs`, `SoundRingDisplay.cs` | 디버그 폴더에 있지만, `Game` 씬과 `Stage` 프리팹에 붙어 있고 키로 켜고 끄지도 않는다. 플레이어가 보는 게임 화면 요소다 |
| `SoundEmitter.Emit` → `SoundRingDisplay`, `RoundAtmosphere` → `CleanViewDebug.On` | 게임 코드가 `Debugging` 폴더의 클래스를 직접 부른다. 디버그 도구를 빼면 게임 코드가 깨진다 |
| `Detection/Sound.cs` | `Sound`와 함께 `SoundList`도 들어 있다 |
| `Dimension/Effects/`와 `Effects/` | 화면 효과가 두 폴더에 나뉘어 있다. Fake 오염은 `Dimension/Effects`, 가장자리 오염과 CCTV 노이즈는 `Effects` |
| `Dimension` 열거형 | `DimensionController.cs` 안, 이름공간 밖에 있다 |
| `Campfire.Light` | 소리를 들을 레이어를 `(1 << 8) \| (1 << 9)`로 직접 적었다. 다른 곳(`DimensionController`)은 `enemyLayer`를 인스펙터로 받는다 |

### 지금 안 쓰는 코드
씬, 프리팹, 다른 코드에서 쓰이지 않는 **파일**은 없다. 아래는 쓰이지 않는 **메서드**와 확인이 필요한 것이다.

| 대상 | 상태 |
|---|---|
| `HorrorChaserAgent.CanSeePlayerNow` | 부르는 곳이 없다. 시야는 `SquadBlackboard.IsSeeingPlayerNow`로 바뀌었다 |
| `FireSource.ResetLight` | 부르는 곳이 없다 |
| `_Game/Scenes/Test.unity` | Build Settings에 없다. 지금도 쓰는지 확인이 필요하다 |
| CLAUDE.md 2장 | 지워진 `ChaserAgent`, `SquadAgent`, `Actions.cs`, `TryClaimRole`을 아직 설명한다 (4장이 현재 기준) |

---

## 4. 디버그·시연 도구

| 파일 경로 | 한 줄 설명 |
|---|---|
| `_Game/Scripts/Debugging/GoapPlanDisplay.cs` | F4: 적 위에 목표·계획·계획 실패·경계·계획 시간, 땅에 수색 후보 점수. F5: 가까운 불 켜기 |
| `_Game/Scripts/Debugging/PathGridDisplay.cs` | F6: 두 겹 격자(Real만 막힘 파랑, Fake만 막힘 자홍)와 차원별 경로 |
| `_Game/Scripts/Debugging/DemoControls.cs` | F7 0.5배속, F8 멈춤, F9 다음 라운드, F11 계획 로그 (스스로 만들어짐) |
| `_Game/Scripts/Debugging/DemoScenario.cs` | 시연 씬마다 라운드 구성·시드·카메라 거리를 덮어쓰고, 안내 문구와 1·2·3 키를 담당 |
| `_Game/Scripts/Debugging/AshStudyDebug.cs` | F1: 시간 제한 없이 Fake에 머물고 재의 적 3마리를 둘레에 놓는 관찰 모드 |
| `_Game/Scripts/Debugging/CleanViewDebug.cs` | F2: 안개·화면 효과를 끈 깨끗한 화면 |
| `_Game/Scripts/Player/Camera/FirstPersonView.cs` | F3: 1인칭 켜기 (게임에서는 꺼 둠, `GameTuning`에서 켤 수 있음) |
| `_Game/Scripts/Editor/DemoSceneBuilder.cs` | 메뉴 Tools > 시연 씬 만들기: Game 씬을 복사해 시연 씬 7개 생성 |
| `_Game/Scripts/Editor/ForestMapTool.cs` | 메뉴 Tools > Forest Map Tool: 지형, 경계 벽, 나무와 풀 만들기 |
| `_Game/Scripts/Editor/PlantColliderTool.cs` | 메뉴 Tools > Plant Collider Tool: 외부 숲 에셋의 Collider 정리 |
| `HorrorChaserAgent.LogPlan` (`_Game/Scripts/Goap/Enemies/Brain/HorrorChaserAgent.cs`) | 계획 로그: 목표나 계획이 바뀔 때 콘솔에 남김 (`LogPlans`, F11) |
| `HorrorChaserAgent.OnDrawGizmos`, `ExtinguishFireGoal.OnDrawGizmos`, `PathGrid.OnDrawGizmos`, `VisionSensor.OnDrawGizmosSelected` | 씬 뷰 Gizmo: 적의 잡는 반경 등, 끌 불, 격자, 시야 범위와 시선 |
| `_Game/Scenes/Demo/` | 시연 씬 7개 (`DemoSceneBuilder`가 만듦) |
