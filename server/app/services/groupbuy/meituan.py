"""美团适配器 —— 骨架（本期未接入）。

实现保持最小：继承 :class:`SkeletonAdapter`，所有接口统一返回
``PLATFORM_NOT_INTEGRATED``。将来接入时只需在对应方法里填真实逻辑，
调用方（业务路由）不需要任何改动。
"""
from .platform import SkeletonAdapter, register_adapter


@register_adapter
class MeituanAdapter(SkeletonAdapter):
    """美团。platform_code = ``meituan``。"""
    platform_code = "meituan"
