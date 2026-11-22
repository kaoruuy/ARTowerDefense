# AR Tower Defense（文房具タワーディフェンス）

## フォルダ
| 場所 | 中身 |
|---|---|
| `Assets/Scripts/TowerDefense` | ランタイムスクリプト（namespace `ARTD`） |
| `Assets/Scripts/TowerDefense/Editor` | プレハブ自動生成・シーンセットアップ（メニュー **Tools > AR Tower Defense**） |
| `Assets/Prefabs/TowerDefense` | Systems / Placeables / Units / Projectiles / Effects / UI のプレハブ |
| `Assets/Data/TowerDefense` | ステータス（ScriptableObject）とウェーブ定義 |
| `Assets/Art/TowerDefense/Materials` | 仮モデル用マテリアル |

## シーンの組み立て方
1. シーンに ARCamera（Vuforia）と、各文房具の ImageTarget を置く。
2. **Tools > AR Tower Defense > 2. Setup Current Scene** を実行すると、次のことを自動でやる。
   - `GameManager`、`EnemySpawner`、`GameUI`、`EventSystem` のプレハブを置く。
   - ImageTarget の名前やデータセット名（pencase / pencil / eraser / pen）を見て、
     `MarkerPlacer` を付け、対応する Placeable プレハブを子に置く。
   - もともとあった子オブジェクトは消さずに、非アクティブにするだけ。
3. 手動でやる場合は、ImageTarget に `MarkerPlacer` を付けて、`Placeable Prefab` に
   `Prefabs/TowerDefense/Placeables/*.prefab` を入れる（子に置いておいてもよい）。

## 遊び方
- マーカーを映すと、文房具の上にオブジェクトが表示される。
- **Enter キー / シャッターボタン**を押すと、その時点で認識しているオブジェクトがその位置に固定され、マーカーから切り離される。
- 筆箱（タワー）を固定したあとで **スタート（Space）** を押すと、タワーの周りから敵が湧く。
- タワーが壊れたらゲームオーバー。全ウェーブを倒したら勝ち。**R / リトライ**でやり直す。
- タワーを固定した面が「机の面」になり、それ以降に固定するオブジェクトやキャラクターは、この面に揃えて配置される。

## 調整できる項目
| 対象 | どこで | 主な項目 |
|---|---|---|
| 壁(鉛筆) / タワー(筆箱) の耐久値 | `Data/Wall_Stats`, `Tower_Stats` | maxHealth |
| 大砲(ボールペン) | `Data/Cannon_Stats` | HP, 射程, 発射間隔, ダメージ, 弾速, 爆発半径, 旋回速度 |
| 兵士スポナー(消しゴム) | `Data/EraserSpawner_Stats` | HP, 出現間隔, 同時最大数, 守備範囲, せり上がり時間 |
| 兵士 / 敵 | `Data/Soldier_Stats`, `Enemy_*_Stats` | HP, 速度, 攻撃力, 攻撃間隔, 射程, 索敵距離, 体の半径, スコア |
| ウェーブ | `Data/Waves_Default` | 湧く半径, ウェーブごとの敵の種類・数・間隔, エンドレス |
| マーカー | ImageTarget の `MarkerPlacer` | 固定できる回数（0=無制限）, EXTENDED_TRACKED を認識中とみなすか |
| ゲーム進行 | `GameManager` | 自動開始, 開始キー, リトライキー |
| シャッター | `GameManager` の `ShutterController` | キー, 効果音 |

## 元のモデルを使う（現在の yamada_test の状態）
**Tools > AR Tower Defense > 3. Use Original Marker Children As Objects** を実行すると、ImageTarget の下にもともと置いてあった子オブジェクトを、そのまま本体として使う形に変換できる。
- 元の子オブジェクトを包むルート（Placeable、Health、各機能、HP バー、当たり判定）を作り、`Placeables/*.prefab` として保存する。
- 生成していた仮オブジェクトは削除する。
- 現在の対応は、鉛筆 = Cube、ペン = Prop_Cannon（`YawPivot` の下）、筆箱 = Prop_Chest_Gold、消しゴム = Characters_Henry。
- 兵士は `SpawnPoint`（Henry の前）から出てくる。
- Placeables は `Rebuild` を実行しても上書きされない。

## マーカーのプレハブ（最新版 = ARgame シーン）
- `Prefabs/TowerDefense/Markers/{pen,pencil,pencase,eraser}_image.prefab` は、ImageTarget（Vuforia の設定、`MarkerPlacer`、文房具オブジェクト）をまとめたプレハブ。
- `Prefabs/TowerDefense/Systems/ARCamera.prefab` は、Vuforia の AR カメラ。
- 別のシーンに持っていくときは、以下を個別にドラッグする。セットのプレハブは作っていない。
  - ARCamera
  - GameManager、EnemySpawner、EventSystem、UI/GameUI
  - Markers の4つ
- **Tools > AR Tower Defense > 4. Make Marker Prefabs** を実行すると、今開いているシーンのマーカーからプレハブを作り直せる（既存のプレハブには Apply される）。

## 差し替え
- **モデル**: 各プレハブの `Model` 子オブジェクトの中身を差し替える。当たり判定はルートの Collider で調整する。
  大砲だけは、回転する部分が `YawPivot` / `PitchPivot`、弾が出る位置が `Muzzle`（CannonTurret で参照）。
- **アニメーション**: Animator を付けると、パラメータが存在する場合だけ自動で送る。
  - キャラ: `Speed`(float), `Attack`, `Die`(trigger)
  - 大砲: `Fire`
  - スポナー: `Spawn`
  - タワー: `Damaged`
  - パラメータ名は Inspector で変更できる。
- **エフェクト**: `Health`（被弾 / 破壊）、`CannonTurret`（マズルフラッシュ）、`Projectile`（着弾）、
  `Placeable`（固定）、`SoldierSpawner` / `EnemySpawner`（出現）のプレハブ欄に入れる。
  自分で消えないエフェクト（パーティクルなど）は、3秒以上経つと自動で削除される。`TimedDestroy` を付ければ時間を指定できる。
- **弾**: `CannonTurret.projectilePrefab` に入れる。Projectile では、追尾の ON/OFF、当たり判定の半径、着弾エフェクトを設定できる。
- **イベント**: `onPlaced`, `onDeath`, `onFire`, `onSpawn`, `onShutter`, `onGameOver` などの UnityEvent に、音や演出をつなげられる。

## 注意
- 大きさの単位は、Vuforia マーカーの幅 = 100 を基準にしている。マーカーのサイズを変えたら、Stats の距離や速度も合わせて変更する。
- タグ、レイヤー、ProjectSettings は一切変更していない（索敵は `Health.All` で行う）。
- `Rebuild Prefabs` を実行するとプレハブが作り直されるので、モデルを差し替えたあとは実行しないこと。Data とマテリアルは上書きされない。

## 更新（トラップドア・兵士・丸太・敵の種類・範囲表示）
未適用ならスクリプトのコンパイル後に自動で1回実行される（手動: **Tools > AR Tower Defense > 5. Update Trapdoor + Soldier (Henry) + Enemy (Sharky) + Long Log**）。実行すると、Prefab が次のように更新される（何度実行しても同じ結果）。
- **消しゴム（Eraser_SoldierSpawner）**: 見た目を PirateKit の Trapdoor に変更。黒い穴 `Hole` と、蝶番 `DoorHinge` の下に扉 `Trapdoor`。
  兵士を出すときは「扉が開く → 兵士が穴からせり上がる → 扉が閉じる」。角度・時間は `SoldierSpawner` の「トラップドア」欄。
- **兵士（Soldier）**: 見た目を Henry に変更（`Animations/Soldier_Henry.controller`）。
- **敵（Enemy_Basic / Enemy_Heavy）**: 見た目を Sharky に変更（`Animations/Enemy_Sharky.controller`）。Heavy は大きめ。倒れるアニメーションの後に消える。
- **丸太（Pencil_Wall）**: `Model/Stretch` で横に 2.5 倍。HP バーは満タンでも常に表示。

スクリプト側の変更（Prefab の更新なしで有効）
- **兵士の徘徊**: トラップドアを中心に `EraserSpawner_Stats.wanderRadius`（既定 60）以内をランダムに歩き回り、近くの敵（`detectRange`、守備範囲 `guardRadius` 内）を追いかける。
- **敵の2タイプ**: 生成時にランダムで「壁を壊す型」か「壁を避ける型（体が青い）」になる。割合は `EnemySpawner.avoiderChance`。
  避ける型は左右に回り込み、`detourGiveUpTime` 秒通れなければ壁を壊す。
- **大砲の射程表示**: `CannonStats.range`（150 に変更）の円を地面に表示（`CannonTurret` の「射程の表示」）。
- **敵の出現円**: タワーを中心に `Waves_Default.spawnRadius`（550 に変更）の円を表示。`spawnRadiusJitter` を 0 にして、敵は円周上から出る。
- **盤（宝箱マーカー基準）**: `GameManager` が宝箱のマーカーに追従する `Board` を作り、固定したオブジェクト・敵・兵士をすべてその子にする。
  小さな揺れは無視し（`boardDeadZone` / `boardDeadZoneAngle`）、動いたときはなめらかに追う（`boardFollowSpeed`）。宝箱を見失うと一時停止。シャッターは配置フェーズのみ。
- **浮いて見えない工夫**: 足元に丸い影（`GroundShadow`）。見た目を少し机に沈める（`Placeable.modelSink` / `CharacterUnit.modelSink`）。
- **敵の狙い**: 宝箱を最優先。すぐ近く（`engageRange`）の兵士とだけ戦い、壁は進路をふさいだときだけ壊す／避ける。
- **難易度**: `Waves_Default` を5ウェーブ・計130体に増加。Enemy_Basic の速さ 30、攻撃力 10。
- **トラップドア**: 直径 24（Henry 1人分）。
