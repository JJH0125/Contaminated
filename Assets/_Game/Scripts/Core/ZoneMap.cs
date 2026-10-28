using UnityEngine;

namespace Squad
{
    /// <summary>
    /// 맵을 가운데 봉우리를 중심으로 부채꼴 4개 구역으로 나눈다. 맵은 모든 라운드가 같고, 열린 구역의 수만 늘어난다.
    ///
    ///            북(3)
    ///        ＼        ／
    ///   서(2)    봉우리    동(1)
    ///        ／        ＼
    ///         남(0, 출구)
    ///
    /// 구역 번호가 곧 열리는 순서다. 열린 구역이 2개면 남·동, 3개면 남·동·서, 4개면 전부.
    /// 경계는 봉우리 꼭대기에서 대각선 네 방향으로 뻗는다. 열린 구역과 닫힌 구역 사이의 경계에만 덤불 벽을 세운다.
    /// </summary>
    public static class ZoneMap
    {
        public const int Count = 4;
        public static readonly string[] Names = { "남", "동", "서", "북" };

        /// 경계선: (각도, 한쪽 구역, 다른 쪽 구역). 각도는 북쪽(+Z)이 0도, 동쪽(+X)이 90도다.
        public static readonly (float angle, int a, int b)[] Boundaries =
        {
            (135f, 0, 1),   // 남 | 동
            (-135f, 0, 2),  // 남 | 서
            (45f, 1, 3),    // 동 | 북
            (-45f, 2, 3),   // 서 | 북
        };

        /// <summary>position이 어느 구역에 있는가. center는 지형 한가운데(봉우리).</summary>
        public static int ZoneOf(Vector3 position, Vector3 center)
        {
            float angle = Mathf.Atan2(position.x - center.x, position.z - center.z) * Mathf.Rad2Deg;
            if (angle >= 135f || angle < -135f)
                return 0;
            if (angle >= 45f)
                return 1;
            if (angle < -45f)
                return 2;
            return 3;
        }

        /// <summary>그 자리가 열린 구역에 있는가.</summary>
        public static bool IsOpen(Vector3 position, Vector3 center, int openZones)
            => ZoneOf(position, center) < openZones;

        /// <summary>각도(도) 쪽을 향하는 평면 방향.</summary>
        public static Vector3 Direction(float angle)
        {
            float rad = angle * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));
        }
    }
}
