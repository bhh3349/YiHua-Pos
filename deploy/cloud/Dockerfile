# 自有餐饮 POS 服务端 —— 生产镜像
#
# 构建： docker build -t yuedian/pos-server:1.0 .
# 运行： docker run -d --name pos-server -p 8000:8000 \
#          -e DSH_POS_DB="mysql+pymysql://pos:PASS@mysql:3306/pos?charset=utf8mb4" \
#          -e DSH_POS_JWT_SECRET="change-me" \
#          yuedian/pos-server:1.0

FROM python:3.12-slim

ENV PYTHONUNBUFFERED=1 \
    PYTHONDONTWRITEBYTECODE=1 \
    TZ=Asia/Shanghai \
    DSH_POS_DATA=/data

WORKDIR /app

# 依赖层单独缓存
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt pymysql gunicorn

COPY app ./app
COPY run.py seed.py export_legacy_csv.py ./

RUN mkdir -p /data && \
    useradd -m -u 10001 pos && chown -R pos:pos /app /data
USER pos

EXPOSE 8000
VOLUME ["/data"]

HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
  CMD python -c "import urllib.request,sys; sys.exit(0 if b'\"ok\"' in urllib.request.urlopen('http://127.0.0.1:8000/health',timeout=4).read() else 1)"

# 多 worker：SQLite 不支持，生产请用 MySQL
CMD ["gunicorn", "-k", "uvicorn.workers.UvicornWorker", \
     "-w", "4", "-b", "0.0.0.0:8000", \
     "--access-logfile", "-", "--error-logfile", "-", \
     "app.main:app"]
