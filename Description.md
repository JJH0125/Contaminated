1. Node.cs
   길찾기에 필요한 노드를 구현한 클래스로, 다음과 같은 정보를 담고 있다.
   걸을 수 있는 노드인지
   실제 월드 좌표가 몇인지
   격자에서 몇 콤마 몇인지
   부모 노드
   f/g/h 비용
   heapindex

2. NodeHeap.cs
   Node를 담는 Heap을 관리하는 클래스.
   Add : 노드 추가
   RemoveFirst : 0번 인덱스의 노드 제거하여 return
   UpdateItem : 해당 노드를 맞는 위치에 정렬
   Contains : heap에 해당 노드가 있는지

3. PathGrid.cs
   월드를 XZ 평면 위의 격자 노드들로 쪼개고, 각 칸이 walkable한지 판정해서 들고 있는 클래스.
   BuildGrid : 격자 크기를 계산하고 Physics.CheckBox로 각 칸의 장애물 여부를 검사해 Node 배열을 채운다. Awake에서 자동 호출.
   NodeFromWorldPoint : 월드 좌표가 속한 Node를 반환. Pathfinder.FindPath와 IsWalkable에서 참조.
   GetNeighbors : 어떤 노드의 이동 가능한 이웃 노드들을 반환(대각선 코너컷 방지 포함). Pathfinder.FindPath에서 참조.
   IsWalkable : 월드 좌표가 walkable한지 반환. Pathfinder.IsWalkable, ChaserLocomotion.GetWanderTarget에서 참조.
   OnDrawGizmos : Scene 뷰에서 격자와 DebugPath를 색으로 시각화.

4. Pathfinder.cs
   PathGrid 위에서 그리드 A*를 돌려 실제 경로(월드 좌표 리스트)를 계산하는 클래스.
   IsWalkable : PathGrid.IsWalkable을 그대로 전달하는 래퍼. ChaserLocomotion.GetWanderTarget에서 참조.
   FindPath : 시작~목표 사이 A* 탐색 후 waypoint 리스트 반환. ChaserLocomotion.MoveTo, (구버전)ChaserAgent.RequestPath에서 참조.
   Heuristic : 대각선 거리 기반 휴리스틱 비용 계산. FindPath 내부에서만 사용.
   Distance : 인접 노드 사이 실제 이동 비용(직선 10 / 대각선 14). FindPath 내부에서만 사용.
   RetracePath : 목표 노드에서 Parent를 거슬러 경로를 역추적하고 PathGrid.DebugPath에 기록, waypoint 리스트로 변환. FindPath 내부에서만 사용.

5. ChaserAgent.cs (Astar3D, ⚠ CLAUDE.md 기준 deprecated — ChaserLocomotion + HorrorChaserAgent로 대체되어 제거 예정)
   GOAP 없이 순수 A\* 길찾기만 확인하려고 만든 테스트용 추격자 이동 스크립트. 실제 게임 로직에서는 쓰이지 않는다.
   Awake : Rigidbody, Pathfinder 참조 캐싱.
   Update : 재계산 타이머와 타겟 이동량을 검사해 RequestPath 호출 여부 결정.
   FixedUpdate : 매 물리 프레임 FollowPath 호출.
   RequestPath : Pathfinder.FindPath로 새 경로를 계산. Update에서 참조.
   FollowPath : 경로를 waypoint 단위로 따라가며 Rigidbody를 이동/회전. FixedUpdate에서 참조.

── Detection ──

6. Sound.cs
   Sound 클래스 : 소리 하나의 데이터(이름/반경/지속시간/유발 경계 단계/차원 관통 여부)만 담는 값 객체. 행동이 없고 데이터만 다르므로 상속 대신 값 채우기 방식(Goal과 같은 설계 원칙).
   SoundList 클래스 : Walking/Running/Generator/Decoy 등 게임에서 쓰는 모든 소리를 static readonly Sound로 미리 정의. Walking·Running은 PlayerStep.Update, Generator는 Object/Generator.cs의 Update에서 참조. Decoy는 아직 참조하는 곳 없음(미끼 아이템용으로 예약).

7. SoundEmitter.cs
   특정 위치에서 소리를 실제로 "방출"하는 정적 유틸리티 클래스.
   Emit : 위치·Sound 데이터·감지 대상 레이어를 받아 Physics.OverlapSphereNonAlloc으로 반경 안에 enemyLayer 오브젝트가 있는지 검사하고, 있으면 SquadBlackboard.ReportSound를 호출. PlayerStep.Update(발소리), Object/Generator.cs의 Update(발전기 소리)에서 참조.

8. VisionCensor.cs
   적의 시야 감지(거리→각도→차폐 3단계)를 수행하고 결과를 SquadBlackboard에 보고하는 컴포넌트.
   Update : detectInterval 주기로 DetectVision 실행.
   IsInViewDistance : 플레이어가 시야 거리 안에 있는지 XZ 평면 거리로 판정. DetectVision 내부에서 참조.
   IsInViewAngle : 플레이어가 시야각(viewAngle) 안에 있는지 판정. DetectVision 내부에서 참조.
   HasLineOfSight : 눈높이(eyeHeight)에서 플레이어 몸통(playerHeight)으로 레이캐스트해 장애물 유무 판정. DetectVision 내부에서 참조.
   DetectVision : 위 세 조건을 모두 만족하면 "봤다"고 판단. 보이면 SquadBlackboard.ReportSighting을 매번 호출해 위치를 갱신하고, 직전엔 보였는데 지금 안 보이면 SquadBlackboard.ReportLostSight를 한 번만 호출(상태 전환 감지, visibleJustBefore로 기억). Update에서 참조.
   OnDrawGizmosSelected : Scene 뷰에서 시야 거리/각도/레이를 시각화.

── 최상위(Assets 루트) ──

9. Follow.cs
   카메라가 target(플레이어)을 일정 offset을 두고 따라다니게 하는 컴포넌트.
   LateUpdate : target.position + offset으로 카메라 위치를 매 프레임 늦게 갱신. Update에서 갱신되는 target보다 나중에 실행되어 카메라 떨림을 방지.

10. InteractionPrompt.cs
    화면에 뜨는 상호작용 안내 문구("[E] 발전기 작동" 등)를 관리하는 씬 싱글톤. 여러 상호작용 오브젝트가 공용으로 Show/Hide를 호출한다.
    Awake : Instance 등록 후 HideImmediate로 초기 숨김.
    Show : owner와 문구를 받아 텍스트를 표시. Object/Generator.cs의 ShowPromptIfNeeded에서 참조.
    Hide : 호출한 owner가 마지막으로 띄운 주인일 때만 숨김. Object/Generator.cs의 HidePrompt에서 참조.
    HideImmediate : owner 검사 없이 즉시 숨김. Awake와 Hide 내부에서 참조.

11. Player.cs
    CharacterController 기반으로 플레이어의 이동/점프/회전을 담당하는 컨트롤러. 쿼터뷰 카메라 각도(45도)에 맞춰 입력 방향을 보정한다.
    Awake : CharacterController·Animator 참조 캐싱, 카메라 기준 forward/right 벡터를 미리 계산해 둔다.
    Update : 매 프레임 GetInput → Move → Turn 순서로 실행.
    GetInput : 방향키/Walk버튼/점프 입력을 읽는다. Update에서 참조.
    Move : 입력값과 카메라 축으로 수평 이동 벡터 계산, 점프 시 초기 y속도 부여, CharacterController.Move로 실제 이동, 착지/공중 여부에 따라 중력 처리, Animator 파라미터(isRun/isWalk/isInTheAir) 갱신. Update에서 참조.
    Turn : 이동 방향을 바라보도록 transform.LookAt으로 회전. Update에서 참조.

12. PlayerStep.cs
    플레이어가 움직이는 동안 Animator의 상태(isRun/isWalk)를 읽어 주기적으로 발소리를 방출하는 컴포넌트. Player.cs가 애니메이션을 위해 이미 계산해 둔 값을 재사용해 같은 판단을 두 번 하지 않는다.
    Awake : Animator 참조가 비어 있으면 자식에서 찾고, 파라미터 이름을 해시로 미리 변환.
    Update : 멈춰 있으면 타이머만 리셋하고, 움직이면 걷기/뛰기에 따라 다른 Sound(SoundList.Walking/Running)와 간격(walkInterval/runInterval)으로 SoundEmitter.Emit을 호출.

── Goap/Basic (GOAP 엔진 + 공용 데이터) ──

13. ChaserContext.cs
    GOAP Action과 Planner가 공통으로 참조하는 정보 묶음(추격자 자신, 블랙보드, 플레이어, 이동 헬퍼, CatchRadius/ArriveRadius 등 판정 반경)을 하나로 묶은 데이터 전달용 클래스. SquadAgent.Start와 HorrorChaserAgent.Start가 각자 자신만의 ChaserContext를 만들어 매 Action.Perform 호출 시 함께 넘긴다.

14. Goap.cs
    GOAP 엔진의 핵심 4개 클래스를 담은, 게임 로직(fact 이름 등)을 전혀 모르는 범용 엔진 파일.
    WorldState 클래스 (bool Dictionary인 Facts로 세계 상태를 표현) :
    Clone : Facts를 복사한 새 WorldState 반환. GoapPlanner.Plan 내부에서 다음 상태를 만들 때 참조.
    Matches : goal의 모든 fact가 자신의 Facts와 일치하는지 검사. GoapPlanner.Plan(전제조건/목표 달성 검사), HorrorChaserAgent.SelectGoal, SquadAgent.SelectGoal에서 참조.
    Apply : Action의 Effects를 자신의 Facts에 덮어써 병합. GoapPlanner.Plan 내부에서 참조.
    DistanceTo : goal과 다른 fact 개수(admissible 휴리스틱)를 반환. GoapPlanner.Plan 내부에서 PlanNode.HCost 계산에 참조.
    GoapAction 추상 클래스 : 이름/전제조건/효과를 갖고 GetCost·CheckProceduralPrecondition·Perform은 하위 클래스가 구현. Goap/SingleChaser/ChaserActions.cs와 Goap/MultipleChaser/Actions.cs의 각 Action 클래스들이 상속.
    Goal 클래스 : 이름·원하는 상태·우선순위만 담는 순수 데이터(코드 없어 상속 불필요). Goap/SingleChaser/ChaserGoals.cs, Goap/MultipleChaser/Actions.cs의 GoalSet에서 값을 채워 생성.
    GoapPlanner 정적 클래스 (WorldState를 노드로, Action을 엣지로 하는 상태공간 A*) :
    Plan : WorldState 공간에서 A* 탐색을 돌려 목표 상태에 도달하는 Action 순서(Queue)를 반환. HorrorChaserAgent.Replan, SquadAgent.Replan에서 참조.
    BuildPlan : 탐색이 끝난 PlanNode에서 Parent를 거슬러 올라가며 Action Queue를 구성. Plan 내부에서만 참조.

15. SquadBlackboard.cs
    씬 싱글톤. 모든 추격자가 함께 읽고 쓰는 "공유 기억"(경계 단계, 플레이어 시야/위치 정보, 소리 정보, 다중 추격자 역할 조율)을 담는다. 한 추격자의 감지가 여기 기록되면 다른 모든 추격자의 GOAP 판단에 즉시 반영된다.
    Awake : 싱글톤 인스턴스 등록(중복 인스턴스면 자신을 파괴).
    Update : 매 프레임 TimeSinceLastSeen·TimeInAlertState를 누적하고, 일정 시간(AlertedToSuspiciousTime/SuspiciousToCalmTime)이 지나면 경계 단계를 자동으로 낮춘다(Alerted→Suspicious→Calm).
    SetAlert : 경계 단계를 바꾸고 TimeInAlertState를 초기화. Update, ReportSighting, ReportSound 내부에서 참조.
    ReportSighting : 플레이어를 시야에서 발견했을 때 위치를 갱신하고 무조건 Alerted로 격상. Detection/VisionCensor.cs의 DetectVision에서 참조.
    ReportLostSight : 플레이어가 시야에서 사라졌을 때 PlayerCurrentlyVisible만 false로(LastPlayerPosition은 수색 단서로 유지). Detection/VisionCensor.cs의 DetectVision에서 참조(상태 전환 시 1회만).
    ReportSound : 소리를 들었을 때, 소리의 Alert 단계가 현재보다 높으면 격상하고 같으면 타이머만 유지, 낮으면 무시. HasSound·LastSoundPosition·SoundSource 갱신. Detection/SoundEmitter.cs의 Emit에서 참조.
    ClearSound : 소리 조사를 마쳤을 때 HasSound를 false로. Goap/SingleChaser/ChaserActions.cs의 MoveToSound·SearchSoundArea에서 참조.
    TryClaimRole / ReleaseRole : 다중 추격자가 매복 등 역할을 겹치지 않게 선점/반납. Goap/MultipleChaser/Actions.cs의 MoveToChokepointAction에서 참조(현재는 다중 추격자 데모에서만 사용).

── Goap/MultipleChaser (구버전 협력 데모, 다중 추격자 부활 대비용으로 보존) ──

16. Actions.cs
    GoalSet 정적 클래스 :
    ForPersonality : 성격(personality)에 따라 KillPlayer(10)·FindPlayer(5)·Patrol(1) 세 Goal 리스트를 생성해 반환. SquadAgent.SelectGoal에서 참조.
    ActionFactory 정적 클래스 :
    BuildActions : 모든 성격이 공유하는 다섯 Action(Chase/Chokepoint/SearchLastKnown/Attack/Patrol) 리스트를 생성. SquadAgent.Start에서 참조.
    ChasePlayerAction : 플레이어(playerVisible)에게 직접 접근해 nearPlayer를 만족. Aggressive 성격이면 비용이 싸 우선 선택된다.
    MoveToChokepointAction : 매복 지점으로 이동. CheckProceduralPrecondition에서 SquadBlackboard.TryClaimRole로 매복 역할을 선점해야 실행 가능(선점 실패 시 이 Action은 계획에서 제외됨). Ambusher 성격이면 비용이 싸다.
    SearchLastKnownAction : 마지막으로 목격된 위치(hasLastKnownPos)를 수색. Searcher 성격이면 비용이 싸다.
    AttackAction : nearPlayer 상태에서 플레이어를 공격해 playerDead를 만족(현재는 즉시 완료 처리만 하는 자리표시자).
    PatrolAction : 전제조건 없이 patrolled를 만족(현재는 즉시 완료 처리만 하는 자리표시자).
    이 파일의 핵심은 "같은 Action 목록이라도 personality별 GetCost가 달라 GoapPlanner가 서로 다른 계획을 세우게 된다"는 것 — 같은 두뇌, 다른 비용표.

17. SquadAgent.cs
    성격(Aggressive/Ambusher/Searcher)을 가진 협력형 추격자의 GOAP 두뇌. HorrorChaserAgent의 구버전으로, 다중 추격자 협력 데모용으로 남겨 두었다.
    Start : ChaserContext 생성, ActionFactory.BuildActions로 자신의 Action 목록 구성.
    Update : 계획이 없고 진행 중인 Action도 없으면 Replan, 현재 Action이 있으면 Perform 실행 후 완료되면 다음 Action으로 진행.
    Replan : BuildWorldState → SelectGoal → GoapPlanner.Plan 순서로 새 계획을 세운다. Update 내부에서 참조.
    BuildWorldState : SquadBlackboard의 정보를 바탕으로 이 개체 시점의 WorldState를 구성(다른 추격자의 감지 결과도 여기로 흘러들어온다). Replan 내부에서만 참조.
    SelectGoal : 아직 달성되지 않은 Goal 중 우선순위가 가장 높은 것을 선택. Replan 내부에서만 참조.
    ChaserPersonality enum : Aggressive/Ambusher/Searcher. Actions.cs의 비용 분기 조건으로 참조.

── Goap/SingleChaser (현재 실제로 쓰이는 단일 추격자 두뇌) ──

18. ChaserActions.cs
    ChaserActions 정적 클래스 :
    Build : ReachPlayer·MoveToSound·SearchSoundArea·WanderStep 네 Action 인스턴스를 리스트로 반환. HorrorChaserAgent.Start에서 참조.
    ReachPlayer : playerVisible일 때 플레이어에게 접근, ChaserLocomotion.MoveTo로 CatchRadius 안까지 들어오면 완료(playerCaught).
    MoveToSound : heardSound일 때 SquadBlackboard.LastSoundPosition으로 이동, 도착하면 완료(atSoundLocation). 경로가 없으면(HasValidPath false) 방어적으로 SquadBlackboard.ClearSound를 호출하고 완료 처리.
    SearchSoundArea : atSoundLocation 상태에서 SearchDuration(3초) 동안 ChaserLocomotion.LookAround로 주변을 둘러보다 완료(soundInvestigated), 완료 시 SquadBlackboard.ClearSound 호출.
    WanderStep : 전제조건 없이 항상 실행 가능. ChaserLocomotion.GetWanderTarget으로 목표를 얻어 이동하고, 도착하면 WanderPauseMin~WanderPauseMax 사이 무작위 시간 동안 대기(LookAround)한 뒤 완료 처리(wandering).

19. ChaserGoals.cs
    단일 추격자가 갖는 세 Goal(CatchPlayer=100, InvestigateSound=50, Wander=1)의 이름·우선순위·원하는 상태를 정의하는 정적 클래스. 싸울 수 없는 호러 추격자라 생존/후퇴 목표는 없음.
    Build : 세 Goal 리스트를 생성해 반환. HorrorChaserAgent.Start에서 참조.

20. ChaserLocomotion.cs
    Astar3D.Pathfinder를 감싸서 GOAP Action이 "어디로 이동해라"만 알면 되도록 해주는 이동 헬퍼. GOAP(무엇을 할지 결정하는 상태공간 A*)와 그리드 A*(어떻게 갈지) 사이를 잇는 다리.
    Awake : Rigidbody, Pathfinder 참조 캐싱.
    MoveTo : 목표 지점·도착 반경·현재 경계 단계를 받아 도착 여부를 판정하고, 필요하면 경로를 (재)계산한 뒤 FollowPath 호출. 경계 단계에 따라 이동 속도가 달라진다(MoveSpeedFor). Goap/SingleChaser/ChaserActions.cs의 ReachPlayer·MoveToSound·WanderStep에서 참조.
    MoveSpeedFor : 경계 단계(Calm/Suspicious/Alerted)에 따른 이동 속도를 반환. MoveTo 내부에서만 참조.
    LookAround : 제자리에서 서서히 회전하며 주변을 둘러본다. Goap/SingleChaser/ChaserActions.cs의 SearchSoundArea·WanderStep에서 참조.
    GetWanderTarget : 자신 주변의 무작위 walkable 지점을 최대 10회 시도해 찾아 반환(이미 있으면 재사용). Goap/SingleChaser/ChaserActions.cs의 WanderStep에서 참조.
    ClearWanderTarget : 배회 목표를 초기화해 다음 호출 때 새로 뽑도록 함. Goap/SingleChaser/ChaserActions.cs의 WanderStep에서 참조.
    HasValidPath : 유효한 경로가 있는지 여부(교착 상태 방어용). Goap/SingleChaser/ChaserActions.cs의 MoveToSound·WanderStep에서 참조.
    FollowPath : 계산된 경로를 waypoint 단위로 따라 Rigidbody를 이동/회전. MoveTo 내부에서만 참조.
    StopHorizontal : 수평 속도를 0으로. MoveTo(도착 시)와 LookAround 내부에서 참조.
    Flat : Vector3를 XZ 평면 값(y=0)으로 치환하는 보조 함수. 파일 내 여러 함수에서 공용으로 참조.

21. HorrorChaserAgent.cs [RequireComponent(ChaserLocomotion)]
    현재 실제로 쓰이는 단일 호러 추격자의 GOAP 두뇌. CatchPlayer·InvestigateSound·Wander 세 목표만 가지며, replanInterval마다 재계획하고 매 프레임 현재 Action을 진행시킨다.
    Awake : ChaserLocomotion 참조 캐싱.
    Start : ChaserContext 구성, ChaserActions.Build·ChaserGoals.Build로 Action/Goal 목록 로드.
    Update : 재계획 타이머를 관리하다 조건이 되면 Replan 호출, 현재 Action이 있으면 Perform 실행 후 완료되면 DoNextAction으로 다음 Action을 꺼냄.
    Replan : BuildWorldState → SelectGoal → GoapPlanner.Plan 순으로 새 계획을 세우고 DoNextAction으로 첫 Action을 꺼낸다. Update 내부에서 참조.
    BuildWorldState : SquadBlackboard 정보를 바탕으로 이 추격자 시점의 WorldState를 구성. Replan 내부에서만 참조.
    SelectGoal : 아직 달성되지 않았고 GoalIsRelevant를 통과한 Goal 중 우선순위가 가장 높은 것을 선택. Replan 내부에서만 참조.
    GoalIsRelevant : Goal을 지금 좇을 이유가 있는지 거르는 필터(예: 플레이어가 안 보이면 CatchPlayer는 애초에 무의미). SelectGoal 내부에서만 참조.
    Fact : WorldState에서 bool 값을 안전하게 꺼내는 보조 함수. GoalIsRelevant 내부에서만 참조.
    DoNextAction : 계획 Queue에서 다음 Action을 꺼내 \_currentAction에 저장. Update, Replan 내부에서 참조.
    OnDrawGizmos : CatchRadius와 마지막 소리 위치를 Scene 뷰에 시각화.

── Object ──

22. Generator.cs
    플레이어가 상호작용해 켤 수 있는 발전기. 켜져 있는 동안 주기적으로 소리를 방출해 "진행을 위해 켜야 하지만 켜면 들킨다"는 핵심 긴장을 만든다. 범위에 들어오면 InteractionPrompt로 안내 문구도 띄운다.
    Start : IsActive를 startsActive 초기값으로 설정.
    Update : 범위 안 + 아직 안 켜짐 + interactKey 입력이면 Activate 호출. 켜진 상태면 emitInterval마다 SoundEmitter.Emit으로 발전기 소리(SoundList.Generator) 방출.
    OnTriggerEnter / OnTriggerExit : 플레이어(playerLayer)가 상호작용 범위에 들고 날 때 \_playerInRange를 갱신하고 ShowPromptIfNeeded/HidePrompt를 호출.
    ShowPromptIfNeeded : 아직 안 켜졌다면 InteractionPrompt.Instance.Show 호출. OnTriggerEnter, Deactivate 내부에서 참조.
    HidePrompt : InteractionPrompt.Instance.Hide 호출. OnTriggerExit, Activate 내부에서 참조.
    IsInLayerMask : 레이어가 LayerMask에 포함되는지 비트 연산으로 검사. OnTriggerEnter/OnTriggerExit 내부에서만 참조.
    Activate : 발전기를 켜고 타이머를 초기화, 안내 문구를 숨긴다. Update에서 참조.
    Deactivate : 발전기를 끄고, 플레이어가 아직 범위 안이면 안내 문구를 다시 띄운다. 현재 이 스크립트 밖에서 호출하는 코드는 없음(외부 트리거용으로 열어 둔 공개 API).

소리가 발생되는 과정
(처음 듣는 소리)
각 오브젝트의 스크립트가 Emit 호출
Emit 함수는 ReportSound 호출
ReportSound는 이미 조사된 소리가 아니라면 board를 업데이트하여 적이 조사하게끔 유발
적은 조사한 후 AddInvestigateCompleted 호출
AddInvestigateCompleted는 board를 업데이트하여 조사된 소리 관리
(이미 들었던 소리)
Emit 호출
ReportSound 호출
ReportSound는 조사된 소리를 받았으므로 아무것도 하지 않음

차원 이동 구현
차원은 AlertLevel과 같이 각자 고유한 데이터를 가진 것이 아닌, 이름표일 뿐이므로 enum으로 손쉽게 정의 및 관리가 가능.

만들 조각들

1. 차원 표시 — Dimension enum + DimensionMember (완료)

2. 플레이어의 차원 — Player에 상태 추가 (완료)

3. 감지 관문 — VisionCensor

지금 거리·각도·차폐 세 조건을 &&로 엮고 있죠. 거기에 "같은 차원인가"를 하나 더 넣습니다.

앞서 정한 대로 가장 싼 검사를 앞에 두면 되고요.

4. 보이는 것 — 렌더러 켜고 끄기

플레이어 차원이 바뀔 때, 다른 차원 오브젝트의 렌더러를 끕니다. 로직은 살려두고 그리기만 멈추는 거죠.

이걸 누가 관리할지가 판단 지점입니다. 각 DimensionMember가 스스로 판단할 수도 있고, 어딘가 관리자가 일괄 처리할 수도 있어요.

5. 전환 존

발전기와 거의 같은 구조입니다. 트리거 콜라이더, InteractionPrompt, 키 입력. 앞서 만든 걸 그대로 재사용할 수 있죠.

1차 작업

새로 생성된 파일

DimensionController : 빈 오브젝트를 만들어 컴포넌트로 추가.
enum 형식으로 두 차원을 정의한다.
플레이어와 적들의 차원 정보를 Awake에서 받아놓는다.
Portal이 차원 변경을 알리면, 플레이어의 차원을 바꿔준 뒤
각각의 적들의 MeshRenderer.enabled 정보를 foreach문 안에서 switch해준다.

DimensionMember : 적 오브젝트에 컴포넌트로 추가.
차원을 초월하지 못하고, 넘나들지도 못하는,
차원 정보가 고정되어 있는 적에게 붙이는 딱지같은 개념.
DimensionController가 이 딱지를 보고, 플레이어와 같은 차원에 있는 적만
MeshRenderer를 켜고, 다른 차원에 있는 적은 MeshRenderer를 꺼서 보이지 않게 한다.

Portal : 포탈 오브젝트에 컴포넌트로 추가.
DimensionController를 멤버변수로 가진다.
플레이어가 다가와 키를 누르면 컨트롤러에게 알린다 (함수 호출)

변경된 기존 파일

Player :
플레이어가 가진 차원을 바꾸는 함수 추가
호출하는 주체는 컨트롤러.

VisionCensor :
차원을 검사하는 절차를 맨 먼저 밟도록 수정해야 함. 어떻게 수정할까?

2차 작업

문제 1 (완)
DimensionController를 싱글톤으로 만들고
컨트롤러를 참고하는 Portal, VisionCensor에서 Instance를 호출하도록 수정해야 함

문제 2 (완)
컨트롤러가 플레이어의 차원을 바꿀 때 불필요한 dimension 필드가 사용되고 있음
컨트롤러가 이 필드를 가질 필요가 있을까?
-> Player의 SwitchMyDimension을 void로 바꿈. 컨트롤러에서는 따로 자신의 필드에 무언가를 받지 않고 플레이어에 명령만 내림.

문제 3
게임이 시작됐을 때 렌더러들의 정리가 필요함. 지금은 차원이 바뀔 때만 렌더러가 정리되고 있음

문제 4 (완)
적의 렌더러를 바꾸는 것을 컨트롤러가 직접 할 필요가 있을까? member에서 알아서 하라고 하면 될 듯

문제 5 (완)
IsInSameDimension 구현. 컨트롤러에게 차원 정보들을 받아오는 편이 좋을 듯
-> Transform만으로는 알 수 없기에 parameter는 따로 두지 않음.
GetComponent로 해당 적 개체의 member를 가져와서, 컨트롤러에 비교해달라고 요청
컨트롤러는 플레이어의 차원과 (get; private set; 을 통해 컨트롤러에서도 알 수 있음)
요청받은 member의 차원을 비교하여 bool 값을 센서에 전달

3차 작업
DimensionMember.ToggleRenderer()이 Renderer 자기 자신만 보고 켜고 끄고 있다
-> 컨트롤러에 있는 Compare 함수 활용!

enemies가 public이어어서 캡슐화 관점에서 아쉬운 요소.
-> private으로 바꾸고 컨트롤러에 멤버를 추가하는 public 함수 추가

IsInSameDimension이 Update마다 GetComponent하는 문제
-> GetComponent는 Censor의 Start에서 한번만 하는 것으로 수정

4차 작업
현재 구조가
Controller → Member (enemies에 담고 있음)
Member → Controller (CompareDimension 호출)
서로를 참조하는 구조 (되도록이면 피해야 할 점)
대안 -> SetRenderer가 판단 결과를 받도록!
컨트롤러가 비교하여 너 나타나/숨어 를 지시
멤버는 받은 대로 렌더러만 처리
요지는, 멤버가 컨트롤러를 모르게!

Q: 내가 적의 WorldState에 대해서 제대로 이해하고 있는게 맞는지 모르겠는데, 게임이 시작되고 나서 적의 State는 비어있는 상태고, 소리를 한번이라도 듣는 순간 HeardSound 키가 생기고, 플레이어를 한번이라도 보는 순간 PlayerVisible이 생기는 구조 맞나?

A: 아니요, 그렇지 않습니다. 매 재계획마다 새로 만들어집니다.
실제 흐름
BuildWorldState가 하는 일을 보면 명확합니다.

Replan() 호출 (0.4초마다)
→ BuildWorldState()
새 WorldState 객체 생성
블랙보드를 읽어서 fact들을 채움
→ 이 상태로 계획 수립

매번 새 WorldState를 만들고, 그 시점의 블랙보드를 스냅샷처럼 찍는 겁니다.

키는 처음부터 있습니다 :

BuildWorldState는 조건 없이 모든 fact를 채웁니다.

playerVisible = (블랙보드 값) ← true든 false든 항상 넣음
heardSound = (블랙보드 값) ← 마찬가지

그러니 게임 시작 직후에도 키는 다 있습니다. 값이 false일 뿐이죠.

게임 시작
→ WorldState: { playerVisible: false, heardSound: false, ... }

"소리를 들으면 키가 생긴다"가 아니라, 키는 항상 있고 값이 바뀌는 구조입니다.

누적되지 않습니다 :

이게 중요한 지점입니다. WorldState는 기억이 아닙니다.

0.0초: WorldState 생성 → { playerVisible: true } → 사용 후 버림
0.4초: WorldState 생성 → { playerVisible: false } → 사용 후 버림
0.8초: WorldState 생성 → { playerVisible: false } → ...

이전 상태를 이어받지 않고, 매번 처음부터 만듭니다.

기억을 담당하는 건 블랙보드죠. 블랙보드가 PlayerCurrentlyVisible, HasSound, Alert 같은 걸 계속 들고 있고, WorldState는 그걸 매번 복사해 오는 겁니다.

그럼 WorldState는 왜 있나 :

\*GOAP 플래너가 A 탐색을 하기 위한 재료\*\*입니다.

앞서 다룬 상태공간 A\*를 떠올려보세요.

현재 상태 → [행동 적용] → 새 상태 → [행동 적용] → ... → 목표 상태

플래너는 이 탐색 과정에서 가상의 상태들을 계속 만들어냅니다.

{ playerVisible: true, playerCaught: false }
→ ReachPlayer 적용 (Effects: playerCaught = true)
{ playerVisible: true, playerCaught: true } ← 이건 상상 속의 상태
→ 목표와 일치? → 계획 완성

두 번째 상태는 실제로 일어난 게 아닙니다. "이 행동을 하면 이렇게 될 것"이라는 가정이죠.

그래서 WorldState에 Clone()과 Apply()가 있는 겁니다. 원본을 복제해서 효과를 적용해보는 거죠.

정리하면 역할이 다릅니다 :
블랙보드 WorldState
성격 지속되는 기억 일회용 스냅샷
수명 게임 내내 재계획 한 번
누가 씀 감지가 쓰고 GOAP가 읽음 플래너가 탐색에 사용
내용 실제 상황 실제 + 가상

BuildWorldState는 둘 사이의 번역기입니다. "블랙보드의 언어"를 "플래너가 이해하는 fact"로 옮기는 거죠.

B 방식 구현에 이게 왜 중요한가 :

지금 하시려는 작업이 정확히 이 번역 지점에 손대는 겁니다.

지금: 블랙보드 값을 그대로 옮김
바꾸면: 옮기면서 "나에게 유효한가"를 거름

WorldState가 매번 새로 만들어지니, 필터가 매 재계획마다 다시 적용됩니다. 차원이 바뀌면 다음 재계획(0.4초 이내)에 반영되죠.

한 번 걸러진 게 남아 있을 걱정은 없습니다. 매번 새로 만드니까요.

Q: private static bool Fact(WorldState s, string key)
=> s.Facts.TryGetValue(key, out bool v) && v;
그러면 GoalIsRelevant 함수에 쓰이는 이 Fact 함수는 s에 key가 있는지를 먼저 검사하고 그 다음에 key의 value가 true인지를 검사하는 거 아냐? key가 존재하지 않을 일이 없다면 key가 있는지 검사는 왜 하는 거지?

A: 좋은 지적입니다. 지금 BuildWorldState가 만든 상태에 대해서는 그 검사가 불필요합니다. 모든 키를 항상 채우니까요.

그런데 이 함수가 그렇게 생긴 이유가 있습니다.

WorldState는 다른 데서도 만들어집니다 :
앞서 얘기했듯, 플래너가 탐색하면서 가상의 상태를 계속 만들죠. 그리고 Goal의 Desired도 WorldState입니다.

csharp
// ChaserGoals에서
catchPlayer.Desired.Facts["playerCaught"] = true; // 키가 하나뿐
목표는 "원하는 것"만 담습니다. playerVisible이나 heardSound는 안 들어 있죠. 앞서 Matches가 "goal의 fact만 검사한다"고 했던 게 이 때문입니다.

이런 WorldState에 Fact(goal.Desired, "heardSound")를 부르면 키가 없습니다.

없는 키를 그냥 읽으면 :
csharp
s.Facts["heardSound"] // KeyNotFoundException 발생!
Dictionary는 없는 키를 읽으면 예외를 던집니다. 게임이 멈추죠.

TryGetValue는 그걸 막고 "없으면 false"로 처리합니다.

의미상으로도 맞습니다 :
GOAP에서 "키가 없다"와 "false"는 같은 뜻으로 취급하는 게 자연스럽습니다.

heardSound가 없다 → 소리를 못 들었다 → false
"모르는 것은 참이 아니다"라는 규칙이죠. 이걸 closed-world assumption이라고 부릅니다. 자동 계획 분야에서 표준적인 가정입니다.

그래서 이 한 줄이 두 가지를 동시에 합니다.

예외 방지 (기술적)
"없음 = 거짓" 규칙 표현 (의미적)
앞서 다룬 것과 같은 맥락
WorldState.Matches를 떠올려보세요.

goal의 fact만 검사, 나머지는 무시
goal에 없는 fact는 "상관없다"는 뜻이었죠. 여기 Fact도 같은 관용성을 갖는 겁니다.

엔진이 특정 fact 집합을 전제하지 않는다는 설계예요. 앞서 "Goap.cs는 fact 이름을 모르는 범용 엔진"이라고 한 것과 이어집니다.

그래서 지금은 불필요하지만
말씀하신 대로, BuildWorldState가 만든 상태에서는 키가 항상 있으니 TryGetValue의 첫 검사가 매번 통과합니다.

하지만 이 함수는 어떤 WorldState가 들어와도 안전하게 만들어졌습니다. 나중에 이런 일이 생겨도 안 깨지죠.

fact를 조건부로만 채우는 경우
플래너가 만든 중간 상태를 검사하는 경우
goal의 Desired를 검사하는 경우
성능은 무시할 수준
혹시 그게 걱정이시라면, TryGetValue는 []와 거의 같은 비용입니다. 해시로 한 번 찾아보고 있으면 값을, 없으면 false를 반환하는 것뿐이죠.

오히려 ContainsKey로 확인한 뒤 []로 읽으면 두 번 찾게 되어 느립니다. TryGetValue가 그걸 한 번에 하는 게 존재 이유고요.

정리
지금 상황에서는 방어적인 코드가 맞습니다. 다만 "왜 있는지 모르고 두는 것"과 "알고 두는 것"은 다르죠.

알고 계시면 나중에 판단할 수 있습니다. 예를 들어 "fact를 조건부로 채우도록 바꾸자"고 할 때, 이 함수가 이미 대비되어 있다는 걸 아니까요.

앞으로 할 것

게임 흐름 만들기

1. 클리어 조건 (발전기 전부 → 출구 활성화 → 탈출)
2. 게임 오버 (잡히면)
3. 반복 구조 (탈출 후 다음 회차)
4. 난이도 상승

다중 추격자

조명 구현

GameManager의 역할
발전기가 자신을 등록하게끔 만들기
발전기가 켜질 때마다 상태를 저장하고 모든 발전기가 켜졌는지 검사
모두 켜졌으면 출구 활성화
활성화된 출구에 도착하면 클리어

주의 — 책임이 번지지 않게
앞서 여러 번 나온 기준입니다. GameManager가 "게임 진행 상태"만 관리하게 하시고, 이런 건 다른 데 두세요.
UI를 직접 그리기 → UI 담당이 따로
발전기 목록을 직접 뒤지기 → 발전기가 알리게
적을 생성하기 → 나중에 스포너가

9/13 GameManager 구현 작업 착수.

9/22
차원을 넘는 순간 잡힐 때 게임오버되는 버그

Stage라는 부모를 만들고
맵, 플레이어, 적 등을 모두 자식으로 집어넣는다
게임 및 라운드 시작, 종료 때마다 Stage를 생성하고 삭제한다
이렇게 하면 각 개체 간의 인스펙터 연결도 살릴 수 있다

Stage 안에 담을 것 : SquadBlackboard, DimensionController, PathController(맵이 바뀔 수 있는 경우)

9/29
숲 에셋
고쳐야 할 것
노멀맵이 일반 텍스처로 임포트되어 있습니다. NormalMap.png와 NormalMap (2).png의 Texture Type이 Default로 되어 있습니다. 이대로 머티리얼에 연결하면 표면 음영이 이상하게 나옵니다. 인스펙터에서 Texture Type → Normal map으로 바꾸고 Apply를 누르세요.
텍스처가 모델에 연결되지 않았을 가능성이 높습니다. Sketchfab의 FBX는 텍스처 연결 정보가 빠져 있는 경우가 많습니다. 씬에 놓았을 때 회색이나 흰색으로 보이면 머티리얼을 직접 만들어 텍스처를 연결해야 합니다. 어느 텍스처가 어느 부분(나무, 땅, 길)용인지는 이름만으로 알 수 없으니 하나씩 넣어 보셔야 합니다.
카메라와 조명 임포트가 켜져 있습니다 (importCameras, importLights). Sketchfab 모델에는 미리보기용 카메라와 조명이 함께 들어 있는 경우가 있습니다. 그러면 씬에 예상치 못한 카메라나 조명이 생겨 화면이나 밝기가 달라질 수 있습니다. 모델 임포트 설정에서 둘 다 끄는 것을 추천합니다.
게임에 쓰려면 고려할 것
충돌체(Collider)가 없습니다 (addColliders: 0). 이 게임에서는 특히 중요합니다.
PathGrid는 Physics.CheckBox로 걸을 수 있는 곳을 판정합니다. 충돌체가 없으면 추격자가 나무를 통과하는 경로를 만듭니다.
시야 감지의 HasLineOfSight는 레이캐스트를 씁니다. 충돌체가 없으면 나무 뒤에 숨어도 보입니다.
모델 전체에 Mesh Collider를 켜는 방법(Generate Colliders)은 간단하지만, 39MB짜리 모델이면 무거울 수 있습니다. 나무 기둥마다 Capsule/Box Collider를 직접 배치하는 편이 가볍고, 판정도 의도대로 조절할 수 있습니다.
용량과 성능: FBX만 39MB입니다. Unity에서 모델을 선택하면 인스펙터 아래 미리보기에 정점(verts)과 삼각형(tris) 수가 나옵니다. 삼각형이 수백만 개라면 쿼터뷰 게임에는 과할 수 있습니다.
Git 용량: 이 커밋 하나로 저장소가 약 70MB 늘었습니다. GitHub는 파일 하나가 100MB를 넘으면 푸시를 거부하고, 50MB부터는 경고를 띄웁니다. 앞으로 큰 에셋을 더 넣으실 거면 Git LFS 설정을 추천합니다. 텍스처를 수정할 때마다 14MB씩 저장소에 쌓이기 때문입니다.
라이선스: Sketchfab 모델은 대부분 CC BY(출처 표시 필수) 같은 라이선스가 붙어 있습니다. 졸업 작품이라면 모델 페이지에서 라이선스를 확인하고, 크레딧에 제작자를 적어 두세요.
1~3번은 Unity 인스펙터에서 바로 고칠 수 있습니다. 숲 모델을 씬에 배치하실 때 4번(충돌체) 작업도 같이 도와드리겠습니다.

참고: 바로 쓸 수 있는 소리
SciFi Warehouse Kit 에셋에 쓸 만한 효과음이 들어 있습니다(Assets/TextureAssets/SciFi Warehouse Kit/Demo/Audio/SFX/).

footstep.wav: Walking과 Running 발소리
enginestart.wav: 발전기를 켤 때 한 번 나는 소리
Fan_St.wav 또는 background_rumble.wav: 발전기 작동 중 반복되는 소리

9/30
Follow.cs의 offset에 대해 수정
: 위에서 내려다보는 각도, 플레이어와 떨어진 거리를 인스펙터에서 설정하도록 수정

네임스페이스 정리
정리하는 것을 추천합니다. 지금 당장 오류가 나는 것은 아니지만, 이름이 내용을 설명하지 못하는 상태입니다.

현재 상태
namespace 스크립트 수 내용
Squad 33개 GOAP, 감지, 소리, UI, 음향, 발전기와 출구, 게임 매니저, 에디터 도구까지 거의 전부
Astar3D 5개 길찾기
없음 2개 Follow.cs, Player.cs
Squad는 원래 "분대 AI(협력 추격자)"용으로 붙인 이름으로 보입니다. 지금은 PauseUI, MusicPlayer까지 전부 Squad 안에 있어서, 이름만 보면 분대와 상관없는 코드도 분대 AI의 일부처럼 보입니다.

정리하면 좋은 이유
졸업 작품의 핵심 주장과 연결됩니다. CLAUDE.md의 원칙 중 하나가 "GOAP 엔진(범용)과 게임 전용 코드의 분리"입니다. 지금은 Goap.cs가 PauseUI와 같은 namespace에 있어서 코드 구조만으로는 이 분리가 드러나지 않습니다. 엔진을 별도 namespace로 빼면 **"이 엔진은 게임을 모른다"**는 것이 using 목록으로 증명됩니다.
의존 관계가 드러납니다. namespace를 나누면 다른 영역을 쓰는 파일마다 using이 붙습니다. 예를 들어 SoundEmitter(감지)가 SoundRingDisplay(디버깅)를 호출하는 것 같은 연결이 파일 맨 위에서 바로 보입니다.
폴더 구조가 이미 나뉘어 있습니다. Goap/, Detection/, UI/, Objects/, Debugging/ 폴더가 있으니 폴더에 맞춰 namespace를 붙이면 됩니다. 새로 설계할 필요가 거의 없습니다.
제안하는 구조
프로젝트 이름을 뿌리로 두고 폴더에 맞춥니다. HorrorGame은 예시이니 원하시는 이름으로 바꾸시면 됩니다.

HorrorGame.AI.Goap Goap.cs (범용 엔진, 게임 이름을 모름)
HorrorGame.AI.Chaser HorrorChaserAgent, ChaserActions, ChaserGoals, ChaserLocomotion,
ChaserContext, SquadBlackboard
HorrorGame.AI.Squad SquadAgent, Actions (다중 추격자 데모)
HorrorGame.AI.Pathfinding Node, NodeHeap, PathGrid, Pathfinder (지금의 Astar3D)
HorrorGame.Detection VisionCensor, Sound, SoundLibrary, SoundEmitter
HorrorGame.Audio MusicPlayer, SfxPlayer
HorrorGame.Gameplay GameManager, Generator, Exit, Portal, Decoy, Dimension*, Player*, Follow
HorrorGame.UI UI 폴더 전체
HorrorGame.Debugging AlertIcon, SoundRingDisplay
HorrorGame.Editor MapBuilder
ChaserAgent.cs는 CLAUDE.md에서 "제거 예정"이라고 한 파일이라, 이번 기회에 지우는 것도 방법입니다.

주의할 점: 버튼 연결 5개
씬과 프리팹의 거의 모든 참조는 파일 ID(meta의 GUID)로 연결되어 있어서 namespace를 바꿔도 깨지지 않습니다. 하지만 버튼의 OnClick 연결은 클래스 이름을 문자열로 저장합니다. 확인해 보니 5개가 있습니다.

Squad.TitleUI (2개), Squad.PauseUI, Squad.ResultUI, Squad.GameOverUI
namespace를 바꾸면 이 연결이 "Missing"으로 보일 수 있습니다. 씬 파일에서 이 문자열도 함께 바꾸거나, 바꾼 뒤 Unity에서 버튼 5개의 OnClick을 확인하고 다시 연결해야 합니다.

진행 방법
작업 규모는 33개 파일의 namespace 줄과 using 추가, 씬 파일의 문자열 5개입니다. 한 번에 바꿀 수 있는 양입니다.

작업 전: 지금 상태를 먼저 커밋하고 푸시합니다. 문제가 생기면 되돌리기 쉽습니다.
작업 중: Unity 에디터를 닫아 둡니다. 씬 파일을 수정하는 동안 Unity가 씬을 덮어쓰지 않게 하기 위해서입니다.
작업 후: Unity를 열어 컴파일 에러가 없는지, 버튼이 동작하는지 확인합니다.
뿌리 이름(HorrorGame 또는 다른 이름)과 위 구조로 진행할지 정해 주시면 작업하겠습니다. 구조가 너무 잘게 나뉘었다고 느끼시면 AI, Gameplay, UI 정도로 더 크게 묶는 방법도 있습니다.

c:\Users\ez\Documents\Assets\3D---\Assets\Scripts\SfxPlayer.cs
음, 그냥 스크립트가 들어있는 폴더의 이름을 따서 namespace를 지어도 되려나?
Bash Check root-level files and potential name clashes
IN
cd "C:/Users/ez/Documents/Assets/3D---/Assets/Scripts" && ls; grep -n "class \|: Editor\|EditorWindow" Editor/MapBuilder.cs | head -5; grep -n "^\s*public class" Player/*.cs

OUT
Astar
Astar.meta
Debugging
Debugging.meta
Detection
Detection.meta
DimensionController.cs
DimensionController.cs.meta
DimensionMember.cs
DimensionMember.cs.meta
Editor
Editor.meta
GameManager.cs
GameManager.cs.meta
Goap
Goap.meta
MusicPlayer.cs
MusicPlayer.cs.meta
Objects
Objects.meta
Player
Player.meta
SfxPlayer.cs
SfxPlayer.cs.meta
UI
UI.meta
24: public static class MapBuilder
Player/Follow.cs:12:public class Follow : MonoBehaviour
Player/Player.cs:17:public class Player : MonoBehaviour
Player/PlayerStep.cs:21: public class PlayerStep : MonoBehaviour
네, 괜찮습니다. 폴더 이름 = namespace는 C#에서 가장 흔한 규칙이고, Visual Studio나 Rider도 기본으로 이 규칙을 따르라고 권합니다. 폴더를 보면 namespace를 알 수 있어서 따로 외울 것이 없다는 장점이 큽니다.

다만 지금 폴더 구조 그대로 적용하면 걸리는 곳이 네 군데 있습니다.

그대로 적용하면 생기는 문제
Scripts 폴더 바로 아래에 있는 파일 5개: GameManager, MusicPlayer, SfxPlayer, DimensionController, DimensionMember는 들어 있는 폴더가 Scripts라서 따를 이름이 없습니다. 폴더를 새로 만들어 옮기면 해결됩니다(예: Audio/, Core/).
Player 폴더 안의 Player 클래스: namespace와 클래스 이름이 같으면 C#에서 자주 문제가 생깁니다. 다른 파일에서 Player라고 쓰면 컴파일러가 namespace를 말하는 것으로 해석해서, Player.Player처럼 써야 하는 상황이 생깁니다. 폴더 이름을 바꾸거나(예: Characters/) 이 폴더만 다른 namespace를 써야 합니다.
Editor 폴더: Unity에는 UnityEditor.Editor라는 클래스가 있어서, Editor라는 namespace를 만들면 나중에 커스텀 인스펙터를 만들 때 이름이 충돌합니다. 게다가 Unity에서 Editor 폴더는 "빌드에서 제외"라는 특별한 의미가 있어서 폴더 이름을 바꿀 수도 없습니다. 이 폴더만 예외로 EditorTools 같은 namespace를 쓰는 것이 좋습니다.
뿌리 이름이 없으면 일반적인 이름이 됩니다. UI, Objects, Debugging처럼 일반적인 이름을 맨 앞에 두면, 에셋이나 Unity 기능의 이름과 겹칠 여지가 생깁니다. 앞에 프로젝트 이름을 붙여 HorrorGame.UI처럼 쓰는 것이 일반적입니다. 이렇게 해도 규칙은 여전히 "폴더 이름"입니다.
규칙을 적용한 결과 (예시)

Scripts/ → HorrorGame (뿌리)
├─ Astar/ → HorrorGame.Astar
├─ Audio/ (새 폴더) → HorrorGame.Audio MusicPlayer, SfxPlayer
├─ Core/ (새 폴더) → HorrorGame.Core GameManager, Dimension\*
├─ Characters/ (Player에서 이름 변경) → HorrorGame.Characters Player, PlayerStep, Follow
├─ Debugging/ → HorrorGame.Debugging
├─ Detection/ → HorrorGame.Detection
├─ Editor/ → HorrorGame.EditorTools (예외)
├─ Goap/Basic/ → HorrorGame.Goap.Basic
├─ Goap/SingleChaser/ → HorrorGame.Goap.SingleChaser
├─ Goap/MultipleChaser/ → HorrorGame.Goap.MultipleChaser
├─ Objects/ → HorrorGame.Objects
└─ UI/ → HorrorGame.UI
고려할 점: Goap/Basic 폴더
Goap/Basic에는 범용 엔진인 Goap.cs와 이 게임 전용인 SquadBlackboard, ChaserContext가 섞여 있습니다. 폴더 규칙만 따르면 엔진과 게임 전용 코드가 같은 namespace에 들어갑니다. "엔진과 게임 코드의 분리"를 구조로 보여 주고 싶으시면 Goap.cs만 Goap/Engine/ 같은 폴더로 옮기는 것을 추천합니다. 필수는 아닙니다.

작업 방식
폴더를 옮기거나 이름을 바꾸는 것은 Unity 에디터 안에서 하셔야 합니다. 탐색기에서 옮기면 meta 파일이 따라오지 않아 연결이 깨질 수 있습니다. 순서는 이렇게 하면 됩니다.

Unity에서 할 일: 새 폴더(Audio, Core)를 만들어 파일을 옮기고, Player 폴더 이름을 바꿉니다.
제가 할 일: Unity를 닫은 상태에서 모든 파일의 namespace와 using, 씬의 버튼 연결 문자열 5개를 고칩니다.
뿌리 이름과 새 폴더 이름을 정해 주시면 1번이 끝난 뒤에 2번을 진행하겠습니다.

10/7
필요한 것 : 블랙보드 수정
PlayerCurrentlyVisible이 참/거짓 하나만 들고 있기 때문에
A가 놓치면 B가 보고 있어도 "아무도 못 보고 있음"이 된다

블랙보드가 Level을 담는 구조를 없애고
적 각각에게 Level을 부여한다.

또한 차원 이동이 유의미한 재미를 주도록

1. 차원마다 길을 다르게 배치한다.
2. 필요한 아이템을 다른 차원에 드롭되도록 한다.
3. 이동에 cost를 추가한다.

또한 차원 이동 직후에 죽는 일이 없도록

1. 건너편 차원의 적이 근처에 있다면 포탈이 경고하도록 한다. (예를 들면 붉게 빛나도록?)
2. 이동한 직후 누구에게도 감지되지 않는 짧은 텀을 준다.

10/12

해야 할 일

1. 맵 크기에 따른 속도 기준 잡기

한 라운드를 몇 분으로 할지를 먼저 정하면 나머지가 따라와요. 출구에서 봉우리 너머 발전기까지 대략 120~150m예요. 발전기 3개를 돌고 다시 출구로 오면 400~500m쯤 걷게 돼요.

플레이어 속도 맵 가로지르기(200m) 라운드 이동 거리
15 (현재) 약 13초 약 30초
8 약 25초 약 1분
5 약 40초 약 1분 40초

지금 속도 15면 맵이 금방 좁게 느껴져요. 숨고 피하는 시간을 더하면 달리기 7~8, 걷기 3.5~4 정도가 한 라운드 3~5분이 되는 수준이에요.

추천 값
항목 지금 추천 근거
플레이어 달리기 / 걷기 15 / 절반 7~8 / 3.5~4 위 표
적 Calm 5 3 플레이어가 걸어도 따돌릴 수 있게
적 Suspicious 8 5 플레이어 걷기보다 조금 빠르게
적 Alerted 13 7 플레이어 달리기보다 조금 느리게
배회 반경 6 25~35 아래 문제 2 참고
시야 거리 (어둠 기준) 18 15~18 유지 아래 설명
미끼 소리 반경 10 25~35 맵의 1/6 정도 범위

2. 발전기 표시 화살표 추가?
3. Fake 차원일 때 화면에 일렁임, 노이즈 같은 특수 효과 추가
4. 카메라와 플레이어 사이의 나무 반투명 처리
5. 적이 내는 소리 추가, 적에게 발각됐을 때의 효과음 추가
6. 게임 이름 짓기
   이름은 무엇을 가장 먼저 떠올리게 할지에 따라 방향이 갈립니다. 이 게임에서 내세울 만한 축이 네 가지라, 축별로 몇 개씩 뽑아봤어요.

숨는 게임이라는 점

숨 — 개인적으로 가장 추천하는 이름입니다. "숨을 죽이다"의 숨과 "숨다"의 숨이 겹쳐요. 소리를 내면 들키고, 결국 숨어야 사는 게임이라는 핵심이 한 글자에 다 들어갑니다. 영어로 병기한다면 Hush나 Breath.

두 개의 세계라는 점

이 게임만의 차별점이라, 전시장에서 이름만 보고도 "뭔가 다르다"를 느끼게 하려면 이쪽입니다.

이면의 숲 — 슬라이드에서 Fake 차원을 "이면"이라고 불렀죠. 그 단어를 그대로 살린 이름
틈 — 두 세계 사이의 틈. 짧고, 공포 게임 특유의 불길함이 있습니다
Mirrorwood — 거울(mirror)과 숲(wood). 같은 맵을 공유하는 두 세계를 영어 한 단어로
빛과 불이라는 점

모닥불과 나뭇가지 설정이 들어가면 살아나는 이름입니다.

불씨 / Last Ember — 손에 든 마지막 불빛. "밝혀야 보이지만, 밝히면 들킨다"는 위험과 보상이 담깁니다
발전기라는 점
Hum — 발전기의 웅웅거리는 소리. 켜야 나가는데, 그 소리가 적을 부르죠
고를 때 기준

전시장에서 읽히는가 — 관람객은 부스 앞을 지나가며 몇 초 안에 판단합니다. 짧고 발음하기 쉬운 이름이 유리해요.

이미 있는 게임과 겹치지 않는가 — 영어 이름은 특히 겹치기 쉽습니다. 저는 지금 검색해보지 않았으니, 마음에 드는 후보가 생기면 Steam이나 구글에서 한 번 찾아보세요. Hush 같은 짧은 영어 단어는 이미 쓰인 경우가 많습니다.

발표 논점과 이어지는가 — 지금까지 발표는 "판단하는 적"과 "두 개의 세계"가 중심이었죠. 이름이 그중 하나를 받쳐주면 첫 슬라이드부터 이야기가 자연스럽게 시작됩니다.

어느 쪽 분위기가 끌리세요? 방향을 정해주시면 그쪽으로 더 다듬어 보겠습니다.

7.

10/17
발전기 제거
횃불을 던지는 기능을 제거, 주인공은 기본적으로 횃불을 들고 있음
주인공이 기본적으로 걷고, shift를 눌러야 뛰는 것으로 변경
횃불을 들고 있는 상태에서 space를 꾹 누르고 있으면 횃불을 숨기고 떼면 다시 꺼내도록 변경 (주머니에 손을 대는 모션만 추가하고, 횃불 오브젝트를 활성화/비활성화하도록)
숨기고 있는 동안 스테미나 게이지가 화면에 등장하고 게이지가 점점 줄어들음. 게이지의 일정 수치마다 효과를 추가함. 게이지가 모두 달면 주인공이 의지와 상관없이 횃불을 강제로 듦. 횃불을 들면 게이지 회복.
100~50% 하얀 서리 결정이 가장자리에서 천천히 자라 들어옴. 순수하게 "추워진다"는 느낌
50% 이하 게이지가 붉어지고, 서리 결정 안쪽에 검은 핏줄 무늬가 번지기 시작. 바이저 오염과 같은 무늬로, "추위가 곧 기운"이라는 게 드러남
0% 직전 서리가 화면의 3분의 1 가까이 덮고 결정이 빠르게 자람
횃불을 들면 서리가 녹듯 가장자리부터 빠르게 물러남

10/28

1. 전체 라운드 흐름
   맵은 4개의 구역으로 나눠짐. 분위기는 라운드가 지날수록 점점 어두워짐.
   1라운드: 두 구역 열림. 모닥불 2개. 적은 Real에만 존재.
   2라운드: 세 구역 열림. 모닥불 3개, 이제부터 적이 두 차원에 모두 존재.
   3라운드: 이제부터 네 구역 모두 열림. 모닥불 3개. 적의 무리가 늘어남.
   4라운드: 모닥불 4개.
   5라운드: 플레이어가 Fake에 갇힘.

아직 열리지 않은 구역은 불로도 걷히지 않는 두꺼운 덤불로 막는다. 라운드가 지나면 그쪽이 열리겠다 라는 기대를 갖게 한다.

2. Fake 차원
   땅과 전체 지형은 두 차원이 공유한다. 두 차원이 다른 점은, Real에서는 막힌 것이 보이고 Fake에서는 연결이 보인다의 차이다.
   길을 막는 것: Real에서는 오염된 덤불 장벽이고, Fake에서는 오염된 웅덩이나 쓰러진 나무 등이다.
   모닥불: Real에서는 장벽 안쪽에 있어 다가갈 수 없고 연기만 보인다. Fake에서는 다가갈 수 있다.
   뿌리: Real에서는 보이지 않고, Fake에서는 바닥을 따라 검은 뿌리가 보여 어느 모닥불이 어느 장벽과 이어져 있는지 알 수 있다.
   랜드마크: Real에서는 원래 모습이고, Fake에서는 같은 자리에 있지만 오염되거나 불탄 모습이다.
   적: Real에서는 무리지어 순찰하고, Fake에서는 각자 움직이며 불이 켜지는 즉시 달려간다.

전시용 설정
볼륨: 라운드 1개, 구역 2개, 모닥불 2개, Real에 2~3마리의 적 + Fake에 1~2마리의 적
야외 모드: 소음이 많은 환경에서 플레이할 것을 고려하여, 플레이어가 들을 만한 소리 정보를 시각 정보로 치환하는 모드. 설정에서 켜고 끄기 가능

전시판에 넣으면 좋은 것
자동 복귀: 클리어하거나 게임 오버가 되면 몇 초 뒤 자동으로 타이틀로 돌아가요. 한동안 아무 입력이 없을 때도 타이틀로 돌아가요. 다음 관람객이 바로 시작할 수 있어요.
발표자용 숨김 키: 교수님이나 심사위원에게 보여 줄 때 다른 라운드로 바로 넘어가는 키를 하나 두세요. 관람객은 모르게요.
대기 화면 (선택): 아무도 플레이하지 않을 때 AI 시각화 모드를 켠 채 적들이 혼자 순찰하는 화면을 띄워 두면, 지나가는 사람에게 GOAP가 무엇을 하는지 보여 줄 수 있어요.

고쳐야 할 점
Real 적 포효 반경이 200m로 들어가 있어요. GameTuning으로 옮길 때 "프리팹에 저장된 실제 값을 그대로 옮겼다"고 했는데, 200은 제가 테스트용으로 잠깐 넓혀 보라고 한 값이에요. 지금 상태면 Real 무리 3마리가 들킬 때마다 맵 전체의 Real 적이 몰려와요. 40으로 되돌리세요.
횃불을 든 채로만 불을 켤 수 있게 해야 해요.
설계에서 달라진 점

계획은 "길목을 막는 장벽을 불로 걷어 새 길을 연다"였는데, 구현은 모닥불을 두른 18m 고리가 됐고, 뿌리는 "어느 불이 어느 장벽을 여는지"가 아니라 **”불에서 출구까지의 길잡이"**가 됐어요. 맵에는 관문이 없고, 열린 숲에 고리와 웅덩이가 흩어져 있는 형태예요.

제 판단은 이대로 가도 된다예요.

남은 3주 안에 관문 구조로 다시 짜는 건 위험해요.
고리가 열리면 그 안쪽 Real 칸이 열리니, 4번 알림만 붙이면 "열린 고리 안을 가로지르는 지름길로 경로가 바뀌는" 장면은 나와요.
Fake 적이 켜진 불로 몰려가고, 불붙는 소리가 두 차원에 퍼지는 것까지 들어가서, "불을 켤수록 위험해진다"는 긴장은 충분히 살아 있어요.

10/30
차원을 '현실'과 '재의 차원'으로 명명
적이 몰려다니는 것을 수정. 너무 몰려다니면 재미가 없으니 그러지 않도록

출구 형태 변경 :
숲 가장자리에 좁은 길이 나 있고 그 끝을 검게 엉긴 오염 덤불이 아치 모양으로 막고 있다. 틈새로 바깥의 불빛이 새어 나온다.
불을 켤 때마다 덤불의 일부가 타서 사라지고 새어나오는 빛이 조금씩 밝아진다. < 이건 굳이 구현 안 해도 될듯?
다 켜면, 덤불이 완전히 타서 길이 열리고 바깥에서 빛이 새어나온다.
재의 차원으로 들어오면, "각 모닥불에서 출발한 뿌리가 모두 이 덤불로 모여 들어가는 모습"이 연출된다. 