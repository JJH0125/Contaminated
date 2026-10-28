using System.Collections.Generic;
using UnityEngine;

namespace Squad
{
    /// <summary>
    /// 불(모닥불, 횃불)이 내는 빛. 두 가지 일을 한다.
    ///   1. 주변을 비추는 일렁이는 조명을 만든다
    ///   2. "이 불빛에 드러난 자리인가"를 다른 스크립트가 물어볼 수 있게 한다
    ///
    /// 화면을 밝게 하려고 조명을 키워도 게임 규칙이 같이 바뀌지 않도록,
    /// 조명이 닿는 거리(lightRadius)와 플레이어가 드러나는 거리(revealRadius)를 따로 둔다.
    ///
    /// 2번이 게임 규칙에 쓰인다. 적의 VisionCensor는 플레이어가 불빛 안에 있으면 더 멀리서도 알아본다.
    /// 그래서 불은 길잡이가 되는 동시에, 그 옆에 있는 플레이어를 드러내는 위험이기도 하다.
    ///
    /// 켜져 있는 모든 불이 목록에 스스로 등록하므로, 묻는 쪽은 불이 어디에 몇 개 있는지 몰라도 된다.
    ///
    /// 씬 구성:
    ///   모닥불이나 횃불 오브젝트에 붙인다. 조명은 시작할 때 스스로 만든다.
    /// </summary>
    public class FireSource : MonoBehaviour
    {
        [Header("○ 튜닝 값 — 자유롭게 조절")]
        [Tooltip("조명의 빛이 닿는 거리(m). 보이는 밝기에만 영향을 준다")]
        [SerializeField] private float lightRadius = 12f;
        [Tooltip("이 거리(m) 안에 있는 플레이어는 불빛에 드러난 것으로 본다. 적의 시야 규칙에만 쓰인다")]
        [SerializeField] private float revealRadius = 12f;
        [Tooltip("불빛의 색")]
        [SerializeField] private Color lightColor = new Color(1f, 0.55f, 0.2f);
        [Tooltip("불빛의 밝기")]
        [SerializeField] private float lightIntensity = 2.5f;
        [Tooltip("이 오브젝트의 위치를 기준으로 한 조명의 높이(m)")]
        [SerializeField] private float lightHeight = 1f;
        [Tooltip("밝기가 일렁이는 정도. 0.2면 밝기가 80~120% 사이에서 흔들린다")]
        [Range(0f, 1f)]
        [SerializeField] private float flickerAmount = 0.2f;
        [Tooltip("일렁이는 빠르기")]
        [SerializeField] private float flickerSpeed = 8f;

        // 지금 켜져 있는 모든 불
        private static readonly List<FireSource> _all = new();

        private Light _light;
        // 불마다 다르게 일렁이도록 서로 다른 값으로 시작한다.
        private float _flickerOffset;

        /// <summary>
        /// position이 어느 불빛 안에라도 들어 있는가. 높이는 보지 않고 위에서 내려다본 거리로 판단한다.
        /// </summary>
        public static bool IsLit(Vector3 position)
        {
            foreach (FireSource fire in _all)
            {
                Vector3 offset = fire.transform.position - position;
                offset.y = 0f;
                if (offset.sqrMagnitude <= fire.revealRadius * fire.revealRadius)
                    return true;
            }

            return false;
        }

        private void Awake()
        {
            var lightObject = new GameObject("Fire Light");
            lightObject.transform.SetParent(transform);
            // 부모의 크기 배율과 기울기에 상관없이 똑바로 위에 두기 위해 월드 좌표로 높이를 준다.
            lightObject.transform.position = transform.position + Vector3.up * lightHeight;

            _light = lightObject.AddComponent<Light>();
            _light.type = LightType.Point;
            _light.color = lightColor;
            _light.range = lightRadius;
            _light.intensity = lightIntensity;

            _flickerOffset = Random.value * 100f;
        }

        /// <summary>
        /// 조명만 position으로 옮기고 닿는 거리를 range로 바꾼다. 손에 든 횃불이 앞쪽을 더 비추게 할 때 쓴다.
        /// 불빛에 드러나는 범위(revealRadius)는 그대로 이 오브젝트(불)의 자리를 기준으로 잰다.
        /// </summary>
        public void PlaceLight(Vector3 position, float range)
        {
            _light.transform.position = position;
            _light.range = range;
        }

        /// <summary>조명을 원래 자리(불 바로 위)와 원래 거리(lightRadius)로 되돌린다.</summary>
        public void ResetLight()
        {
            _light.transform.position = transform.position + Vector3.up * lightHeight;
            _light.range = lightRadius;
        }

        // 꺼 두면(꺼진 모닥불) 조명도 꺼지고 불빛 목록에서도 빠진다.
        private void OnEnable()
        {
            _all.Add(this);
            if (_light != null)
                _light.enabled = true;
        }

        private void OnDisable()
        {
            _all.Remove(this);
            if (_light != null)
                _light.enabled = false;
        }

        private void Update()
        {
            // PerlinNoise는 시간에 따라 부드럽게 변하는 0~1 값이라, 뚝뚝 끊기지 않고 일렁인다.
            float noise = Mathf.PerlinNoise(Time.time * flickerSpeed, _flickerOffset);
            _light.intensity = lightIntensity * (1f + (noise - 0.5f) * 2f * flickerAmount);
        }
    }
}
