namespace ARTD
{
    /// <summary>陣営。味方(壁・大砲・兵士・タワー)と敵。</summary>
    public enum Team
    {
        Ally,
        Enemy,
    }

    /// <summary>ターゲットの種類。敵の狙う優先度の判定に使う。</summary>
    public enum TargetKind
    {
        Unit,       // 兵士・敵キャラ
        Structure,  // 壁・大砲・兵士スポナー
        Core,       // 守るべきタワー
    }

    public enum GameState
    {
        Setup,     // 配置フェーズ（シャッターで布陣を固定）
        Playing,   // 敵が湧いている
        GameOver,  // タワー破壊
        Victory,   // 全ウェーブ撃破
    }

    public static class TeamUtil
    {
        public static bool IsHostile(Team a, Team b) => a != b;
        public static Team Opponent(Team t) => t == Team.Ally ? Team.Enemy : Team.Ally;
    }
}
