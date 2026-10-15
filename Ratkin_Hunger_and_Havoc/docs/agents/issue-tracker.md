# 工作项：GitHub

缺陷、规格、实施工作项和后续债统一使用当前仓库 [GitHub Issues](https://github.com/Nanaloveyuki/Ratkin_Hunger_and_Havoc/issues)。机制讨论留在对应设计稿，形成可执行工作项后再发布 Issue。

## 操作

使用 `gh issue create/view/list/edit/close`，发布前从本仓库解析远程。前置依赖使用 GitHub 原生 `blocked_by`，目标为阻塞项的数据库 ID；不可用时在正文注明 `Blocked by: #编号`。已经由规格拆出的实施工作项按前置依赖推进，不重复分流。

缺陷 Issue 按已知事实记录复现或玩家反馈、期望与实际、相关版本及影响；定位后补根因、修复范围和实际验证结果。不为填写模板补做无关 Smoke 或 Tests，也不批量把历史记录发布成新 Issue。
