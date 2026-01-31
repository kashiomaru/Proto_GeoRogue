# Proto_GeoRogue

Unityで開発中のGeoRogueプロトタイププロジェクトです。

## 概要

このプロジェクトは、大量のオブジェクトを効率的に処理するための最適化技術を検証・実装するためのプロトタイプです。

## 技術スタック

- **Unity**: 2022.3以降
- **Universal Render Pipeline (URP)**: 17.3.0
- **Jobs System**: Unity Jobs Systemを使用した並列処理
- **Burst Compiler**: 高パフォーマンスなネイティブコード生成

## 現在の実装

現在のプロジェクトでは、以下の技術を使用しています：

- **TransformAccessArray + IJobParallelForTransform**: 大量のオブジェクトの移動処理を並列化
- **Burst Compile**: Jobの高速化
- **NativeArray**: メモリ効率的なデータ管理

## TODO

### 最適化関連

- [ ] **ダメージテキストの描画最適化**（保留）
  - 現状：UI（TMP）のプールで Instantiate 済みオブジェクトを再利用
  - 検討案：敵・ジェム・弾と同様に、データ駆動＋ワールド空間ビルボード＋数字テクスチャアトラス＋`DrawMeshInstanced` で一括描画
  - 必要なもの：数字 0–9 のアトラス、インスタンスごとの UV・アルファ用シェーダ、桁ごとのインスタンス描画ロジック

### 機能追加

- [ ] 弾の衝突判定とダメージシステム
- [ ] ステージシステム（敵強化）
- [ ] 敵バリエーション
- [ ] ボスバリエーション
- [ ] アップグレードバリエーション
- [ ] ゲームモード：デフェンス
- [ ] 敵の出現演出、消える演出
- [ ] プレイヤーHP回復（アイテム or アップグレード）
- [ ] クリティカルヒット処理
- [ ] ジェム全吸引（ボス遷移前？）
- [ ] ボス生成時にボスをプレイヤー方向に向ける

### パフォーマンス改善

- [ ] オブジェクトプーリングの実装
- [ ] BulletPool、GemPool対応
- [ ] LOD（Level of Detail）システムの導入
- [ ] 空間分割（Spatial Partitioning）による衝突判定の最適化

### その他

- [ ] プロファイリングとベンチマークの追加
- [ ] ドキュメントの充実

## DrawMeshInstancedによる最適化について

### 現在の実装との比較

**現在の実装（TransformAccessArray + GameObject）:**
- 各オブジェクトが個別のGameObjectとして存在
- Transformの更新にCPUリソースを消費
- 描画コール数 = オブジェクト数（例：3000個 = 3000回の描画コール）

**DrawMeshInstancedによる最適化後:**
- メッシュとマテリアルを共有
- 位置・回転・スケール情報を配列で管理
- 描画コール数 = 1回（例：3000個 = 1回の描画コール）
- GPUインスタンシングにより、GPU側で効率的に処理

### 実装のポイント

1. **データ構造の設計**
   ```csharp
   NativeArray<Matrix4x4> matrices; // または float4x4
   NativeArray<float3> positions;
   NativeArray<quaternion> rotations;
   ```

2. **描画処理**
   ```csharp
   Graphics.DrawMeshInstanced(
       mesh, 
       0, 
       material, 
       matrices, 
       count,
       materialPropertyBlock
   );
   ```

3. **更新処理**
   - Job Systemを使用して位置・回転の更新を並列化
   - 更新結果をMatrix4x4に変換して描画用配列に反映

4. **注意点**
   - 個別のGameObjectが必要な場合（衝突判定、個別のコンポーネントなど）は別途管理が必要
   - インスタンス数の上限（通常1023個、Indirect版は制限なし）に注意
   - 動的なオブジェクトの追加・削除の処理を考慮

## セットアップ

1. Unity Hubからプロジェクトを開く
2. 必要なパッケージが自動的にインポートされます
3. `Assets/Scenes/SampleScene.unity`を開いて実行

## ライセンス

このプロジェクトはプロトタイプ段階です。
