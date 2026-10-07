-- =====================================================================
-- Seed del usuario M-BOS inicial. Se inicia sesión con el email.
-- Password en texto plano (solo para referencia, no se guarda así):
--   mbos@fundtrack.local  ->  mbos12345
-- Cámbiala apenas entres en un ambiente real.
-- =====================================================================

INSERT INTO app_user (full_name, email, password_hash, must_change_password, is_active) VALUES
('M-BOS', 'mbos@fundtrack.local', '$2b$11$Ap2hxeTvzarOS3rlKveAhu7TTllP3l7Yy7Mf7GWPkAoDerQCj23ya', FALSE, TRUE)
ON CONFLICT (email) DO NOTHING;

INSERT INTO app_user_role (app_user_id, role_id)
SELECT u.app_user_id, r.role_id
FROM app_user u
JOIN role r ON r.name = 'M-BOS'
WHERE u.email = 'mbos@fundtrack.local'
ON CONFLICT (app_user_id, role_id) DO NOTHING;
