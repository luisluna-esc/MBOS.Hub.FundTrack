#!/bin/bash
# Actualiza FundTrack en el servidor con lo último de GitHub (backend y frontend).
# Uso, en el servidor:  bash /opt/fundtrack/backend/deploy.sh
# No aplica migraciones de base de datos: esas se corren antes, a mano (ver DEPLOY.md).
set -euo pipefail

ROOT=/opt/fundtrack

echo "== Bajando el código"
git -C "$ROOT/backend" pull --ff-only
git -C "$ROOT/frontend" pull --ff-only
echo "   backend:  $(git -C "$ROOT/backend" log --oneline -1)"
echo "   frontend: $(git -C "$ROOT/frontend" log --oneline -1)"

echo "== Construyendo las imágenes (la versión actual sigue funcionando mientras tanto)"
docker build -q -t fundtrack-backend:latest "$ROOT/backend" > /dev/null
docker build -q -t fundtrack-frontend:latest "$ROOT/frontend" > /dev/null

echo "== Cambiando los contenedores"
docker rm -f fundtrack-backend fundtrack-frontend > /dev/null 2>&1 || true

docker run -d --name fundtrack-backend --network fundtrack-net --restart unless-stopped \
  --env-file /root/fundtrack-backend.env --add-host host.docker.internal:host-gateway \
  -v "$ROOT/data/attachments:/app/App_Data/attachments" \
  -p 127.0.0.1:5090:8080 fundtrack-backend:latest > /dev/null

docker run -d --name fundtrack-frontend --network fundtrack-net --restart unless-stopped \
  -p 127.0.0.1:5091:80 fundtrack-frontend:latest > /dev/null

docker image prune -f > /dev/null

echo "== Revisando"
sleep 8
docker ps --format "   {{.Names}}: {{.Status}}" | grep fundtrack
echo "   sitio: $(curl -s -o /dev/null -w "%{http_code}" http://127.0.0.1:8090/)"
echo "Listo: http://148.230.73.226:8090"
