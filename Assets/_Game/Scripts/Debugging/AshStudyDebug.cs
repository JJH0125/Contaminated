using System.Collections.Generic;
using UnityEngine;

namespace Squad
{
    /// <summary>
    /// 디버그용. 플레이 중에 F1을 누르면 재의 차원(Fake)에 시간 제한 없이 머물며 재의 적을 가까이서 살펴볼 수 있다.
    /// 다시 누르면 끝나고 Real로 돌아온다.
    ///
    /// 관찰하는 동안에는
    ///   - Fake로 넘어가고, 8초가 지나도 튕겨 나오지 않는다. 차원 이동 키(F)도 막는다
    ///   - 원래 있던 Fake 적은 잠시 숨기고, 플레이어 둘레 25~35m에 새 재의 적 3마리를 놓는다
    ///     (먼 곳에 흩어져 있는 적을 끌어오면 걸어가던 길이 꼬이므로, 새로 만들어 처음부터 판단하게 한다)
    ///   - 적의 AI는 평소대로 움직인다. 들키면 발각 반응을, 잡히면 잡을 때의 연출을 그대로 본다
    /// 끝나면 새로 놓은 적은 지우고, 숨긴 적을 되살린다. 잡혀서 라운드가 끝나면 Stage와 함께 정리된다.
    ///
    /// 씬이나 프리팹에 붙일 필요가 없다. 게임이 시작될 때 스스로 만들어진다.
    /// 필요 없어지면 이 파일만 지우면 된다.
    /// </summary>
    public class AshStudyDebug : MonoBehaviour
    {
        // 관찰을 켜고 끄는 키
        private const KeyCode ToggleKey = KeyCode.F1;
        // 새로 놓을 재의 적의 수
        private const int EnemyCount = 3;
        // 플레이어에게서 적을 놓을 거리(m). 처음부터 보이면 흩어진 모습부터 볼 수 없으므로 조금 떨어뜨린다.
        private const float MinDistance = 25f;
        private const float MaxDistance = 35f;
        private const int MaxTries = 30;

        /// 지금 관찰 중인가.
        public static bool Studying { get; private set; }

        // 관찰을 시작한 라운드의 차원 컨트롤러. Stage와 함께 사라지면 라운드가 끝난 것이다.
        private DimensionController _controller;
        private readonly List<GameObject> _hidden = new();
        private readonly List<GameObject> _spawned = new();

        // 게임이 시작되고 첫 씬이 열린 직후에 Unity가 한 번 불러 준다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            Studying = false;
            var debugObject = new GameObject("Ash Study Debug");
            debugObject.AddComponent<AshStudyDebug>();
            // Stage가 라운드마다 바뀌어도 이 오브젝트는 남아 있어야 한다.
            DontDestroyOnLoad(debugObject);
        }

        private void Update()
        {
            // 잡혀서 라운드가 끝났으면 적과 컨트롤러가 Stage와 함께 사라졌다. 기억만 비운다.
            if (Studying && _controller == null)
                Forget();

            if (!Input.GetKeyDown(ToggleKey))
                return;

            if (Studying)
                EndStudy();
            else
                BeginStudy();
        }

        private void BeginStudy()
        {
            DimensionController controller = DimensionController.Instance;
            if (controller == null || controller.player == null ||
                GameManager.Instance == null || GameManager.Instance.CurrentState != GameState.Playing)
            {
                Debug.Log("[AshStudyDebug] 플레이 중인 라운드가 없어서 관찰할 수 없습니다.");
                return;
            }

            // 원래 있던 Fake 적은 숨기고, 그중 하나를 견본으로 삼는다. (꺼 둔 견본도 찾는다)
            HorrorChaserAgent template = null;
            foreach (HorrorChaserAgent enemy in FindObjectsOfType<HorrorChaserAgent>(true))
            {
                if (!enemy.TryGetComponent(out DimensionMember member) || member.Dimension != Dimension.Fake)
                    continue;

                template ??= enemy;
                if (enemy.gameObject.activeSelf)
                {
                    enemy.gameObject.SetActive(false);
                    _hidden.Add(enemy.gameObject);
                }
            }

            if (template == null)
            {
                Debug.Log("[AshStudyDebug] 이 라운드에는 재의 적 견본이 없어서 관찰할 수 없습니다.");
                return;
            }

            _controller = controller;
            Studying = true;
            controller.ShiftTo(Dimension.Fake);
            controller.StayInFake = true;

            Vector3 center = controller.player.transform.position;
            var pathfinder = FindObjectOfType<Astar3D.Pathfinder>();
            float firstAngle = Random.value * 360f;
            for (int i = 0; i < EnemyCount; i++)
                _spawned.Add(Spawn(template, i, FindSpot(center, firstAngle + i * 360f / EnemyCount, pathfinder), center));

            Debug.Log($"[AshStudyDebug] 재의 차원 관찰 시작. 재의 적 {EnemyCount}마리를 25~35m 둘레에 놓았습니다. F1을 다시 누르면 끝납니다.");
        }

        private void EndStudy()
        {
            foreach (GameObject spawned in _spawned)
                if (spawned != null)
                    Destroy(spawned);
            foreach (GameObject hidden in _hidden)
                if (hidden != null)
                    hidden.SetActive(true);

            if (_controller != null)
            {
                _controller.StayInFake = false;
                _controller.ShiftTo(Dimension.Real);
            }

            Forget();
            Debug.Log("[AshStudyDebug] 관찰 끝.");
        }

        private void Forget()
        {
            Studying = false;
            _controller = null;
            _spawned.Clear();
            _hidden.Clear();
        }

        // 견본을 복제해 spot에 플레이어를 바라보게 세운다.
        // 견본은 꺼져 있으므로 복제본도 꺼진 채 만들어져, 켜기 전에 정리할 수 있다.
        private static GameObject Spawn(HorrorChaserAgent template, int index, Vector3 spot, Vector3 lookAt)
        {
            GameObject copy = Instantiate(template.gameObject, template.transform.parent);
            copy.name = $"{template.name} (관찰 {index + 1})";

            // 견본이 이미 만든 재의 몸은 복제본에 그대로 따라오므로 지운다. 켜질 때 AshEnemyBody가 새로 만든다.
            Transform oldBody = copy.transform.Find("Ash Body");
            if (oldBody != null)
                DestroyImmediate(oldBody.gameObject);

            Vector3 toPlayer = lookAt - spot;
            toPlayer.y = 0f;
            copy.transform.SetPositionAndRotation(spot,
                toPlayer.sqrMagnitude > 0.01f ? Quaternion.LookRotation(toPlayer, Vector3.up) : Quaternion.identity);
            copy.SetActive(true);

            // 꺼 둔 견본(라운드 구성에 쓰이지 않은 것)에는 재의 몸이 아직 없다.
            if (copy.GetComponent<AshEnemyBody>() == null)
                copy.AddComponent<AshEnemyBody>();
            return copy;
        }

        // 플레이어에게서 angle 쪽으로 25~35m 떨어진, Fake에서 걸을 수 있는 자리. 못 찾으면 마지막 후보를 쓴다.
        private static Vector3 FindSpot(Vector3 center, float angle, Astar3D.Pathfinder pathfinder)
        {
            Vector3 spot = center;
            for (int i = 0; i < MaxTries; i++)
            {
                spot = center + ZoneMap.Direction(angle + Random.Range(-40f, 40f)) * Random.Range(MinDistance, MaxDistance);
                if (pathfinder == null || pathfinder.IsWalkable(spot, Dimension.Fake))
                    break;
            }

            Terrain terrain = Terrain.activeTerrain;
            spot.y = terrain != null
                ? terrain.SampleHeight(spot) + terrain.transform.position.y + GameTuning.Instance.fakeEnemy.heightAboveGround
                : center.y;
            return spot;
        }
    }
}
