-- =====================================================================
-- Migración 07: se entra con usuario (nombre.apellido) en lugar de correo,
-- y ya no existe la contraseña temporal que obligaba a cambiarla.
-- La columna email pasa a llamarse username; los usuarios que ya existían
-- quedan con estos usuarios:
--   mbos@fundtrack.local          -> admin.mbos
--   analista@fundtrack.local      -> analista.fundtrack   (datos de ejemplo)
--   administrador@, tesorero@, auditor@, pastor@ -> <rol>.fundtrack
--   cualquier otro                -> lo que va antes de la @, con ".usuario"
--                                    si no tiene punto
-- Todo en una transacción.
-- =====================================================================

BEGIN;

ALTER TABLE app_user RENAME COLUMN email TO username;
ALTER TABLE app_user RENAME CONSTRAINT uq_app_user_email TO uq_app_user_username;

UPDATE app_user SET username = CASE username
    WHEN 'mbos@fundtrack.local' THEN 'admin.mbos'
    WHEN 'analista@fundtrack.local' THEN 'analista.fundtrack'
    WHEN 'administrador@fundtrack.local' THEN 'administrador.fundtrack'
    WHEN 'tesorero@fundtrack.local' THEN 'tesorero.fundtrack'
    WHEN 'auditor@fundtrack.local' THEN 'auditor.fundtrack'
    WHEN 'pastor@fundtrack.local' THEN 'pastor.fundtrack'
    ELSE CASE WHEN split_part(lower(username), '@', 1) LIKE '%.%'
              THEN split_part(lower(username), '@', 1)
              ELSE split_part(lower(username), '@', 1) || '.usuario' END
END;

ALTER TABLE app_user ALTER COLUMN username TYPE VARCHAR(100);
ALTER TABLE app_user DROP COLUMN must_change_password;

COMMIT;
