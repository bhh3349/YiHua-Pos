"""全局配置。生产切 MySQL 只需改 DSH_POS_DB。"""
import os
import sys

# 冻结（PyInstaller）运行时，数据目录放在 exe 同级；开发时放在 server/ 下
if getattr(sys, "frozen", False):
    BASE_DIR = os.path.dirname(sys.executable)
else:
    BASE_DIR = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

DATA_DIR = os.getenv("DSH_POS_DATA", os.path.join(BASE_DIR, "data"))
os.makedirs(DATA_DIR, exist_ok=True)

DB_FILE = os.path.join(DATA_DIR, "pos.db")
DB_URL = os.getenv("DSH_POS_DB", "sqlite:///" + DB_FILE.replace("\\", "/"))

JWT_SECRET = os.getenv("DSH_POS_JWT_SECRET", "dev-secret-change-me")
JWT_ALG = "HS256"
TOKEN_TTL_MIN = int(os.getenv("DSH_POS_TOKEN_TTL_MIN", "720"))       # 12h
REFRESH_TTL_MIN = int(os.getenv("DSH_POS_REFRESH_TTL_MIN", "10080"))  # 7d

# 商品 CSV 投放目录（相当于对象存储的根）
OSS_DIR = os.getenv("DSH_POS_OSS_DIR", os.path.join(DATA_DIR, "oss"))
OSS_BASE_URL = os.getenv("DSH_POS_OSS_BASE", "http://127.0.0.1:8000/oss/")

APP_NAME = "自有餐饮 POS 服务端"
API_VER = "100"
