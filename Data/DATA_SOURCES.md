# 数据来源与许可声明

## LuminaSupplemental 补充数据

`ItemPatch.csv`、`ItemSupplement.csv`、`StoreItem.csv`、`BGMOrchestrion.csv`、`DungeonBoss.csv`、`DungeonChest.csv`、`DungeonBossDrop.csv`、`DungeonBossChest.csv`、`DungeonChestItem.csv` 原样取自 Critical-Impact 的 [LuminaSupplemental](https://github.com/Critical-Impact/LuminaSupplemental) 项目随 `LuminaSupplemental.Excel 5.1.4` NuGet 包嵌入的资源，固定提交为 `e9bf1495bf8588d2cf21441ef03f0d9dfe0f790a`。仅携带 Omni 实际使用的九份数据，不加载或分发该库及其专用 CSV 解析依赖。

上游包附带 GNU GPL v3 许可，完整许可随资源保存在 `LuminaSupplemental.LICENSE.txt`。上游 [README](https://github.com/Critical-Impact/LuminaSupplemental/blob/e9bf1495bf8588d2cf21441ef03f0d9dfe0f790a/README.md) 说明：物品版本来自 xivapi datamining-patches，物品补充关系来自 Lodestone、Gubal、Tracky、人工维护及游戏数据，商城物品来自 Square Enix Store，副本 Boss 和掉落由人工维护，副本宝箱与宝箱物品来自 Tracky 社区采集。背景音乐与管弦乐谱映射沿用该提交生成的资源。

快照保留上游字节、表头、全部行、列与行序；运行时仅解析实际消费字段。物品版本先展开补充表的闭区间，再由现有 Wiki 数据覆盖；副本 Boss 和宝箱的重复索引保留首条，背景音乐名称按原顺序覆盖。物品补充表保留分解关系，但容器来源继续排除 `ItemSupplementSource = 1` 的分解数据。

刷新使用 `pwsh -File tools/update-supplemental-data.ps1`。脚本从本地 NuGet 缓存读取固定版本，使用 PE 元数据提取资源，不执行源程序集；可通过 `-PackageDirectory` 指定下载并解压的同版本包。脚本同时核对包版本与仓库提交并复制完整许可。更新版本时须同时审查脚本中的版本、提交、CSV 字段、上游逐行解析规则及现有资源摘要回归基线，不能只替换数据文件。

## 最终幻想XIV中文维基物品数据

OmniToolbox 的 `WikiItemSources.json` 来源于“最终幻想XIV中文维基”贡献者共同维护的物品数据：

- 网站：https://ff14.huijiwiki.com/
- 批量数据接口：https://ff14.huijiwiki.com/api/rest_v1/namespace/data

除另有声明的内容外，原始数据采用 [CC BY-NC-SA 3.0](https://creativecommons.org/licenses/by-nc-sa/3.0/deed.zh-hans) 许可。

OmniToolbox 对原始数据进行了字段筛选、格式转换、分类、去重和补充处理。由该数据生成的派生数据文件同样按照 CC BY-NC-SA 3.0 提供。

`WikiItemSources.json` 使用版本 2 结构。商店来源的 `r` 字段保存兑换材料的 `Item.RowId`，`g` 字段标记金币商店；描述与详情仅用于展示。已有快照中的兑换材料与本地国服 `2026.09.15.0000.0000` 的 `Item` 表核对，更新脚本直接保留上游结构化材料 ID 与商店类型。

2026-10-26 从上述同一 namespace API 获取完整 52 页物品快照，共 51,227 个不同物品 ID，其中 44,367 件有获取来源，去重后共 29,218 条来源，版本标记最高为 7.56。本轮通过 `tools/update-wiki-item-sources.ps1 -SnapshotDirectory` 导入完整分页快照，核对分页总数、物品数量、重复 ID 和来源类型后发布。插件运行时只读取本地快照，不会请求 Wiki；覆盖范围以该接口当次公开的数据为准，社区尚未收录的来源不在快照中。

`WikiItemSources.json` 保存物品来源。任务直接奖励的表情由客户端 `Quest.EmoteReward → Emote.UnlockLink` 关系补充；2026-10-26 在本机国服 `2026.10.06.0000.0000` 核对了 41 条任务奖励，共用解锁条件的动作继承同一来源，包括 `Emote:85/86/87 → UnlockLink:24 → Quest:66846`。季节任务不据此一律标为当前可获取。

同日另核对了不经物品、也未写入 `Quest.EmoteReward` 的直接解锁关系：[尘世幻龙坐骑](https://ff14.huijiwiki.com/wiki/尘世幻龙（坐骑）)为 `Mount:50 → Quest:67204`（振翅高飞），[尘世幻龙宠物](https://ff14.huijiwiki.com/wiki/尘世幻龙（宠物）)为 `Companion:119 → Quest:65902`（前往密约之塔）；`Emote.UnlockLink:229 → Quest:67114`（天长地久），`Emote.UnlockLink:340 → Quest:68020`（遨游大海！）。[永结同心](https://ff14.huijiwiki.com/wiki/永结同心)的标准方案也包含深情拥抱。`UnlockLink:373` 的深蹲、俯卧撑、仰卧起坐和深呼吸关联 `Achievement:1966`（可靠的队长1），条件读取客户端成就描述；[官方成就页](https://na.finalfantasyxiv.com/lodestone/playguide/db/achievement/60e9333c0fe/)确认需完成 10 次分队攻略任务。以上关系使用稳定行 ID，名称与描述读取客户端，不通过名称匹配或把无物品条目默认判为已无法获取。

`FishingSources.json` 来源于[鱼糕](https://fish.ffmomola.com)公开前端资源，由 `tools/update-fishing-sources.mjs` 生成，并与 Wiki 物品快照联合发布。2026-10-26 更新的站点版本为 `803987e`，包含 524 个钓场和 2,331 种鱼类物品；本轮钓场、鱼类及其条件与此前快照一致。插件运行时只读取本地钓鱼快照，不会请求来源站点。

钓鱼来源覆盖核对时同时读取该快照，不能仅依据 Wiki 物品接口判断有无来源。鳐鳐 `Item:46779 → FishingSpot:321` 和小甚平 `Item:51687 → FishingSpot:336` 已包含在钓鱼快照中，另以本机 `FishingSpot.Item` 核实关联，不重复添加条件不全的钓场记录。

宠物变体 `Companion:68/69/70` 共用主形态 `67` 和 `Item:6987`；`Companion:72/73/74` 共用主形态 `71` 和 `Item:6988`。变体保留各自名称、图标和展示编号，获取来源和收录状态使用共用的解锁物品与主形态。

国际服及实体特典单独标明范围：`Item:41498` 关联[官方 Twitch 订阅活动](https://na.finalfantasyxiv.com/lodestone/topics/detail/fad06b11fd789fce573492001f47b6605f94c95b)，活动期间为 2026-07-28 至 2026-08-25，兑换码不适用于国服和韩服；`Item:52654` 关联 [TRAIL TO THE HEAVENS 原声集 Blu-ray 实体特典](https://na.finalfantasyxiv.com/lodestone/topics/detail/6f6882bc8f027103d44d16a00bc7fc1d2078f3eb)，数字版不附宠物；`Item:52638` 关联[英文画集实体特典](https://na.finalfantasyxiv.com/lodestone/topics/detail/39afebffa618e55f5f7514e446a9cc41209fc72a)，官方公布发售日为 2026-12-15。后两者尚未核实国服兑换方式。资料核对日期为 2026-10-26，来源已公布不代表当前国服已经能够取得。

园艺工耳坠 `Item:47939`、渔工耳坠 `47940` 等已有客户端数据但尚未查到已开放途径的条目保留空来源，并在悬浮窗说明尚未核实；不套用同模型、相近名称、其他区服或推测中的商城来源。

## 职业数据与配装等级

物品职业筛选读取客户端 `ClassJob.UIPriority`、`JobIndex` 与 `DohDolJobIndex`；职业攻击修正读取 `PrimaryStat` 对应的属性修正字段。2026-09-10 通过 EXDViewer 核对 `ClassJob:43` 为驯兽师，主属性为力量、修正为 110；`ItemUICategory:113` 为单手斧。当前 Omen 的 `IsClassJobIn` 已覆盖该职业。

在线交叉核对请求：[ClassJob 36/43](https://xivapi-v2.xivcdn.com/api/sheet/ClassJob?language=chs&fields=Name,Abbreviation,IsLimitedJob,JobIndex,Role,PrimaryStat,ModifierStrength,ModifierIntelligence&rows=36,43)。响应 `version=2026071600010000`、`schema=exdschema@2:rev:83e965d091116f895d5b17573cc5d12909a5f407`。在线 `ItemUICategory:113` 名称仍为空，以目标客户端为准；本次 EXDViewer 未返回版本号。

BIS 使用集中定义的特职上限：驯兽师 50、青魔法师 80，依据[官方驯兽师指南](https://na.finalfantasyxiv.com/jobguide/beastmaster/)和[官方青魔法师指南](https://na.finalfantasyxiv.com/jobguide/bluemage/)。已检查的 FFCS/Omen 未提供离线按任意职业查询上限的接口，`PlayerState.MaxLevel` 属于当前角色状态。驯兽师支持本地计算与保存；外部 ffxiv-gearing v5/v6 分享协议没有特职编码，维持原有职业编码表。

青魔法师配装采用独立的直伤估算模型，依据 [Allagan Studies 青魔伤害文档](https://docs.google.com/document/d/1-NBJDkyl8h_UXFcOba-CrPPcFXZrDs2QXX53pSS3QJc/edit)及 [Blue Academy 所链接的 DPS 模拟表](https://docs.google.com/spreadsheets/d/1IZBK7MMMuS4KABnWxDCUNrPRAf_hzUz89BmqYxjpMMA/edit)。2026-09-10 读取该表的 `Tables` 页，使用前两列的装备智力与额外武器伤害断点。原伤害文档标注 6.38；模拟表将 1260/1290 智力断点标为已验证，930 至 1230 的断点仍属于待验证区间。因此此实现是社区模型估算，不能作为当前客户端逐点实测证明。

模型以 100 威力直伤换算每威力伤害期望，默认输出拟态增加 20% 暴击率与直击率、纯青魔队 1% 主属性加成。队伍加成依据[官方 5.0 更新说明](https://na.finalfantasyxiv.com/lodestone/topics/detail/330f2b280067d69d85b17831c66712a499e97484)：每种职能提供 1%，青魔属于魔法远程职能。装备智力单独扣除基础属性和食物，额外武器伤害不受种族、食物及队伍增益影响。断点表在装备智力 1290 时结束，更高输入沿用最后一档 105，不推导未知档位。持续伤害、完整循环、额外技能增益及治疗/坦克拟态不在此评分范围；不同技能组合的最优装备可能不同。客户端表和当前 FFCS/Omen 未找到可直接复用的青魔离线伤害计算接口。

## 青魔法获取数据

`BlueSpells.csv` 来源于[青魔法来源查询](https://bluemagic.badend.cn/)。更新脚本读取网站公开前端资源中的技能与获取途径数据，过滤站点标记为失效的途径后生成本地快照。

`DutyID` 与 `TerritoryID` 分别保存 `ContentFinderCondition.RowId` 和 `TerritoryType.RowId`，0 表示来源没有对应入口。地点关联与缺失坐标补全在资源生成时完成，插件运行时直接读取 ID 与坐标。2026-10-08 使用本地国服 `2026.09.15.0000.0000` 的 `ContentFinderCondition`、`TerritoryType`、`Map` 和 `PlaceName` 核对了 374 条来源；129 个副本 ID 和 37 个区域 ID 与 FFCafe 在线数据一致。在线响应版本为 `20260929-0264d14`，Schema 为 `exdschema@2:rev:e773c41a90aed788cf4c1c48469fa85618ef01fb`。更新脚本通过同一表关系生成稳定 ID，攻略文本继续保留来源站原文。

## 特殊武器数据

特殊武器的系列、阶段与物品列表参考“最终幻想XIV中文维基”的[特殊装备](https://ff14.huijiwiki.com/wiki/%E7%89%B9%E6%AE%8A%E8%A3%85%E5%A4%87)、[蛮神发光武器](https://ff14.huijiwiki.com/wiki/%E8%9B%AE%E7%A5%9E%E5%8F%91%E5%85%89%E6%AD%A6%E5%99%A8)与[制作采集特殊工具](https://ff14.huijiwiki.com/wiki/%E5%88%B6%E4%BD%9C%E9%87%87%E9%9B%86%E7%89%B9%E6%AE%8A%E5%B7%A5%E5%85%B7)页面。

`RelicItemIdToAchievement.csv` 来源于 FFXIV Collect 的公开接口：

- 特殊武器物品与成就 ID：https://ffxivcollect.com/api/relics
- 成就名称：https://ffxivcollect.com/api/achievements

上述两份资源可通过 `node tools/update-collection-resources.mjs` 更新。插件运行时只读取本地快照，不会请求外部网站。

2026-10-26 使用本机国服 `2026.10.06.0000.0000` 的客户端表补充了 Wiki 物品批量接口未记录的强化来源。宇宙工具通过 `WKSCosmoToolClass.Stages.Item` 关联 11 个职业、20 个阶段的工具，并读取 `WKSCosmoToolDataAmount.Stages.RequiredAmount` 显示研究需求；研究阶段、工具名称和物品 ID 均来自客户端。强化 NPC、地点和开放流程核对[宇宙工具](https://ff14.huijiwiki.com/wiki/宇宙工具)：憧憬湾、法恩娜行星、俄匊斯行星和奥克塞西亚行星的研明威分别负责对应阶段，领取下一阶段不要求持有上一阶段实物。宇宙工具复制品读取 `SpecialShop:1770975` 的 44 件商品及 `AchievementUnlock` 门槛，领取入口另核对 [Researchingway / Purchase a Cosmic Tool Replica](https://ffxiv.gamerescape.com/wiki/Researchingway/Purchase_a_Cosmic_Tool_Replica)。

黄道武器通过客户端 `RelicItem` 第 2 至 7 行的职业物品字段关联魂晶、魂灵、新星、镇魂、黄道和本我六个阶段，共 66 件武器与盾牌；前阶段物品由相邻行读取。各阶段任务、强化 NPC 与操作说明核对[黄道武器](https://ff14.huijiwiki.com/wiki/黄道武器)，任务名称、NPC 名称与区域关联使用本机表数据。该补充仅增加已核实的获取流程，不把骑士剑盾的材料需求按整套说明套用到每一件装备，也不改变物品可获取状态。

上述补充由客户端本地表与已核实的玩法规则生成，不写回 `WikiItemSources.json`。物品接口没有来源、搜索分类为 0 或与其他物品模型相同，均不足以证明该物品已无法获取；尚未核实具体途径的以太装备、任务临时装备和区服限定衣饰继续保留未收录来源的状态，不继承同模型物品的来源。

## 装备任务过程与历史来源补充

2026-10-26 使用本机国服 `2026.10.06.0000.0000` 核对了十件上古武器半成品。任务 `66655` 至 `66663`、`67115` 的 `QuestParams` 中，指令 `RITEM1` 分别保存对应半成品的精确物品 ID；胖嘟嘟的莫古力 `ENpcResident:1008161` 的 `ENpcBase` 直接关联这些任务，`Level:4328412` 位于黑衣森林北部林区。任务中获得及遗失后补领的规则核对[黄道武器](https://ff14.huijiwiki.com/wiki/黄道武器)流程，补领地点为北部林区 `(30.4,20.1)`。补领来源明确限制为对应任务进行中且物品遗失，不作为无条件商店来源。

`Item:2995` 风化束腰牧衣与 `Item:3306` 风化牧羊裤通过 `Quest:66110`、`Quest:67884` 的 `RITEM0`、`RITEM1` 关系补充，任务过程核对 [Dressed to Deceive](https://ffxiv.consolegameswiki.com/wiki/Dressed_to_Deceive)、[Honorable Heroes](https://ffxiv.consolegameswiki.com/wiki/Honorable_Heroes)。这两件服装在任务中作为伪装领取，展示为任务过程来源。

`EquipmentSourceSupplement.csv` 保存 GarlandTools 按精确 `Item.RowId` 提供的以太装备与副本关联。原始公开端点为 `https://garlandtools.org/db/doc/item/en/3/{ItemId}.json`。本轮逐一检查 395 个缺来源装备 ID，只有 186 件提供了明确的 `item.instances` 与对应 `partials` 迷宫挑战记录，共 230 条关系。十二个副本 ID 已与本机 `ContentFinderCondition` 的名称、`ContentType:2` 和 `Content` 关联核对。运行时使用 `Content.RowId` 作为含详情来源的副本 ID，不借用同模型或同名装备的数据。

上述副本关系展示为“历史副本掉落记录；当前是否仍掉落未核实”，不能据此承诺当前掉落。`EquipmentSourceSupplement.meta.json` 独立保存生成时间、查询数量、每份原始 JSON 的 URL 与 SHA-256；补充不写入 `WikiItemSources.json`，插件不请求 GarlandTools。生成方式为 `node tools/update-equipment-source-supplement.mjs --snapshot-directory <原始物品JSON目录>`；输入文件须以精确物品 ID 命名，脚本核对文件名、物品 ID、稀有度与实例记录，并用现有资源发布器成组发布 CSV 和元数据。没有实例关系的物品不据模型、装备等级或武器类型推导副本。

`Item:11745` 至 `11764` 的二十件炽热饰品保留历史禁书神典石兑换说明。具体系列及停用信息核对 [Ardent Accessories](https://ffxiv.consolegameswiki.com/wiki/Ardent_Accessories)；[官方 5.3 更新说明](https://eu.finalfantasyxiv.com/lodestone/topics/detail/c2f8c5ba14d9cfaa09d5aab63c5c2da260eae21a)明确说明相关同盟章、兵团章装备改为直接兑换强化版本，基础装备和强化材料不再从商店与副本取得。当前仍能把旧饰品作为材料兑换强化品，不代表当前还能获得基础饰品。

`DungeonChest.csv` 中大于等于 `100000` 的部分 `ContentFinderConditionId` 是上游的野外区域聚合编号。已追溯固定 LuminaSupplemental 提交引用的 `FFXIVGachaSpreadsheet:1e57f1dca6cd805892660bd1c5bbf835cf041890`，其生成代码在区域无副本入口时使用 `TerritoryType.RowId + 100000`，并将不同时期记录合并。这些数字不能作为副本入口；区域聚合记录中未保存的宝箱触发条件或准确宝图等级也不能从单个采集坐标推导。

本轮按插件实际来源入口纳入 Wiki、制作、容器、商城 CSV、客户端试衣商城及套装成员关系后，装备空来源从 389 件降为 171 件，与截图基线一致。剩余 161 件以太装备尚无精确的历史或当前来源记录；另有 `2533/2559/3042/8693/8694/8695/41504/47939/47940/52439` 十件物品尚未核实具体途径。未以相似装备或历史随机奖励的泛称替代精确记录，也未把空来源统一解释为无法获得。

## 誓约手镯与旧版采集工具

截图中的 `Item:8693/8694/8695` 分别为纯情、激情、钟情之誓的不可交易版本，不能直接套用商城发放的 `Item:9545/9546/9547` 方案手镯。[官方永结同心商品页](https://store.finalfantasyxiv.com/ffxivstore/en-gb/product/5)说明手镯原本可以交易，接受 The Ties that Bind 后转为不可交易；[中文 Wiki 永结同心说明](https://ff14.huijiwiki.com/wiki/永结同心)区分方案手镯与对应的已使用不可交易版本。本机各组物品的方案描述、等级与交易标志吻合，使用人工核实的精确物品对照补充流程，不冒称客户端 `Quest.Reward` 提供了转换关系。

三件成品均显示取得对应方案手镯、双方装备后组队接取 `Quest:67114` 天长地久的步骤，入口为克拉丽贝尔 `ENpcResident:1009761`，黑衣森林东部林区 `TerritoryType:152` 的 `(17.5,18.2)`。来源记录为任务中的转换步骤，不将它们当作商城直接售卖的物品，也不把方案手镯填作兑换消耗。

风化碎石锤 `Item:2533` 与风化镰刀 `Item:2559` 的历史来源核对 [1.19 官方 New NPC Vendors 列表](https://forum.square-enix.com/ffxiv/threads/24818-Changes-Planned-for-Patch-1.19/page2)中 NOC_JP 的第 15 楼。该记录精确列出 Weathered Sledgehammer 与 Weathered Scythe，由 Syneyhil（Limsa Lominsa，Fieldcraft Ward `(1,1)`）、Grillairmais（Gridania，Fieldcraft Den `(1,1)`）及 Tetetoka（Ul'dah，Fieldcraft Row `(2,1)`）销售。本机 `GilShopItem` 的 16,157 行查询未包含这两个物品。

这些坐标属于 1.x 旧地图，运行时只显示历史记录，不绑定现版同名 NPC、商店查询或导航。当前没有该商店产出不等于国际服遗留物完全不能由玩家转让，因此保留原可获取状态，不以历史记录一律改成不可获取；国服也没有 1.x 角色继承的物品。

## 乐谱商品特典来源

2026-10-26 使用本机国服 `2026.10.06.0000.0000`、项目引用的 Lumina.Excel `7.5.1` 核对了截图中 17 首空来源乐谱：`Orchestrion:278/279/305/306/406/562/563/589/641/642/691/725/726/831/832/889/890`。它们均能通过原有 `ItemAction.Action` 和 `Item.AdditionalData` 关联到正确的乐谱物品，且 `OrchestrionUiparam.OrchestrionCategory:8` 为“商城与特典”；缺失的是 Wiki 物品接口及现有商城补充表中的来源记录。`BGMOrchestrion.csv` 仅保存播放器的背景音乐关联，不作为获取方式依据。

通过 FFXIV Collect 的精确 Orchestrion ID 与物品 ID 交叉核对商品，再逐一查验官方音乐页面中的附赠曲目。来源对应如下：

- `278`：[FINAL FANTASY XIV - The Best Compilation Album](https://na.finalfantasyxiv.com/blog/002521.html)，首版附赠像素版乐谱兑换码。
- `279`：[THE PRIMALS Zepp Tour 2018 - Trial By Shadow](https://www.jp.square-enix.com/music/sem/page/ff14/primals/en/music/SQEX-20058.html)，蓝光影像特典。
- `305/306`：[Journeys](https://na.finalfantasyxiv.com/lodestone/topics/detail/3b5a216316846eb1e90daf43401b420ea1549727)，限定首版附赠 The Worm's Tail 与 eScape 的 Journeys 版本。
- `406`：[SCIONS & SINNERS](https://na.finalfantasyxiv.com/blog/003103.html)，首版附赠对应改编乐谱。
- `562/563`：[THE PRIMALS Live in Japan - Beyond the Shadow](https://na.store.square-enix-games.com/the-primals-live-in-japan---beyond-the-shadow-_blu-ray_)，附赠 Close in the Distance 与 Hic Svnt Leones 对应版本。
- `589`：[Eorzean Symphony Vol. 3](https://www.jp.square-enix.com/music/sem/page/ff14/concert/2022/bdm/en/bonus/)，附赠 Flow 管弦乐版本。
- `641/642`：[Forge Ahead](https://www.jp.square-enix.com/music/sem/page/ff14/ForgeAhead/en/)，分别为 Dedicated to Moonlight 与 In the Balance 的对应改编版本，不将同专辑的其他曲目混作解锁物品。
- `691`：[And Back Again: Live Performances from Fan Festival 2024](https://www.jp.square-enix.com/music/sem/page/ff14/AndBackAgain/en/)，附赠 Flow 对应现场版本。
- `725/726`：[THE PRIMALS Live in Japan - Darkest Before Dawn](https://www.jp.square-enix.com/music/sem/page/ff14/primals/dbdBD/en/)，附赠 Bee My Honey 与 Give It All；`726 → Item:45092` 为截图中的孤注一掷版本。
- `831/832`：[THE PRIMALS Dark Decades Tour BUDOKAN](https://na.finalfantasyxiv.com/lodestone/topics/detail/01fc53dfcfcc7b2232a62225d639a819caf9074c)，附赠 Shadowbringers 与 Ride the Rhythm 对应演出版本。
- `889/890`：[Eorzean Symphony Vol. 4](https://na.finalfantasyxiv.com/lodestone/topics/detail/a68d2d124ab9ea9829eff1df1987ad015482bd53)，附赠 Smile 与 Serenity Unending 管弦乐版本。

运行时只对已核实的 17 个稳定 ID 且原生分类匹配的条目补充商品来源，保留已有来源及可获取状态。仅指定特典批次包含兑换码，数字版音乐不因此包含兑换码；国际服唱片特典不代表国服商城已上架。页面的预售、首版或历史发售描述也不代表核查当天仍有库存或兑换码仍可使用。未核实具体商品的后续条目继续保留空来源，不用分类提示替代具体获取途径。

## 对战赛季与系列奖励来源

2026-10-26 核对本机国服 `2026.10.06.0000.0000` 与[国服水晶冲突第21赛季奖励公告](https://actff1.web.sdo.com/project/ffcrystranking/reward21.html)，将 `Item:51723` 至 `51733` 的十一件肖像教材逐项关联到该赛季的排名或段位奖励。映射按核实后的稳定物品 ID 保存，显示名称和动作类型从客户端读取。`Item:51726` 为青铜段位奖励：该赛季至少参加一次段位赛并达到青铜，赛季结束后向狼狱停船场的赛季奖励发放员领取。NPC 使用客户端 `ENpcResident:1005417`，区域为 `TerritoryType:250`，公告坐标为 `(4.9,5.7)`。段位奖励仅可在下个赛季领取，超过领取期限无法领取，季前赛也计作一个赛季；补充来源不据此把历史奖励标为当前可领取。

对战系列奖励读取本机 `PvPSeries.LevelRewards.LevelRewardItem` 与对应的正数 `LevelRewardCount`，按数组下标展示系列等级，按来源类别和入口 ID 去重追加。本机第 1 至 12 行包含 63 种有效奖励物品；第 0 行的 64 个物品槽与 64 个数量槽均为零，全部行的等级 0 物品槽也为空。系列编号来自客户端行 ID，不根据物品名称推导。系列经验、历史奖励领取与水晶冲突赛季结算分别展示，已有 Wiki 来源保留原样；后台准备后合并与同步构建使用相同的去重规则。

本机 `PvPRank` 保存对战等级经验，并不提供这些赛季教材的奖励映射。肖像教材的 `Item.AdditionalData` 为解锁位 ID，不能直接作为 `BannerCondition.RowId` 使用；第21赛季的显式来源以官方奖励公告为依据，不借用同号条件。`Item:50021` 特别奖对应 `BannerCondition:1021` 的条件类型 55，未归入对战赛季奖励；国际服 [FFXIV Collect](https://ffxivcollect.com/frames/1021) 将其精确关联到 2026 粉丝节艺术与视频比赛，[北美粉丝节官方艺术比赛规则](https://fanfest-na.finalfantasyxiv.com/2026/en-us/news/art-contest)确认优胜者获得新增肖像教材，国服具体发放渠道尚未核实。`Item:52447` 厚框护目镜不在本机对战系列奖励中，未据相邻物品推导其来源。

## 陆行鸟装甲与眼镜解锁

2026-10-26 在同一本机国服版本核对了 `BuddyEquip:1/5/9` 的 `GrandCompany` 与对应的三大国防联军。初始陆行鸟鞍在加入相应军队后自动解锁，[密林陆行鸟鞍](https://ff14.huijiwiki.com/wiki/密林陆行鸟鞍)对应双蛇党；这些条目不经过解锁物品。`BuddyEquip:13/14/15` 分别由搭档防护、进攻、治疗技能达到 10 级直接解锁，具体条件核对[陆行鸟骑士装甲](https://ff14.huijiwiki.com/wiki/陆行鸟骑士装甲)、[陆行鸟龙骑士装甲](https://ff14.huijiwiki.com/wiki/陆行鸟龙骑士装甲)和[陆行鸟白魔装甲](https://ff14.huijiwiki.com/wiki/陆行鸟白魔装甲)。上述六项补充具体条件，并修正无物品条目默认显示不可获得的问题。

眼镜解锁物品的 `ItemAction.Action:37312` 使用 `Item.AdditionalData` 指向款式中的一个 `Glasses.RowId`，不是 `ItemAction.Data[0]`。读取该行的 `Style → GlassesStyle.Glasses`，将同款全部颜色关联到同一个真实解锁物品；每个颜色保留自己的名称、图标和解锁状态，`GlassesStyle:0` 也是有效款式。黑色罩眼式墨镜 `Glasses:736 → Style:61` 与同组颜色共用 `Item:52449`。本机有效眼镜为 61 款、732 个颜色，关联修正后有来源条目从 60 个增加到 720 个，无来源从 672 个降至 12 个。

剩余 12 色 `Glasses:709–720` 共用 `Item:52447` 厚框护目镜，尚未核实已开放的具体途径。`BuddyEquip:57 → Item:20562` 东洋装甲的中文 Wiki 未记录来源，国际社区资料虽标注国服商城，但本轮未找到国服官方商品或活动依据；不把它和远东装甲等近似名称混淆。这些条目保留未核实来源说明。

肖像教材特别奖 `Item:50021` 通过 [FFXIV Collect 的精确条目](https://ffxivcollect.com/frames/1021)关联到新增的 Lauded Framer's Kit，并核对[北美 Fan Festival 2026 官方艺术比赛规则](https://fanfest-na.finalfantasyxiv.com/2026/en-us/news/art-contest)中的优胜者奖励、绑定角色及不可转让条件。插件记录其国际服历史活动来源，明确国服发放渠道尚未核实。

## 魔兽图鉴数据

名称、图标、描述和主要栖息地从当前客户端的 `XBMPet`、`Pet`、`PlaceName` 与 `ContentFinderCondition` 读取，不维护重复的名称或副本列表。2026-09-08 核对的国服客户端构建标识为 `2026.09.01.0000.0000`，`XBMPet` 有 50 条有效记录。

当前生成的 `XBMPet` 类型与该客户端的列布局不同，适配使用 Lumina `RawRow` 并检查列类型：列 0 为 `Pet.RowId`，列 4 为图标，列 6 为地点类型，列 7 为地点 ID，列 8 为描述。地点类型 1 对应 `PlaceName`，类型 2 对应 `ContentFinderCondition`。

37 种野外魔兽的 41 个参考坐标按 2026-09-10 读取的[灰机 Wiki 魔兽图鉴](https://ff14.huijiwiki.com/wiki/魔兽图鉴)核对，采用表格中的地点与坐标，包含 4、13、16、41 号的第二处坐标。物品收录列出表内全部地点；原生详情按钮仍按图鉴栖息地名称选择地点。40 号幽灵位于中拉诺西亚（20,19）。Wiki 优先列出与游戏图鉴一致、等级较低的地点，不代表全部刷新位置。

新增地点涉及 `Map.RowId` 4/15/16/17/18/20/21，分别对应 `TerritoryType.RowId` 148/134/135/137/138/140/141；已通过 FFCafe XIVAPI 与 EXDViewer 交叉核对。FFCafe 请求为 `/api/sheet/Map?language=chs&fields=TerritoryType%40as%28raw%29%2CPlaceName.Name&rows=4%2C15%2C16%2C17%2C18%2C20%2C21`，响应 `version=2026071600010000`、`schema=exdschema@2:rev:83e965d091116f895d5b17573cc5d12909a5f407`；EXDViewer 的版本未提供。运行时继续核对 `TerritoryType.Map` 与 `Map.TerritoryType`。坐标来自社区资料，尚未经游戏内逐点验证。

1 号库西的获取来源为 `Quest:71026`（驯养魔兽之人）及奖励 `Item:49805`（科纳格壶：库西），由当前客户端任务奖励和任务文本交叉确认。

版本归类 `7.56` 依据[官方 7.5 专题页的驯兽师栏目](https://na.finalfantasyxiv.com/dawntrail/patch_7_5/)，不将客户端构建标识转换为补丁号。

捕获状态从原生 `XBMMonsterNotebook` 的条目状态与全册计数同步；每页 25 个槽位从 `AtkValues[24]` 开始，步长为 8，偏移 0 为图鉴编号、1 为有效标志、2 为捕获标志、4 为图标。数据按原生类型读取，并交叉检查图标。尚未同步的条目保持未知，不计作已收录；登出时清除角色状态。捕获增量复用 Omen `LogMessageManager` 的 `LogMessage:11400`，参数 0 为 `Pet.RowId`，仅接受本玩家来源。

导航到达后的模型高亮使用 `XBMPet → Pet → PetMirage.ModelChara`，当前引用中的 `Pet.Unknown8` 对应默认 `PetMirage.RowId`。高亮同时限制目标区域、存活战斗 NPC 和原生捕获动作的目标判定；同模型对象仅作为候选，不作为物种或收录状态的证明。

本项目与最终幻想XIV中文维基、灰机 Wiki 及其贡献者不存在隶属或背书关系。

《FINAL FANTASY XIV》相关名称、商标及游戏素材的权利归 SQUARE ENIX CO., LTD. 所有。
