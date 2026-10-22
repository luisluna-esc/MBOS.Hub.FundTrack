-- =====================================================================
-- Seed del usuario M-BOS inicial. Se inicia sesión con el usuario (nombre.apellido).
-- Password en texto plano (solo para referencia, no se guarda así):
--   admin.mbos  ->  mbos12345
-- Cámbiala apenas entres en un ambiente real.
-- =====================================================================

INSERT INTO app_user (full_name, username, password_hash, is_active) VALUES
('M-BOS', 'admin.mbos', '$2b$11$Ap2hxeTvzarOS3rlKveAhu7TTllP3l7Yy7Mf7GWPkAoDerQCj23ya', TRUE)
ON CONFLICT (username) DO NOTHING;

INSERT INTO app_user_role (app_user_id, role_id)
SELECT u.app_user_id, r.role_id
FROM app_user u
JOIN role r ON r.name = 'M-BOS'
WHERE u.username = 'admin.mbos'
ON CONFLICT (app_user_id, role_id) DO NOTHING;
