"""抖音适配器 —— 骨架（本期未接入）。"""
from .platform import SkeletonAdapter, register_adapter


@register_adapter
class DouyinAdapter(SkeletonAdapter):
    """抖音。platform_code = ``douyin``。"""
    platform_code = "douyin"
