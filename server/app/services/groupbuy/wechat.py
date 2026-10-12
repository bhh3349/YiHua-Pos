"""微信平台适配器 —— 骨架（本期未接入）。"""
from .platform import SkeletonAdapter, register_adapter


@register_adapter
class WechatAdapter(SkeletonAdapter):
    """微信。platform_code = ``wechat``。"""
    platform_code = "wechat"
