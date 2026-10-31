using UnityEngine;

namespace Squad
{
    /// <summary>
    /// 적의 얼굴에 빛나는 눈 두 개를 단다. Real 차원의 적에게만 붙인다.
    ///   - 같은 모델을 쓰는 두 차원의 적을 멀리서도 구분할 수 있다 (눈이 빛나면 Real)
    ///   - 어두운 숲에서 적이 어디에 있고 어느 쪽을 보고 있는지 눈빛으로 읽힌다
    ///
    /// 눈은 머리를 움직이는 뼈의 자식으로 만들어서, 걷거나 공격할 때 얼굴을 따라 움직이게 한다.
    ///
    /// 눈의 자리는 "모델 기준 좌표(오른쪽, 위, 앞)"로 적는다. 뼈 기준 좌표는 축이 제멋대로 돌아가 있어서
    /// 숫자만 보고는 어디인지 알 수 없기 때문이다. 모델 기준 좌표를 뼈 기준 좌표로 바꾸는 일은,
    /// 메시가 뼈마다 갖고 있는 변환 행렬(bindpose)이 해 준다. 이 행렬은 원래 "기본 자세의 꼭짓점이
    /// 이 뼈에서 보면 어디에 있는가"를 구하는 데 쓰이므로, 눈의 자리에도 그대로 쓸 수 있다.
    ///
    /// 눈의 자리를 맞추려면 플레이 중에 값을 바꿔 보면 된다. 바꾼 값은 바로 반영된다.
    /// (플레이 중에 바꾼 값은 저장되지 않으므로, 맞춘 값을 적어 두었다가 프리팹에 넣는다)
    ///
    /// 씬 구성:
    ///   적 오브젝트(DimensionMember가 붙은 것)에 붙인다. 모델은 그 자식으로 둔다.
    /// </summary>
    public class EnemyEyes : MonoBehaviour
    {
        [Header("■ 필수 연결 — 비워두면 에러")]
        [Tooltip("눈에 입힐 재질. 스스로 빛나는(Emission) 재질이어야 어두운 곳에서 보인다")]
        [SerializeField] private Material eyeMaterial;

        [Header("○ 튜닝 값 — 자유롭게 조절")]
        [Tooltip("머리를 움직이는 뼈의 이름. 눈이 이 뼈를 따라 움직인다")]
        [SerializeField] private string headBoneName = "Biped_Skeleton_Chest1";
        [Tooltip("두 눈의 한가운데. 모델 기준 좌표 (오른쪽, 위, 앞). 발이 (0, 0, 0)이다")]
        [SerializeField] private Vector3 eyeCenter = new Vector3(0.15f, 12.7f, 1.65f);
        [Tooltip("두 눈 사이의 거리(m)")]
        [SerializeField] private float eyeSpacing = 0.6f;
        [Tooltip("눈 하나의 지름(m)")]
        [SerializeField] private float eyeSize = 0.28f;

        // 몸의 Renderer. 차원에 따라 몸이 숨겨질 때 눈도 함께 숨기기 위해 붙잡아 둔다.
        private SkinnedMeshRenderer _body;
        private Transform _head;
        // 모델 기준 좌표를 머리뼈 기준 좌표로 바꾸는 행렬
        private Matrix4x4 _modelToHead;
        private readonly Transform[] _eyes = new Transform[2];
        private readonly Renderer[] _eyeRenderers = new Renderer[2];

        private void Start()
        {
            _body = GetComponentInChildren<SkinnedMeshRenderer>();
            if (_body == null || !FindHead())
            {
                Debug.LogWarning($"[EnemyEyes] {name}: 머리뼈 '{headBoneName}'를 찾지 못해 눈을 만들지 않았습니다.", this);
                enabled = false;
                return;
            }

            for (int i = 0; i < _eyes.Length; i++)
            {
                GameObject eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                eye.name = i == 0 ? "Eye L" : "Eye R";
                // 구를 만들면 Collider가 함께 붙는다. 눈이 무엇을 막거나 시야를 가리면 안 되므로 지운다.
                Destroy(eye.GetComponent<Collider>());

                _eyes[i] = eye.transform;
                _eyes[i].SetParent(_head, false);
                _eyeRenderers[i] = eye.GetComponent<Renderer>();
                _eyeRenderers[i].sharedMaterial = eyeMaterial;
                // 작은 눈이 그림자를 드리울 필요는 없다.
                _eyeRenderers[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        // 뼈 목록에서 머리뼈를 찾고, 그 뼈의 변환 행렬을 꺼낸다.
        private bool FindHead()
        {
            Transform[] bones = _body.bones;
            Matrix4x4[] bindposes = _body.sharedMesh.bindposes;

            for (int i = 0; i < bones.Length && i < bindposes.Length; i++)
            {
                if (bones[i] == null || bones[i].name != headBoneName)
                    continue;

                _head = bones[i];
                _modelToHead = bindposes[i];
                return true;
            }

            return false;
        }

        // 애니메이션이 뼈를 움직인 뒤에 맞춰야 하므로 LateUpdate에서 한다.
        private void LateUpdate()
        {
            // 뼈의 크기 배율이 1이 아니어도 눈이 정해 둔 크기로 보이도록 나눠 준다.
            float boneScale = _head.lossyScale.x;
            Vector3 scale = Vector3.one * (boneScale > 0f ? eyeSize / boneScale : eyeSize);

            for (int i = 0; i < _eyes.Length; i++)
            {
                // 왼쪽 눈은 가운데에서 왼쪽으로, 오른쪽 눈은 오른쪽으로 절반씩 벌린다.
                float side = i == 0 ? -0.5f : 0.5f;
                Vector3 modelPosition = eyeCenter + Vector3.right * (eyeSpacing * side);

                _eyes[i].localPosition = _modelToHead.MultiplyPoint3x4(modelPosition);
                _eyes[i].localScale = scale;
                // 플레이어가 다른 차원에 있으면 몸이 숨겨진다. 눈만 떠 있지 않도록 함께 숨긴다.
                _eyeRenderers[i].enabled = _body.enabled;
            }
        }
    }
}
