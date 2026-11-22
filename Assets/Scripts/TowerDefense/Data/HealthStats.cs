using UnityEngine;

namespace ARTD
{
    /// <summary>耐久値だけを持つステータス（壁・タワーなど）。他のステータスの基底。</summary>
    [CreateAssetMenu(menuName = "AR Tower Defense/Stats/Health (Wall, Tower)", fileName = "HealthStats")]
    public class HealthStats : ScriptableObject
    {
        [Tooltip("最大耐久値 / HP")]
        [Min(1)] public float maxHealth = 100f;
    }
}
