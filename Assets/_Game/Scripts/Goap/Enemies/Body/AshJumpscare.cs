using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Squad
{
    /// <summary>
    /// 재의 적에게 잡힌 순간의 연출. Real 적은 달려들어 잡지만, 재의 적은 흩어졌다가 플레이어를 감싸고 눈앞에서 다시 형체를 이룬다.
    /// 소리, 카메라, 재의 움직임, 화면 가장자리 오염, 횃불을 이 코루틴 하나에서 시간 순서대로 움직인다.
    /// 단계마다 걸리는 시간은 GameTuning > Ash Enemy의 숫자만 바꿔 맞춘다. (기본값 합계 1.5초)
    ///   1. 정지   (0.2초)  모든 소리가 끊기고 시간이 멈춘다
    ///   2. 흩어짐 (0.3초)  재의 적이 한 번에 터지듯 흩어진다
    ///   3. 감싸기 (0.5초)  흩어진 재가 플레이어 둘레를 돌며 모여들고, 화면 가장자리부터 오염이 빠르게 차오른다.
    ///                      쿼터뷰로는 감싸는 것이 작게 보이므로, 여기서부터 카메라를 플레이어 쪽으로 확 당긴다
    ///   4. 얼굴   (0.3초)  카메라 바로 앞에 흰 결정이 모여 얼굴 자리를 만들고, 횃불 빛에 한 번 번쩍인다
    ///   5. 꺼짐   (0.2초)  횃불이 꺼지고 화면이 검게 잠긴 뒤 게임 오버로 넘어간다
    ///
    /// 전시장에서는 여러 번 반복해서 보게 되므로, 직접 연속으로 당해 보고 길거나 지루한 단계의 시간을 줄인다.
    ///
    /// 씬 구성:
    ///   붙일 필요가 없다. AshEnemyBody가 스스로 붙이고, 잡는 행동(ReachPlayer)이 Jumpscare 대신 이것을 부른다.
    /// </summary>
    [RequireComponent(typeof(AshEnemyBody))]
    public class AshJumpscare : MonoBehaviour
    {
        // 감싸는 힘이 미치는 거리(m)와, 둘레를 도는 재를 붙잡아 두는 저항
        private const float WrapReach = 30f;
        private const float WrapDrag = 1.2f;

        private AshEnemyBody _body;
        private AudioSource _source;

        private static AshEnemyTuning Tuning => GameTuning.Instance.ashEnemy;

        /// <summary>
        /// 연출을 시작한다. 이미 잡혔거나 플레이 중이 아니면 아무 일도 하지 않는다.
        /// </summary>
        public void Play(Transform player)
        {
            if (!GameManager.Instance.BeginCaught())
                return;

            _body = GetComponent<AshEnemyBody>();
            StartCoroutine(PlayRoutine(player));
        }

        private IEnumerator PlayRoutine(Transform player)
        {
            AshEnemyTuning tuning = Tuning;

            // 연출 중에 다시 계획을 세워 걸어가지 않도록 AI를 끄고, 다른 적은 화면에 끼어들지 않게 모두 끈다.
            HorrorChaserAgent agent = GetComponent<HorrorChaserAgent>();
            if (agent != null)
                agent.enabled = false;
            foreach (HorrorChaserAgent other in FindObjectsOfType<HorrorChaserAgent>())
                if (other.gameObject != gameObject)
                    other.gameObject.SetActive(false);

            // Real 적의 연출과 달리 플레이어는 남겨 둔다. 재가 그 둘레를 감싸야 하므로 움직임만 막는다.
            Player mover = player.GetComponent<Player>();
            if (mover != null)
                mover.MovementLocked = true;
            TorchHolder torch = player.GetComponent<TorchHolder>();
            FireSource torchFire = torch != null ? torch.TorchFire : null;

            _body.BeginScripted();

            // ── 1. 정지: 모든 소리가 끊기고 화면이 멈춘 듯한 순간 ──
            AudioListener.pause = true;
            Time.timeScale = 0f;
            yield return new WaitForSecondsRealtime(tuning.freezeTime);
            Time.timeScale = 1f;

            // ── 2. 흩어짐 ──
            PlaySound(AshSounds.Burst);
            _body.PlayAnimation(tuning.embraceAnimation, true);
            _body.Scatter();
            yield return new WaitForSecondsRealtime(tuning.scatterTime);

            // ── 3. 감싸기 + 4. 얼굴 (카메라는 두 단계에 걸쳐 플레이어 쪽으로 당겨 둔다) ──
            Camera cam = Camera.main;
            Follow follow = cam != null ? cam.GetComponent<Follow>() : null;
            Vector3 focus = player.position + Vector3.up * tuning.cameraFocusHeight;
            Vector3 cameraFrom = cam != null ? cam.transform.position : focus;
            Quaternion cameraRotation = cam != null ? cam.transform.rotation : Quaternion.identity;
            Vector3 cameraTo = focus - cameraRotation * Vector3.forward * tuning.cameraPullDistance;
            float fieldOfView = cam != null ? cam.fieldOfView : 60f;

            _body.Pull(focus, tuning.wrapPull, tuning.wrapSwirl, tuning.wrapInner, WrapReach, WrapDrag);
            float sfx = SfxPlayer.Instance != null ? SfxPlayer.Instance.Volume : 0.5f;
            float wrapEnd = tuning.wrapTime;
            float faceEnd = wrapEnd + tuning.faceTime;
            bool faceStarted = false;
            float flashedAt = -1f;

            for (float t = 0f; t < faceEnd; t += Time.unscaledDeltaTime)
            {
                // 카메라: 처음 잠깐 사이에 확 당기고, 그 자리에 머문다.
                float pull = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / Mathf.Max(tuning.cameraPullTime, 0.01f)), 3f);
                Vector3 cameraPosition = Vector3.Lerp(cameraFrom, cameraTo, pull);
                if (follow != null)
                    follow.BeginCloseUp(cameraPosition, cameraRotation, fieldOfView);
                else if (cam != null)
                    cam.transform.SetPositionAndRotation(cameraPosition, cameraRotation);

                if (t < wrapEnd)
                {
                    float wrap = t / Mathf.Max(wrapEnd, 0.01f);
                    ContaminationEdgeEffect.Surge = wrap;
                    _body.EmitAround(focus, tuning.wrapRadius,
                                     Mathf.CeilToInt(tuning.wrapEmitRate * Time.unscaledDeltaTime),
                                     tuning.wrapSwirl * tuning.wrapRadius * 0.5f);
                    _body.SetWind(tuning.scareVolume * sfx * Mathf.Lerp(0.4f, 1f, wrap), Mathf.Lerp(300f, 1100f, wrap), 1f, 0.3f);
                }
                else
                {
                    float face = (t - wrapEnd) / Mathf.Max(tuning.faceTime, 0.01f);
                    if (!faceStarted)
                    {
                        faceStarted = true;
                        ContaminationEdgeEffect.Surge = 1f;
                        _body.BeginFace();
                    }

                    Transform view = cam != null ? cam.transform : transform;
                    _body.SetFace(view.position + view.forward * tuning.faceDistance, view.rotation, tuning.faceSize,
                                  Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(face / 0.6f)));

                    // 결정이 다 모일 즈음 횃불 빛에 한 번 번쩍인다.
                    if (flashedAt < 0f && face >= 0.5f)
                    {
                        flashedAt = t;
                        _body.Flash(1f);
                        PlaySound(AshSounds.Shatter);
                    }
                    if (torchFire != null)
                        torchFire.Strength = flashedAt < 0f ? 1f
                            : 1f + (tuning.torchFlash - 1f) * Mathf.Exp(-(t - flashedAt) / 0.08f);
                }

                yield return null;
            }

            // ── 5. 꺼짐: 횃불이 꺼지고 화면이 검게 잠긴다 ──
            _body.SetWind(0f, 300f, 0f, 0f);
            _body.ReleasePull();
            if (torch != null)
                torch.PutOut();
            Image blackout = CreateBlackout();
            for (float t = 0f; t < tuning.blackoutTime; t += Time.unscaledDeltaTime)
            {
                blackout.color = new Color(0f, 0f, 0f, Mathf.Clamp01(t / Mathf.Max(tuning.blackoutTime, 0.01f)));
                yield return null;
            }
            blackout.color = Color.black;

            // 카메라와 화면 효과는 Stage와 함께 사라지지 않으므로, 다음 라운드를 위해 직접 되돌린다.
            ContaminationEdgeEffect.Surge = 0f;
            if (follow != null)
                follow.EndCloseUp();
            if (_source != null)
                _source.Stop();

            GameManager.Instance.GameOver();
            // 게임 오버 화면을 가리지 않도록 바로 지운다. (같은 프레임이라 그 사이 게임 화면이 비치지 않는다)
            Destroy(blackout.canvas.gameObject);
        }

        // 일시정지된 소리(AudioListener.pause) 사이에서도 이 연출의 소리만 들리게 한다.
        private void PlaySound(AudioClip clip)
        {
            if (_source == null)
            {
                _source = gameObject.AddComponent<AudioSource>();
                _source.playOnAwake = false;
                _source.spatialBlend = 0f;
                _source.ignoreListenerPause = true;
            }

            float sfx = SfxPlayer.Instance != null ? SfxPlayer.Instance.Volume : 0.5f;
            _source.PlayOneShot(clip, Tuning.scareVolume * sfx);
        }

        // 화면 전체를 덮는 검은 막. 이 적의 자식으로 두므로 게임 오버 때 Stage와 함께 사라진다.
        private Image CreateBlackout()
        {
            var canvasObject = new GameObject("Ash Blackout", typeof(Canvas));
            canvasObject.transform.SetParent(transform, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;

            var imageObject = new GameObject("Black", typeof(RectTransform));
            RectTransform rect = imageObject.GetComponent<RectTransform>();
            rect.SetParent(canvasObject.transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            Image image = imageObject.AddComponent<Image>();
            image.color = Color.clear;
            image.raycastTarget = false;
            return image;
        }
    }
}
