-- =====================================================================
-- Borra lo que agregó seedSampleData.sql: los arqueos de ejemplo (creados
-- por analista.fundtrack) con su seguimiento, correcciones y
-- adjuntos, y los 5 usuarios de ejemplo. Los períodos de 2026 se quedan,
-- porque el sistema real también los necesita.
-- Ojo: si con el usuario analista.fundtrack se registraron arqueos
-- de verdad, también se borran.
-- =====================================================================

BEGIN;

CREATE TEMP TABLE sample_counts ON COMMIT DROP AS
SELECT lfc.local_fund_count_id
FROM local_fund_count lfc
JOIN app_user u ON u.app_user_id = lfc.created_by
WHERE u.username = 'analista.fundtrack';

DELETE FROM difference_follow_up WHERE local_fund_count_id IN (SELECT local_fund_count_id FROM sample_counts);
DELETE FROM local_fund_count_correction WHERE local_fund_count_id IN (SELECT local_fund_count_id FROM sample_counts);
DELETE FROM local_fund_count_attachment WHERE local_fund_count_id IN (SELECT local_fund_count_id FROM sample_counts);
DELETE FROM local_fund_count WHERE local_fund_count_id IN (SELECT local_fund_count_id FROM sample_counts);

-- Los usuarios de ejemplo pueden figurar en otras tablas (refresh_token, auditoría, seguimiento):
-- se sueltan esas referencias antes de borrarlos.
CREATE TEMP TABLE sample_users ON COMMIT DROP AS
SELECT app_user_id FROM app_user
WHERE username IN ('analista.fundtrack', 'administrador.fundtrack', 'tesorero.fundtrack',
                'auditor.fundtrack', 'pastor.fundtrack');

DELETE FROM refresh_token WHERE app_user_id IN (SELECT app_user_id FROM sample_users);
UPDATE audit_log SET app_user_id = NULL WHERE app_user_id IN (SELECT app_user_id FROM sample_users);
UPDATE difference_follow_up SET app_user_id = NULL WHERE app_user_id IN (SELECT app_user_id FROM sample_users);
DELETE FROM app_user WHERE app_user_id IN (SELECT app_user_id FROM sample_users);

COMMIT;
