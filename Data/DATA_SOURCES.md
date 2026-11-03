# 数据来源与更新入口

## 客户端数据

物品、任务、收藏品、职业、地图等基础数据来自用户安装的《FINAL FANTASY XIV》客户端。

### 藏宝图等级简称

藏宝图选择器的 `G1` 至 `G18` 及隐藏地图 `G1` 根据日文客户端 `Item.Name` 中的等级简称显示，通过 [FFCafe XIVAPI v2](https://xivapi-v2.xivcdn.com/api/sheet/Item?language=ja&fields=Name&rows=6688,6689,6690,6691,6692,8156,12241,12242,12243,17835,17836,26744,26745,36611,36612,39591,43556,43557,46185)核对同一物品 ID。

## LuminaSupplemental 补充数据

以下 CSV 原样取自 Critical-Impact 的 [LuminaSupplemental](https://github.com/Critical-Impact/LuminaSupplemental) 项目，由 `LuminaSupplemental.Excel 5.1.4` 包提供，固定提交为 `e9bf1495bf8588d2cf21441ef03f0d9dfe0f790a`：

- `ItemPatch.csv`、`ItemSupplement.csv`、`StoreItem.csv`、`BGMOrchestrion.csv`。
- `DungeonBoss.csv`、`DungeonChest.csv`、`DungeonBossDrop.csv`、`DungeonBossChest.csv`、`DungeonChestItem.csv`。

更新入口：`tools/game-data/update-supplemental-data.ps1`。

## 数据更新入口

正式资源位置、分组、来源版本与更新入口登记在 `resource-manifest.json`。客户端补充表位于 `Resources/ClientSupplemental`，收录资源位于 `Resources/Collections`；逻辑文件名与远端数据包保持一致。

- 自动更新工作流：本体仓库 `.github/workflows/update-community-data.yml`。
- 数据包生成：`tools/game-data/build-community-data.mjs`。
- MessagePack 运行包生成：`lib/TreeTools/TreeTools.csproj` 的 `TreeToolsGameDataTool=true` 构建模式，入口源码为 `lib/TreeTools/Tools/GameData/Program.cs`。通过工具模式的 `TreeTools.dll` 执行 `generate --data-directory <Common/Data> --output-directory <仓库外包目录>`；`verify` 校验包和输入对应关系，`export` 将包解码为维护用 JSON。原始 JSON/CSV 仍是维护输入，运行包按物品目录、Wiki 来源和副本来源分别生成，嵌入索引记录输入与产物摘要。
- 离线核验：`tools/audit-game-data.ps1`，读取本地客户端和仓库静态资源，支持 `-Source` 分源与 `-Resume` 续跑；材料关系核验使用 `tools/game-data/audit-catalog-data.py`。报告和客户端快照输出至仓库外，逐项保留证据范围与未核实项。
- 运行包独立核验：上述入口使用 `-Source RuntimePackages -RuntimePackageDirectory <包目录> -GameDataToolPath <生成器DLL>`，先解码运行包，再由 `audit-runtime-packages.mjs` 独立读取原始 JSON/CSV，比较全部字段、十进制数值、缺省状态、数组顺序和关联索引。转换保留不代表获取条件及坐标已经获得独立业务核实。
- 各数据源的更新脚本见下文。

## 最终幻想 XIV 中文维基

`tools/game-data/update-wiki-item-sources.ps1 -SaveSnapshotDirectory <仓库外目录>` 保存完整原始分页；`-SnapshotDirectory` 可离线重新生成。生成文件可传给 `tools/audit-game-data.ps1 -WikiReferencePath` 做逐项一致性比对。Wiki 副本编号属于 `InstanceContent`，读取时转换为任务搜索器编号；住宅地图 `-1` 和工房地图 `-2` 表示未指定实例，商城的 64 位业务编号属于外部编号体系，均不作为客户端行 ID 使用。原始编号保留在下载快照中。

`WikiItemSources.json` 及部分特殊装备、收藏品和容器奖励补充，来源于[最终幻想 XIV 中文维基](https://ff14.huijiwiki.com/)贡献者维护的数据。批量接口为 `https://ff14.huijiwiki.com/api/rest_v1/namespace/data`。

Omni 对数据进行字段筛选、格式转换、分类、去重和补充。

主要补充来源包括[特殊装备](https://ff14.huijiwiki.com/wiki/特殊装备)、[蛮神发光武器](https://ff14.huijiwiki.com/wiki/蛮神发光武器)、[制作采集特殊工具](https://ff14.huijiwiki.com/wiki/制作采集特殊工具)、[魔光键影宝箱](https://ff14.huijiwiki.com/wiki/物品:魔光键影宝箱)及[魔兽图鉴](https://ff14.huijiwiki.com/wiki/魔兽图鉴)页面。

`RelicMaterialSources.csv` 补充特殊武器材料的任务条件、掉落与检品途径，参考[义军武器](https://ff14.huijiwiki.com/wiki/义军武器)、[幻境武器](https://ff14.huijiwiki.com/wiki/幻境武器)、[天钢工具](https://ff14.huijiwiki.com/wiki/天钢工具)、[天上福地云冠群岛](https://ff14.huijiwiki.com/wiki/天上福地云冠群岛)及对应物品、任务页面。

更新入口：`tools/game-data/update-wiki-item-sources.ps1`、`tools/game-data/update-collection-resources.mjs`。

## 其他社区数据

特殊武器的系列、阶段及职业映射维护于 `Data/Catalogs/Collections/RelicWeaponCatalog.cs`。物品 ID、图标和可用职业以客户端 `Item`、`ClassJobCategory` 为准，制作武器结合 `Recipe.ItemResult` 与材料关系核对；不同的单手、双手武器分别记录。复制品按原品逐一关联，覆盖同阶段的普通、哑光版本及骑士盾；持有数量取原品与关联复制品的最大值。复制品名称、职业和模型字段用于离线核对，运行时按物品 ID 读取映射。

目录交叉核对来源为 [SQUARE ENIX 艾欧泽亚数据库](https://na.finalfantasyxiv.com/lodestone/playguide/db/)、[FFCafe XIVAPI v2](https://xivapi-v2.xivcdn.com/api)、[FFXIV Collect 特殊武器接口](https://ffxivcollect.com/api/relics)及 [FFXIV Console Games Wiki 蛮神发光武器目录](https://ffxiv.consolegameswiki.com/wiki/Extreme_Trial_Glamour_Weapons)。官方物品与配方示例为[圆桌之夺命镰](https://na.finalfantasyxiv.com/lodestone/playguide/db/item/3c40c47c825/)及其[制作配方](https://na.finalfantasyxiv.com/lodestone/playguide/db/recipe/b087b5c9b9d/)。更新目录时按实际客户端版本核对条目及职业，并结合配方检查后补职业与同系列武器变体；成就关联数据使用下表的独立更新入口。

| 数据 | 来源 | 更新入口 |
| --- | --- | --- |
| `FishingSources.json` | [鱼糕](https://fish.ffmomola.com)公开前端数据 | `tools/game-data/update-fishing-sources.mjs` |
| `BlueSpells.csv` | [青魔法来源查询](https://bluemagic.badend.cn/)公开技能及获取途径；攻略文本保留来源站原文 | `tools/game-data/update-collection-resources.mjs` |
| `RelicItemIdToAchievement.csv` | FFXIV Collect 的[特殊武器](https://ffxivcollect.com/api/relics)与[成就](https://ffxivcollect.com/api/achievements)公开接口 | `tools/game-data/update-collection-resources.mjs` |
| `EquipmentSourceSupplement.csv` | GarlandTools 的公开物品端点 `https://garlandtools.org/db/doc/item/en/3/{ItemId}.json` | `tools/game-data/update-equipment-source-supplement.mjs` |

`EquipmentSourceSupplement.meta.json` 保存资源原始 URL 与 SHA-256。

获取流程补充还参考 [FFXIV Collect](https://ffxivcollect.com/)、[Gamer Escape](https://ffxiv.gamerescape.com/wiki/Researchingway/Purchase_a_Cosmic_Tool_Replica)及 FFXIV Console Games Wiki 的[装备](https://ffxiv.consolegameswiki.com/wiki/Ardent_Accessories)和[任务](https://ffxiv.consolegameswiki.com/wiki/Dressed_to_Deceive)资料。

特殊武器材料流程还参考 FFXIV Console Games Wiki 的[义军武器](https://ffxiv.consolegameswiki.com/wiki/Resistance_Weapons)、[禁地兵装](https://ffxiv.consolegameswiki.com/wiki/Eurekan_Weapons)、[幻境武器](https://ffxiv.consolegameswiki.com/wiki/Phantom_Weapons)及其各阶段任务资料，以及 [AkhMorning 的义军武器指南](https://www.akhmorning.com/resources/bozjan-southern-front/relic-weapons/)。

## 官方玩法、商品与活动资料

职业上限参考官方[驯兽师指南](https://na.finalfantasyxiv.com/jobguide/beastmaster/)和[青魔法师指南](https://na.finalfantasyxiv.com/jobguide/bluemage/)；魔兽图鉴补丁归类参考 [7.5 专题页](https://na.finalfantasyxiv.com/dawntrail/patch_7_5/)。

商品与活动来源补充参考 SQUARE ENIX 官方音乐商品页面、[Twitch 活动公告](https://na.finalfantasyxiv.com/lodestone/topics/detail/fad06b11fd789fce573492001f47b6605f94c95b)、[原声集实体特典](https://na.finalfantasyxiv.com/lodestone/topics/detail/6f6882bc8f027103d44d16a00bc7fc1d2078f3eb)、[画集实体特典](https://na.finalfantasyxiv.com/lodestone/topics/detail/39afebffa618e55f5f7514e446a9cc41209fc72a)、[永结同心商品说明](https://store.finalfantasyxiv.com/ffxivstore/en-gb/product/5)、[国服水晶冲突赛季奖励公告](https://actff1.web.sdo.com/project/ffcrystranking/reward21.html)及[北美粉丝节艺术比赛规则](https://fanfest-na.finalfantasyxiv.com/2026/en-us/news/art-contest)。

乐谱商品的官方来源：

- [The Best Compilation Album](https://na.finalfantasyxiv.com/blog/002521.html)、[Trial By Shadow](https://www.jp.square-enix.com/music/sem/page/ff14/primals/en/music/SQEX-20058.html)、[Journeys](https://na.finalfantasyxiv.com/lodestone/topics/detail/3b5a216316846eb1e90daf43401b420ea1549727)。
- [SCIONS & SINNERS](https://na.finalfantasyxiv.com/blog/003103.html)、[Beyond the Shadow](https://na.store.square-enix-games.com/the-primals-live-in-japan---beyond-the-shadow-_blu-ray_)、[Eorzean Symphony Vol. 3](https://www.jp.square-enix.com/music/sem/page/ff14/concert/2022/bdm/en/bonus/)。
- [Forge Ahead](https://www.jp.square-enix.com/music/sem/page/ff14/ForgeAhead/en/)、[And Back Again](https://www.jp.square-enix.com/music/sem/page/ff14/AndBackAgain/en/)、[Darkest Before Dawn](https://www.jp.square-enix.com/music/sem/page/ff14/primals/dbdBD/en/)。
- [Dark Decades Tour BUDOKAN](https://na.finalfantasyxiv.com/lodestone/topics/detail/01fc53dfcfcc7b2232a62225d639a819caf9074c)、[Eorzean Symphony Vol. 4](https://na.finalfantasyxiv.com/lodestone/topics/detail/a68d2d124ab9ea9829eff1df1987ad015482bd53)。

旧版采集工具商店来源参考[官方 1.19 NPC 商店说明](https://forum.square-enix.com/ffxiv/threads/24818-Changes-Planned-for-Patch-1.19/page2)，装备产出调整参考[官方 5.3 更新说明](https://eu.finalfantasyxiv.com/lodestone/topics/detail/c2f8c5ba14d9cfaa09d5aab63c5c2da260eae21a)。

## 青魔法师配装模型

配装评分参考 [Allagan Studies 青魔伤害文档](https://docs.google.com/document/d/1-NBJDkyl8h_UXFcOba-CrPPcFXZrDs2QXX53pSS3QJc/edit)与 [Blue Academy 所链接的模拟表](https://docs.google.com/spreadsheets/d/1IZBK7MMMuS4KABnWxDCUNrPRAf_hzUz89BmqYxjpMMA/edit)。队伍职能加成参考[官方 5.0 更新说明](https://na.finalfantasyxiv.com/lodestone/topics/detail/330f2b280067d69d85b17831c66712a499e97484)。

## 公开招募共享数据

公开招募条目来源于在线 Omni 客户端收到的游戏列表。

## 品牌图标

- Discord：[官方品牌素材](https://discord.com/branding)中的 Discord Symbol Color，使用 Blurple 版本。
- GitHub：[官方 Logo 素材](https://brand.github.com/foundations/logo)中的 GitHub Invertocat，使用黑色和白色版本。
- 爱发电：[官方应用页面](https://afdian.com/app)使用的 [152 像素应用图标](https://static.afdiancdn.com/static/img/icons/apple-touch-icon-152x152.png)。
- Omni Info：用户提供的原始图标。
