"""团购平台对接服务层（T-405）。

模块速览：
  * ``errors``         错误码（券状态 8 种细分 + 平台层错误）
  * ``crypto``         密钥加密存储 / 脱敏
  * ``platform``       PlatformAdapter 抽象基类 + 注册表
  * ``self_platform``  自有平台完整实现（其余平台为骨架）
  * ``verify``         券状态机（与平台无关的 8 项校验）
  * ``store``          平台配置 CRUD + CouponQuery 构建
  * ``guard``          验券限流
  * ``reconciliation`` 对账差异 + CSV 导出
"""
