using UnityEngine;

namespace ARTD
{
    /// <summary>
    /// 筆箱 = 守るべきタワー。固定されると GameManager に登録され、
    /// ここを中心に敵が湧く。壊れたらゲームオーバー。
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class TowerCore : MonoBehaviour
    {
        [SerializeField] Animator animator;
        [SerializeField] string damagedTrigger = "Damaged";

        public Health Health { get; private set; }

        Placeable placeable;

        void Awake()
        {
            Health = GetComponent<Health>();
            placeable = GetComponent<Placeable>();
            if (placeable) placeable.onPlaced.AddListener(Register);
            Health.onDamaged.AddListener(() => AnimUtil.Trigger(animator, damagedTrigger));
        }

        void Start()
        {
            if (!placeable || placeable.IsPlaced) Register();
        }

        void Register()
        {
            if (GameManager.Instance) GameManager.Instance.RegisterTower(this);
            else Debug.LogWarning("[ARTD] シーンに GameManager がありません", this);
        }
    }
}
