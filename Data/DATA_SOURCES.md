# 数据来源与更新入口

## 客户端数据

物品、任务、收藏品、职业、地图等基础数据来自用户安装的《FINAL FANTASY XIV》客户端。

### 藏宝图等级简称

藏宝图选择器的 `G1` 至 `G18` 及隐藏地图 `G1` 根据日文客户端 `Item.Name` 中的等级简称显示，通过 [FFCafe XIVAPI v2](https://xivapi-v2.xivcdn.com/api/sheet/Item?language=ja&fields=Name&rows=6688,6689,6690,6691,6692,8156,12241,12242,12243,17835,17836,26744,26745,36611,36612,39591,43556,43557,46185)核对同一物品 ID。

## LuminaSupplemental 补充数据

以下 CSV 原样取自 Critical-Impact 的 [LuminaSupplemental](https://github.com/Critical-Impact/LuminaSupplemental) 项目，由 `LuminaSupplemental.Excel 5.1.4` 包提供，固定提交为 `e9bf1495bf8588d2cf21441ef03f0d9dfe0f790a`：

- `ItemPatch.csv`、`ItemSupplement.csv`、`StoreItem.csv`、`BGMOrchestrion.csv`。
- `DungeonBoss.csv`、`DungeonChest.csv`、`DungeonBossDrop.csv`、`DungeonBossChest.csv`、`DungeonChestItem.csv`。

更新入口：`tools/update-supplemental-data.ps1`。

## 数据更新入口

- 自动更新工作流：本体仓库 `.github/workflows/update-community-data.yml`。
- 数据包生成：`tools/build-community-data.mjs`。
- 各数据源的更新脚本见下文。

## 最终幻想 XIV 中文维基

`WikiItemSources.json` 及部分特殊装备、收藏品和容器奖励补充，来源于[最终幻想 XIV 中文维基](https://ff14.huijiwiki.com/)贡献者维护的数据。批量接口为 `https://ff14.huijiwiki.com/api/rest_v1/namespace/data`。

Omni 对数据进行字段筛选、格式转换、分类、去重和补充。

主要补充来源包括[特殊装备](https://ff14.huijiwiki.com/wiki/特殊装备)、[蛮神发光武器](https://ff14.huijiwiki.com/wiki/蛮神发光武器)、[制作采集特殊工具](https://ff14.huijiwiki.com/wiki/制作采集特殊工具)、[魔光键影宝箱](https://ff14.huijiwiki.com/wiki/物品:魔光键影宝箱)及[魔兽图鉴](https://ff14.huijiwiki.com/wiki/魔兽图鉴)页面。

`RelicMaterialSources.csv` 补充特殊武器材料的任务条件、掉落与检品途径，参考[义军武器](https://ff14.huijiwiki.com/wiki/义军武器)、[幻境武器](https://ff14.huijiwiki.com/wiki/幻境武器)、[天钢工具](https://ff14.huijiwiki.com/wiki/天钢工具)、[天上福地云冠群岛](https://ff14.huijiwiki.com/wiki/天上福地云冠群岛)及对应物品、任务页面。

更新入口：`tools/update-wiki-item-sources.ps1`、`tools/update-collection-resources.mjs`。

## 其他社区数据

| 数据 | 来源 | 更新入口 |
| --- | --- | --- |
| `FishingSources.json` | [鱼糕](https://fish.ffmomola.com)公开前端数据 | `tools/update-fishing-sources.mjs` |
| `BlueSpells.csv` | [青魔法来源查询](https://bluemagic.badend.cn/)公开技能及获取途径；攻略文本保留来源站原文 | `tools/update-collection-resources.mjs` |
| `RelicItemIdToAchievement.csv` | FFXIV Collect 的[特殊武器](https://ffxivcollect.com/api/relics)与[成就](https://ffxivcollect.com/api/achievements)公开接口 | `tools/update-collection-resources.mjs` |
| `EquipmentSourceSupplement.csv` | GarlandTools 的公开物品端点 `https://garlandtools.org/db/doc/item/en/3/{ItemId}.json` | `tools/update-equipment-source-supplement.mjs` |

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
