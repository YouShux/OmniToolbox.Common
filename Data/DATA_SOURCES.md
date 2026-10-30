# 数据与图标来源及许可声明

## 客户端数据

物品、任务、收藏品、职业、地图等基础数据来自用户安装的《FINAL FANTASY XIV》客户端，通过 Lumina 和 FFXIVClientStructs 读取。相关名称、商标、文本及游戏素材的权利归 SQUARE ENIX CO., LTD. 所有。

### 藏宝图等级简称

藏宝图选择器的 `G1` 至 `G18` 及隐藏地图 `G1` 根据日文客户端 `Item.Name` 中的等级简称显示，通过 [FFCafe XIVAPI v2](https://xivapi-v2.xivcdn.com/api/sheet/Item?language=ja&fields=Name&rows=6688,6689,6690,6691,6692,8156,12241,12242,12243,17835,17836,26744,26745,36611,36612,39591,43556,43557,46185)核对同一物品 ID。特殊地图 `S1` 至 `S5` 按对应藏宝副本显示为 `G10`、`G12`、`G14`、`G15`、`G17`，与用户提供的庄子选择器分组一致；庄子参考来源为 [Join-Taoist-Zhuangzi-Repo](https://github.com/Join-Taoist/Join-Taoist-Zhuangzi-Repo) 的 `0.3.0.7`，仅参考分组关系。未定义 G 编号的地图显示 `·—`。G 编号与 `TreasureHuntRank.RowId` 不同。

## LuminaSupplemental 补充数据

以下 CSV 原样取自 Critical-Impact 的 [LuminaSupplemental](https://github.com/Critical-Impact/LuminaSupplemental) 项目，由 `LuminaSupplemental.Excel 5.1.4` 包提供，固定提交为 `e9bf1495bf8588d2cf21441ef03f0d9dfe0f790a`：

- `ItemPatch.csv`、`ItemSupplement.csv`、`StoreItem.csv`、`BGMOrchestrion.csv`。
- `DungeonBoss.csv`、`DungeonChest.csv`、`DungeonBossDrop.csv`、`DungeonBossChest.csv`、`DungeonChestItem.csv`。

上游包附带 GNU GPL v3 许可，完整许可保存在 [LuminaSupplemental.LICENSE.txt](Resources/LuminaSupplemental.LICENSE.txt)。上游数据的原始出处及贡献者见其 [README](https://github.com/Critical-Impact/LuminaSupplemental/blob/e9bf1495bf8588d2cf21441ef03f0d9dfe0f790a/README.md)。Omni 仅分发所用 CSV 与许可文件。

内置快照更新入口：`tools/update-supplemental-data.ps1`。插件启动时还会检查同一上游 `main` 中补充数据目录的最新提交，按同一提交下载上述九份 CSV，校验表头、记录及物品版本区间后，统一用于物品、收录、副本掉落与乐谱索引。沿用上游 GPL v3 许可；Wiki 逐项版本补充仍优先于 CSV。

运行时缓存位于插件配置目录 `Data/SupplementalData.json`。同一游戏数据版本下，成功检查后 3 天内复用缓存；客户端数据版本变化时立即重新检查。网络阶段总计最多等待 10 秒，失败使用上次有效缓存或内置快照，不写入游戏文件或替换插件程序集。上游尚未提供的版本与来源信息无法自动推断。

## 社区数据自动更新

`WikiItemSources.json`、`FishingSources.json`、`BlueSpells.csv`、`RelicItemIdToAchievement.csv`、`EquipmentSourceSupplement.csv` 及其 `EquipmentSourceSupplement.meta.json` 由本体仓库的 `.github/workflows/update-community-data.yml` 定时维护。工作流在北京时间 20:00 按三天周期运行轻量版本检查；定时入口每天只判断日期，未到检查日时跳过整个数据更新任务，不检出代码或请求数据源，跨月保持三天间隔；只有检测到游戏数据标识变化、需要补齐资料或存在失败来源时，才准备基准数据并启动转换器。相关转换脚本更新后触发重新同步，手动运行可选择强制同步；GitHub 调度可能延迟。Wiki、钓鱼、青魔、特殊武器、历史装备五组分别生成，某一组失败时保留该组上次有效内容，其他成功组仍可发布。失败会保留在工作流报告并使任务显示失败。

维护端使用 `tools/build-community-data.mjs --baseline-directory <Common 的 Data/Resources> --output-directory <仓库外临时目录>`。维护端先请求 FFCafe XIVAPI 的 `/api/version` 与已发布的 `community-data-state.json`，比较实际构建字符串和数据发布标识。该服务合并多个客户端的最新已处理数据，此信号不等同于用户本机国服版本或公开补丁号。稳定且无待处理任务时，只输出检查报告，不下载数据包、不检出基准数据或抓取来源。需要同步时，首次以 Common 内置快照为基准，此后读取上次有效数据包；既有发行无法读取或摘要不符时停止发布，避免降级。内容改变或首次成功初始化时，向公共 `YouShux/OmniToolbox.Common` 仓库的 `community-data` 发行上传 `community-data-<大写 SHA256>.json.gz`，旧摘要对应的数据包保留。小状态文件 `community-data-state.json` 保存游戏数据标识、压缩包 SHA-256、补齐状态与失败来源；数据未变时只发布必要的状态变更。发布时先上传新数据包，再将原状态保存为 `community-data-state.previous.json`，最后替换主状态；主状态暂时缺失时，维护端与客户端读取备用状态及其完整旧包。首次发行以草稿准备完整资源后公开，发布中断时可在后续同步重试。不改源码分支、子模块指针或插件版本号。压缩包解压后为 JSON，保留六个固定文件及其 SHA-256；只有生成时间变化时不重复发布数据包。检测到新游戏数据标识后，同步一次并在后续三天检查补齐一次，随后继续轻量探测；失败来源每三天单独重试，已成功来源不因其他来源失败而反复抓取。

部署时，将工作流提交到本体仓库默认分支，并在该仓库一次性配置 `OMNI_DATA_PUBLISH_TOKEN` Secret，发布凭据需要公共 Common 仓库 Contents 写权限，建议使用仅授权该仓库的专用 Token。Wiki 优先匿名访问；受站点保护时使用维护端 `FIRECRAWL_API_KEY` Secret，后备抓取消耗该账号额度。密钥只进入对应维护步骤，不写入数据包或客户端。普通用户无需配置密钥或运行生成脚本。

插件通过 `GameDataCache` 在构建索引前下载公开数据包，限制压缩响应及解压后体积，再校验固定文件集合、大小、哈希、格式、ID 和关联关系；缓存保存在插件配置目录 `Data/CommunityData.json`。社区包与 LuminaSupplemental 分别缓存并同时检查，各网络阶段限时 10 秒；同一客户端数据版本下，成功检查后 3 天内复用缓存，客户端数据版本变化时立即检查。社区数据先读取小状态文件，仅压缩包 SHA-256 变化时下载数据包并核对压缩包摘要；LuminaSupplemental 仅数据目录提交变化时下载补充表。断网、包损坏或格式不兼容时保留有效缓存或使用内置快照。新资料在维护端成功发布后的下一次有效启动检查生效，不在绘制期间替换正在使用的索引。

现有数据源中的新增、修订记录可自动同步；网站尚未收录的内容、网页或表结构变更、新来源类型及新增玩法逻辑仍可能需要人工核实或代码适配，不能从版本号推断。历史装备仅刷新已经核实的物品集合，保留历史关联标记，不自动推断当前掉落。

## 最终幻想 XIV 中文维基

`WikiItemSources.json` 及部分特殊装备、收藏品和容器奖励补充，来源于[最终幻想 XIV 中文维基](https://ff14.huijiwiki.com/)贡献者维护的数据。批量接口为 `https://ff14.huijiwiki.com/api/rest_v1/namespace/data`。

除另有声明的内容外，原始数据采用 [CC BY-NC-SA 3.0](https://creativecommons.org/licenses/by-nc-sa/3.0/deed.zh-hans) 许可。Omni 对数据进行字段筛选、格式转换、分类、去重和补充；由该数据生成的派生数据同样按 CC BY-NC-SA 3.0 提供。

主要补充来源包括[特殊装备](https://ff14.huijiwiki.com/wiki/特殊装备)、[蛮神发光武器](https://ff14.huijiwiki.com/wiki/蛮神发光武器)、[制作采集特殊工具](https://ff14.huijiwiki.com/wiki/制作采集特殊工具)、[魔光键影宝箱](https://ff14.huijiwiki.com/wiki/物品:魔光键影宝箱)及[魔兽图鉴](https://ff14.huijiwiki.com/wiki/魔兽图鉴)页面。

更新入口：`tools/update-wiki-item-sources.ps1`、`tools/update-collection-resources.mjs`。物品版本及来源快照接入上述自动发布管道；其余特定玩法与页面补充按现有代码维护。

## 其他社区数据

| 数据 | 来源 | 更新入口 |
| --- | --- | --- |
| `FishingSources.json` | [鱼糕](https://fish.ffmomola.com)公开前端数据 | `tools/update-fishing-sources.mjs` |
| `BlueSpells.csv` | [青魔法来源查询](https://bluemagic.badend.cn/)公开技能及获取途径；攻略文本保留来源站原文 | `tools/update-collection-resources.mjs` |
| `RelicItemIdToAchievement.csv` | FFXIV Collect 的[特殊武器](https://ffxivcollect.com/api/relics)与[成就](https://ffxivcollect.com/api/achievements)公开接口 | `tools/update-collection-resources.mjs` |
| `EquipmentSourceSupplement.csv` | GarlandTools 的公开物品端点 `https://garlandtools.org/db/doc/item/en/3/{ItemId}.json` | `tools/update-equipment-source-supplement.mjs` |

上述资源接入社区数据自动发布管道，运行时使用经校验的下载缓存或内置快照。`EquipmentSourceSupplement.meta.json` 保留原始资源 URL 与 SHA-256；旧版副本掉落资料不代表当前仍有相同产出。鱼糕数据采用本地声明的 protobuf 字段解码，不执行来源站 JavaScript；青魔数据按内容结构定位，不依赖打包变量名。

获取流程补充还参考 [FFXIV Collect](https://ffxivcollect.com/)、[Gamer Escape](https://ffxiv.gamerescape.com/wiki/Researchingway/Purchase_a_Cosmic_Tool_Replica)及 FFXIV Console Games Wiki 的[装备](https://ffxiv.consolegameswiki.com/wiki/Ardent_Accessories)和[任务](https://ffxiv.consolegameswiki.com/wiki/Dressed_to_Deceive)资料。

## 官方玩法、商品与活动资料

职业上限参考官方[驯兽师指南](https://na.finalfantasyxiv.com/jobguide/beastmaster/)和[青魔法师指南](https://na.finalfantasyxiv.com/jobguide/bluemage/)；魔兽图鉴补丁归类参考 [7.5 专题页](https://na.finalfantasyxiv.com/dawntrail/patch_7_5/)。

商品与活动来源补充参考 SQUARE ENIX 官方音乐商品页面、[Twitch 活动公告](https://na.finalfantasyxiv.com/lodestone/topics/detail/fad06b11fd789fce573492001f47b6605f94c95b)、[原声集实体特典](https://na.finalfantasyxiv.com/lodestone/topics/detail/6f6882bc8f027103d44d16a00bc7fc1d2078f3eb)、[画集实体特典](https://na.finalfantasyxiv.com/lodestone/topics/detail/39afebffa618e55f5f7514e446a9cc41209fc72a)、[永结同心商品说明](https://store.finalfantasyxiv.com/ffxivstore/en-gb/product/5)、[国服水晶冲突赛季奖励公告](https://actff1.web.sdo.com/project/ffcrystranking/reward21.html)及[北美粉丝节艺术比赛规则](https://fanfest-na.finalfantasyxiv.com/2026/en-us/news/art-contest)。商品特典、历史活动及其他区服资料不代表当前国服可以取得。

乐谱商品的官方来源：

- [The Best Compilation Album](https://na.finalfantasyxiv.com/blog/002521.html)、[Trial By Shadow](https://www.jp.square-enix.com/music/sem/page/ff14/primals/en/music/SQEX-20058.html)、[Journeys](https://na.finalfantasyxiv.com/lodestone/topics/detail/3b5a216316846eb1e90daf43401b420ea1549727)。
- [SCIONS & SINNERS](https://na.finalfantasyxiv.com/blog/003103.html)、[Beyond the Shadow](https://na.store.square-enix-games.com/the-primals-live-in-japan---beyond-the-shadow-_blu-ray_)、[Eorzean Symphony Vol. 3](https://www.jp.square-enix.com/music/sem/page/ff14/concert/2022/bdm/en/bonus/)。
- [Forge Ahead](https://www.jp.square-enix.com/music/sem/page/ff14/ForgeAhead/en/)、[And Back Again](https://www.jp.square-enix.com/music/sem/page/ff14/AndBackAgain/en/)、[Darkest Before Dawn](https://www.jp.square-enix.com/music/sem/page/ff14/primals/dbdBD/en/)。
- [Dark Decades Tour BUDOKAN](https://na.finalfantasyxiv.com/lodestone/topics/detail/01fc53dfcfcc7b2232a62225d639a819caf9074c)、[Eorzean Symphony Vol. 4](https://na.finalfantasyxiv.com/lodestone/topics/detail/a68d2d124ab9ea9829eff1df1987ad015482bd53)。

旧版采集工具商店来源参考[官方 1.19 NPC 商店说明](https://forum.square-enix.com/ffxiv/threads/24818-Changes-Planned-for-Patch-1.19/page2)，装备产出调整参考[官方 5.3 更新说明](https://eu.finalfantasyxiv.com/lodestone/topics/detail/c2f8c5ba14d9cfaa09d5aab63c5c2da260eae21a)。

## 青魔法师配装模型

配装评分参考 [Allagan Studies 青魔伤害文档](https://docs.google.com/document/d/1-NBJDkyl8h_UXFcOba-CrPPcFXZrDs2QXX53pSS3QJc/edit)与 [Blue Academy 所链接的模拟表](https://docs.google.com/spreadsheets/d/1IZBK7MMMuS4KABnWxDCUNrPRAf_hzUz89BmqYxjpMMA/edit)。队伍职能加成参考[官方 5.0 更新说明](https://na.finalfantasyxiv.com/lodestone/topics/detail/330f2b280067d69d85b17831c66712a499e97484)。该评分属于社区模型估算。

## 公开招募共享数据

公开招募摘要来自在线 Omni 客户端已收到的游戏列表，由 Omni Office 校验、去重并按招募剩余时间过期。仅共享公开条目，不保存玩家 CID、账号 ID、私聊或加入请求。

## 品牌图标

- Discord：[官方品牌素材](https://discord.com/branding)中的 Discord Symbol Color，使用 Blurple 版本。
- GitHub：[官方 Logo 素材](https://brand.github.com/foundations/logo)中的 GitHub Invertocat，使用黑色和白色版本。
- 爱发电：[官方应用页面](https://afdian.com/app)使用的 [152 像素应用图标](https://static.afdiancdn.com/static/img/icons/apple-touch-icon-152x152.png)。
- Omni Info：用户提供的原始图标。

品牌图标归各品牌或原权利人所有，用于标识对应链接。

本项目与上述社区、网站及其贡献者不存在隶属或背书关系。各来源内容的权利归原权利人所有。
