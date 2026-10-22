# Despliegue de FundTrack

FundTrack está en el mismo servidor que InvenTrack (`148.230.73.226`, Ubuntu, Docker) y se abre en
**http://148.230.73.226:8090**.

## Cómo está armado

| Pieza | Dónde | Detalle |
|---|---|---|
| Frontend | contenedor `fundtrack-frontend` | Angular servido por nginx; pasa `/api` al backend. Puerto local `127.0.0.1:5091`. |
| Backend | contenedor `fundtrack-backend` | .NET 10. Puerto local `127.0.0.1:5090`. Red Docker `fundtrack-net`. |
| Base de datos | PostgreSQL 17 del servidor | Base `fundtrack`, usuario `fundtrack`. El contenedor entra por `host.docker.internal`. |
| Configuración | `/root/fundtrack-backend.env` | Cadena de conexión, clave JWT y CORS. Solo root la puede leer. **No va a Git.** |
| Adjuntos | `/opt/fundtrack/data/attachments` | Montado en el contenedor en `/app/App_Data/attachments`. Se conserva al actualizar. |
| Entrada pública | nginx del servidor, `/etc/nginx/sites-available/fundtrack.conf` | Escucha en el 8090 y pasa todo a `127.0.0.1:5091`. |
| Código | `/opt/fundtrack/backend` y `/opt/fundtrack/frontend` | Desde donde se construyen las imágenes. |

Los archivos de despliegue están en los repos: `Dockerfile` y `.dockerignore` en los dos, y
`nginx.conf` en el frontend.

## Actualizar a una nueva versión

1. Sube los cambios a GitHub (backend y frontend).
2. Si hay cambios de base de datos, primero haz un respaldo y después aplica la migración
   (`Scrips/migration_XX_*.sql` o las funciones de `Scrips/storedProcedures.sql`):

   ```bash
   sudo -u postgres pg_dump -Fc fundtrack > /root/fundtrack_antes_$(date +%Y%m%d_%H%M).dump
   PGPASSWORD='<clave del .env>' psql -h 127.0.0.1 -U fundtrack -d fundtrack -v ON_ERROR_STOP=1 -f /opt/fundtrack/backend/Scrips/migration_XX.sql
   ```

3. Baja el código nuevo y reconstruye. Con `git` (la primera vez hay que clonar; después alcanza con `git pull`):

   ```bash
   cd /opt/fundtrack
   rm -rf backend frontend   # solo la primera vez: hoy el código se subió sin .git
   git clone https://github.com/luisluna-esc/MBOS.Hub.FundTrack.git backend
   git clone https://github.com/luisluna-esc/MBOS.Hub.FundTrack.Web.git frontend
   # las siguientes veces:  git -C backend pull && git -C frontend pull

   docker build -t fundtrack-backend:latest backend
   docker build -t fundtrack-frontend:latest frontend
   ```

4. Cambia los contenedores (los adjuntos y la configuración no se tocan):

   ```bash
   docker rm -f fundtrack-backend fundtrack-frontend

   docker run -d --name fundtrack-backend --network fundtrack-net --restart unless-stopped \
     --env-file /root/fundtrack-backend.env --add-host host.docker.internal:host-gateway \
     -v /opt/fundtrack/data/attachments:/app/App_Data/attachments \
     -p 127.0.0.1:5090:8080 fundtrack-backend:latest

   docker run -d --name fundtrack-frontend --network fundtrack-net --restart unless-stopped \
     -p 127.0.0.1:5091:80 fundtrack-frontend:latest
   ```

5. Revisa que esté bien:

   ```bash
   docker ps | grep fundtrack
   docker logs --tail 30 fundtrack-backend
   curl -s -o /dev/null -w "%{http_code}\n" http://127.0.0.1:8090/
   ```

   El aviso `Cannot load library libgssapi_krb5.so.2` en el log es normal: es una librería opcional
   de Kerberos que no se usa.

## Respaldos

Un respaldo manual de la base:

```bash
sudo -u postgres pg_dump -Fc fundtrack > /root/fundtrack_$(date +%Y%m%d_%H%M).dump
```

Los adjuntos se respaldan copiando `/opt/fundtrack/data/attachments`.
