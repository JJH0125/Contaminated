using System.Collections;
using UnityEngine;

namespace Squad
{
    /// <summary>
    /// 적에게 잡힌 순간의 연출(점프스케어).
    ///   1. 적의 AI와 플레이어를 멈춘다
    ///   2. 카메라를 적의 얼굴 바로 앞으로 옮긴다 (쿼터뷰 → 1인칭 느낌)
    ///   3. 공격 애니메이션과 소리를 재생한다
    ///   4. 연출이 끝나면 게임 오버로 넘긴다
    ///
    /// 카메라 자리는 플레이어의 눈이 아니라 적을 기준으로 잡는다.
    /// 플레이어 자리에서 비추면 사이에 나무나 벽이 끼거나 적이 화면 밖에 있을 수 있지만,
    /// 적 기준으로 잡으면 항상 얼굴이 화면 정면에 온다.
    ///
    /// 구도 맞추기:
    ///   이 오브젝트를 선택하면 Scene 화면에 빨간 구(바라볼 지점)와
    ///   카메라 시야(흰 선)가 그려진다. 구가 얼굴에 오도록 값을 조절한다.
    ///
    /// 씬 구성:
    ///   HorrorChaserAgent가 붙은 적 오브젝트에 붙인다.
    ///   Animator Controller에 attackStates의 이름과 같은 상태가 있어야 한다.
    ///   이 컴포넌트가 없는 적은 연출 없이 바로 게임 오버가 된다.
    /// </summary>
    public class Jumpscare : MonoBehaviour
    {
        [Header("□ 선택 연결 — 비워두면 자동 처리")]
        [Tooltip("잡힌 순간 재생할 소리. 비워두면 소리 없이 진행한다")]
        [SerializeField] private AudioClip scareSound;
        [Tooltip("연출 중에만 켤 오브젝트 (얼굴을 비추는 조명 등). 평소에는 꺼 둔다")]
        [SerializeField] private GameObject showDuringScare;

        [Header("○ 튜닝 값 — 자유롭게 조절")]
        [Tooltip("재생할 Animator 상태 이름. 여러 개면 매번 무작위로 하나를 고른다")]
        [SerializeField] private string[] attackStates = { "Attack", "Attack Near" };
        private float duration => GameTuning.Instance.enemyBody.jumpscareDuration;
        private float faceHeight => GameTuning.Instance.enemyBody.faceHeight;
        private float cameraDistance => GameTuning.Instance.enemyBody.cameraDistance;
        private float cameraHeightOffset => GameTuning.Instance.enemyBody.cameraHeightOffset;
        private float fieldOfView => GameTuning.Instance.enemyBody.fieldOfView;

        // 얼굴 조명 — 어두운 맵에서도 얼굴이 보이도록 연출 중에만 켜진다
        private float lightIntensity => GameTuning.Instance.enemyBody.lightIntensity;
        private Color lightColor => GameTuning.Instance.enemyBody.lightColor;
        private float lightHeightOffset => GameTuning.Instance.enemyBody.lightHeightOffset;

        /// <summary>
        /// 연출을 시작한다. 이미 잡혔거나 플레이 중이 아니면 아무 일도 하지 않는다.
        /// </summary>
        public void Play(Transform player)
        {
            if (!GameManager.Instance.BeginCaught())
                return;

            StartCoroutine(PlayRoutine(player));
        }

        private IEnumerator PlayRoutine(Transform player)
        {
            // 연출 중에 적이 다시 계획을 세워 걸어가지 않도록 AI를 끈다.
            HorrorChaserAgent agent = GetComponent<HorrorChaserAgent>();
            if (agent != null)
                agent.enabled = false;

            // 적이 플레이어를 정면으로 보게 한다.
            Vector3 toPlayer = player.position - transform.position;
            toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(toPlayer, Vector3.up);

            // 플레이어를 끄면 입력이 막히고, 카메라 앞을 플레이어 모델이 가리는 일도 없다.
            player.gameObject.SetActive(false);

            // 함께 쫓아오던 다른 적들이 화면에 끼어들지 않도록, 잡은 적만 남기고 모두 끈다.
            // (Stage와 함께 사라지므로 다시 켤 필요는 없다)
            foreach (HorrorChaserAgent other in FindObjectsOfType<HorrorChaserAgent>())
                if (other.gameObject != gameObject)
                    other.gameObject.SetActive(false);

            GetCameraPose(out _, out Vector3 position, out Quaternion rotation);

            Follow follow = Camera.main != null ? Camera.main.GetComponent<Follow>() : null;
            if (follow != null)
                follow.BeginCloseUp(position, rotation, fieldOfView);

            if (lightIntensity > 0f)
                CreateFaceLight(position);

            if (showDuringScare != null)
                showDuringScare.SetActive(true);

            ChaserAnimator chaserAnimator = GetComponent<ChaserAnimator>();
            if (chaserAnimator != null && attackStates.Length > 0)
                chaserAnimator.PlayOnce(attackStates[Random.Range(0, attackStates.Length)]);

            // 위치와 상관없이 같은 크기로 들려야 하므로 2D 효과음으로 재생한다.
            if (scareSound != null && SfxPlayer.Instance != null)
                SfxPlayer.Instance.PlayUI(scareSound);

            yield return new WaitForSeconds(duration);

            // 카메라는 Stage와 함께 사라지지 않으므로, 다음 라운드를 위해 직접 되돌린다.
            if (follow != null)
                follow.EndCloseUp();

            GameManager.Instance.GameOver();
        }

        // 카메라 자리 근처에 얼굴을 비추는 조명을 만든다.
        // 구도 값(높이, 거리)을 바꿔도 조명이 따라오도록, 프리팹에 미리 두지 않고 그때 만든다.
        // 이 오브젝트의 자식으로 두므로 게임 오버 때 Stage와 함께 사라진다.
        private void CreateFaceLight(Vector3 cameraPosition)
        {
            var lightObject = new GameObject("Scare Light");
            lightObject.transform.SetParent(transform, true);
            lightObject.transform.position = cameraPosition + Vector3.up * lightHeightOffset;

            Light faceLight = lightObject.AddComponent<Light>();
            faceLight.type = LightType.Point;
            faceLight.color = lightColor;
            faceLight.intensity = lightIntensity;
            // 얼굴까지 충분히 닿도록 카메라 거리보다 넉넉하게 잡는다.
            faceLight.range = cameraDistance * 3f;
            // 조명이 여러 개 있는 맵에서도 얼굴이 뭉개지지 않게 항상 픽셀 단위로 계산한다.
            faceLight.renderMode = LightRenderMode.ForcePixel;
        }

        // 적의 얼굴 위치와, 그 앞에서 얼굴을 바라보는 카메라의 위치·방향을 계산한다.
        private void GetCameraPose(out Vector3 face, out Vector3 position, out Quaternion rotation)
        {
            face = transform.position + Vector3.up * faceHeight;
            position = face
                       + transform.forward * cameraDistance
                       + Vector3.up * cameraHeightOffset;
            rotation = Quaternion.LookRotation(face - position, Vector3.up);
        }

        /// <summary>
        /// 구도를 숫자로 맞추기 어려울 때 쓴다. 컴포넌트 이름을 우클릭하면 메뉴에 나온다.
        ///   1. 플레이 중 연출이 나오는 순간 Unity의 일시정지 버튼을 누른다
        ///   2. Main Camera를 원하는 구도가 되도록 직접 옮기고 돌린다
        ///   3. 이 메뉴를 실행하면 그 구도에 해당하는 값이 Console에 출력된다
        ///   4. 플레이를 끄고 출력된 값을 프리팹에 넣는다 (플레이 중 바꾼 값은 저장되지 않는다)
        /// 카메라가 적의 정면에 있다고 보고 계산하므로, 좌우로 비껴 있는 만큼은 반영되지 않는다.
        /// </summary>
        [ContextMenu("현재 카메라 구도를 값으로 계산")]
        private void LogPoseFromCamera()
        {
            if (Camera.main == null)
                return;

            Transform cam = Camera.main.transform;
            Vector3 toCamera = cam.position - transform.position;

            // 적의 정면 방향으로 얼마나 떨어져 있는가
            float distance = Vector3.Dot(toCamera, transform.forward);

            // 카메라가 보는 방향을 따라 적이 서 있는 곳까지 갔을 때의 높이가 곧 바라보는 지점의 높이다.
            Vector3 look = cam.forward;
            float flatLength = new Vector2(look.x, look.z).magnitude;
            float lookHeight = toCamera.y;
            if (flatLength > 0.0001f)
                lookHeight += look.y / flatLength * Mathf.Abs(distance);

            Debug.Log(
                $"[Jumpscare] Face Height: {lookHeight:0.00} / " +
                $"Camera Distance: {distance:0.00} / " +
                $"Camera Height Offset: {toCamera.y - lookHeight:0.00} / " +
                $"Field Of View: {Camera.main.fieldOfView:0}", this);
        }

        // 구도를 눈으로 보며 맞출 수 있도록, 바라볼 지점과 카메라 시야를 그린다.
        private void OnDrawGizmosSelected()
        {
            GetCameraPose(out Vector3 face, out Vector3 position, out Quaternion rotation);

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(face, 0.1f);

            Gizmos.color = Color.white;
            Gizmos.matrix = Matrix4x4.TRS(position, rotation, Vector3.one);
            Gizmos.DrawFrustum(Vector3.zero, fieldOfView, cameraDistance * 1.5f, 0.05f, 16f / 9f);
        }
    }
}
