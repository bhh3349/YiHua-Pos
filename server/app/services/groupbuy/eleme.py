"""饿了么适配器 —— 骨架（本期未接入）。"""
from .platform import SkeletonAdapter, register_adapter


@register_adapter
class ElemeAdapter(SkeletonAdapter):
    """饿了么。platform_code = ``eleme``。"""
    platform_code = "eleme"
