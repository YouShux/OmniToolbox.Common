# 数据来源与许可声明

## 最终幻想XIV中文维基物品数据

OmniToolbox 的 `WikiItemSources.json` 来源于“最终幻想XIV中文维基”贡献者共同维护的物品数据：

- 网站：https://ff14.huijiwiki.com/
- 批量数据接口：https://ff14.huijiwiki.com/api/rest_v1/namespace/data

除另有声明的内容外，原始数据采用 [CC BY-NC-SA 3.0](https://creativecommons.org/licenses/by-nc-sa/3.0/deed.zh-hans) 许可。

OmniToolbox 对原始数据进行了字段筛选、格式转换、分类、去重和补充处理。由该数据生成的派生数据文件同样按照 CC BY-NC-SA 3.0 提供。

2026-09-10 全量抓取的物品快照包含 51,227 件物品，其中 44,350 件有获取来源，去重后共 27,217 条来源，版本标记最高为 7.56。更新使用 `tools/update-wiki-item-sources.ps1`，强制读取最新页面并校验分页数量、物品 ID 和来源类型。同期鱼糕补充数据因上游资源结构变化未更新，保留已有 `FishingSources.json`。

## 职业数据与配装等级

物品职业筛选读取客户端 `ClassJob.UIPriority`、`JobIndex` 与 `DohDolJobIndex`；职业攻击修正读取 `PrimaryStat` 对应的属性修正字段。2026-09-10 通过 EXDViewer 核对 `ClassJob:43` 为驯兽师，主属性为力量、修正为 110；`ItemUICategory:113` 为单手斧。当前 Omen 的 `IsClassJobIn` 已覆盖该职业。

在线交叉核对请求：[ClassJob 36/43](https://xivapi-v2.xivcdn.com/api/sheet/ClassJob?language=chs&fields=Name,Abbreviation,IsLimitedJob,JobIndex,Role,PrimaryStat,ModifierStrength,ModifierIntelligence&rows=36,43)。响应 `version=2026071600010000`、`schema=exdschema@2:rev:83e965d091116f895d5b17573cc5d12909a5f407`。在线 `ItemUICategory:113` 名称仍为空，以目标客户端为准；本次 EXDViewer 未返回版本号。

BIS 使用集中定义的特职上限：驯兽师 50、青魔法师 80，依据[官方驯兽师指南](https://na.finalfantasyxiv.com/jobguide/beastmaster/)和[官方青魔法师指南](https://na.finalfantasyxiv.com/jobguide/bluemage/)。已检查的 FFCS/Omen 未提供离线按任意职业查询上限的接口，`PlayerState.MaxLevel` 属于当前角色状态。驯兽师支持本地计算与保存；外部 ffxiv-gearing v5 分享协议没有特职编码，维持原有职业编码表。

青魔法师配装采用独立的直伤估算模型，依据 [Allagan Studies 青魔伤害文档](https://docs.google.com/document/d/1-NBJDkyl8h_UXFcOba-CrPPcFXZrDs2QXX53pSS3QJc/edit)及 [Blue Academy 所链接的 DPS 模拟表](https://docs.google.com/spreadsheets/d/1IZBK7MMMuS4KABnWxDCUNrPRAf_hzUz89BmqYxjpMMA/edit)。2026-09-10 读取该表的 `Tables` 页，使用前两列的装备智力与额外武器伤害断点。原伤害文档标注 6.38；模拟表将 1260/1290 智力断点标为已验证，930 至 1230 的断点仍属于待验证区间。因此此实现是社区模型估算，不能作为当前客户端逐点实测证明。

模型以 100 威力直伤换算每威力伤害期望，默认输出拟态增加 20% 暴击率与直击率、纯青魔队 1% 主属性加成。队伍加成依据[官方 5.0 更新说明](https://na.finalfantasyxiv.com/lodestone/topics/detail/330f2b280067d69d85b17831c66712a499e97484)：每种职能提供 1%，青魔属于魔法远程职能。装备智力单独扣除基础属性和食物，额外武器伤害不受种族、食物及队伍增益影响。断点表在装备智力 1290 时结束，更高输入沿用最后一档 105，不推导未知档位。持续伤害、完整循环、额外技能增益及治疗/坦克拟态不在此评分范围；不同技能组合的最优装备可能不同。客户端表和当前 FFCS/Omen 未找到可直接复用的青魔离线伤害计算接口。

## 青魔法获取数据

`BlueSpells.csv` 来源于[青魔法来源查询](https://bluemagic.badend.cn/)。更新脚本读取网站公开前端资源中的技能与获取途径数据，过滤站点标记为失效的途径后生成本地快照。

## 特殊武器数据

特殊武器的系列、阶段与物品列表参考“最终幻想XIV中文维基”的[特殊装备](https://ff14.huijiwiki.com/wiki/%E7%89%B9%E6%AE%8A%E8%A3%85%E5%A4%87)、[蛮神发光武器](https://ff14.huijiwiki.com/wiki/%E8%9B%AE%E7%A5%9E%E5%8F%91%E5%85%89%E6%AD%A6%E5%99%A8)与[制作采集特殊工具](https://ff14.huijiwiki.com/wiki/%E5%88%B6%E4%BD%9C%E9%87%87%E9%9B%86%E7%89%B9%E6%AE%8A%E5%B7%A5%E5%85%B7)页面。

`RelicItemIdToAchievement.csv` 来源于 FFXIV Collect 的公开接口：

- 特殊武器物品与成就 ID：https://ffxivcollect.com/api/relics
- 成就名称：https://ffxivcollect.com/api/achievements

上述两份资源可通过 `node tools/update-collection-resources.mjs` 更新。插件运行时只读取本地快照，不会请求外部网站。

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
