-- =====================================================================
-- DATOS DE EJEMPLO para probar el frontend con datos reales en la base.
-- Usa los catálogos reales ya importados (regiones, distritos, pastores e
-- iglesias) y agrega encima:
--   1. Un usuario por rol (contraseña de todos: Ejemplo2026).
--   2. Los 12 períodos de 2026: enero a julio cerrados, agosto en adelante
--      abiertos (agosto queda vencido: su fecha límite fue el 15/09).
--   3. Arqueos de junio a septiembre de 2026 para las Iglesias y Grupos
--      activos, con algunas diferencias en cada estado y su seguimiento.
-- Se puede correr más de una vez: lo que ya existe no se duplica.
-- Los arqueos de ejemplo quedan creados por analista@fundtrack.local; así
-- deleteSampleData.sql los puede borrar sin tocar datos reales.
-- No pasa por la API, por eso no deja filas en audit_log.
-- =====================================================================

BEGIN;

-- Mismo resultado en cada corrida (los montos salen de random()).
SELECT setseed(0.42);

-- ---------------------------------------------------------------------
-- 1. Usuarios de ejemplo, uno por rol
-- ---------------------------------------------------------------------
INSERT INTO app_user (full_name, email, password_hash, must_change_password, is_active, pastor_id, created_by, created_at)
SELECT v.full_name, v.email,
       '$2b$11$fI3ATeiKcUorvL7uwlCfZe6rju.i0roMmIKQwbywLXy5GSD2UUvOi',
       FALSE, TRUE,
       CASE WHEN v.role_name = 'Pastor' THEN (
           SELECT dp.pastor_id FROM district_pastor dp
           JOIN district d ON d.district_id = dp.district_id
           WHERE d.name = 'Villa Remedios' AND dp.end_date IS NULL
           LIMIT 1)
       END,
       (SELECT app_user_id FROM app_user WHERE email = 'mbos@fundtrack.local'),
       NOW() AT TIME ZONE 'UTC'
FROM (VALUES
    ('Analista Ejemplo', 'analista@fundtrack.local', 'Analista'),
    ('Administrador Ejemplo', 'administrador@fundtrack.local', 'Administrador'),
    ('Tesorero Ejemplo', 'tesorero@fundtrack.local', 'Tesorero'),
    ('Auditor Ejemplo', 'auditor@fundtrack.local', 'Auditor'),
    ('Pastor Ejemplo (Villa Remedios)', 'pastor@fundtrack.local', 'Pastor')
) AS v(full_name, email, role_name)
WHERE NOT EXISTS (SELECT 1 FROM app_user u WHERE u.email = v.email);

INSERT INTO app_user_role (app_user_id, role_id, assigned_by)
SELECT u.app_user_id, r.role_id, (SELECT app_user_id FROM app_user WHERE email = 'mbos@fundtrack.local')
FROM (VALUES
    ('analista@fundtrack.local', 'Analista'),
    ('administrador@fundtrack.local', 'Administrador'),
    ('tesorero@fundtrack.local', 'Tesorero'),
    ('auditor@fundtrack.local', 'Auditor'),
    ('pastor@fundtrack.local', 'Pastor')
) AS v(email, role_name)
JOIN app_user u ON u.email = v.email
JOIN role r ON r.name = v.role_name
ON CONFLICT DO NOTHING;

-- ---------------------------------------------------------------------
-- 2. Períodos 2026: fecha límite el 15 del mes siguiente
-- ---------------------------------------------------------------------
INSERT INTO period (year, month, due_date, status, closed_at, closed_by)
SELECT 2026, m,
       (make_date(2026, m, 1) + INTERVAL '1 month' + INTERVAL '14 days')::date,
       CASE WHEN m <= 7 THEN 'closed' ELSE 'open' END,
       CASE WHEN m <= 7 THEN (make_date(2026, m, 1) + INTERVAL '1 month' + INTERVAL '20 days 14 hours')::timestamp END,
       CASE WHEN m <= 7 THEN (SELECT app_user_id FROM app_user WHERE email = 'mbos@fundtrack.local') END
FROM generate_series(1, 12) AS m
ON CONFLICT (year, month) DO NOTHING;

-- ---------------------------------------------------------------------
-- 3. Arqueos de junio a septiembre
--    junio y julio: todas las iglesias; agosto: 85 %; septiembre: 50 %
--    (el mes en curso todavía se está registrando). 5 % no presentó.
--    15 % de los presentados tiene diferencia (de 10 a 300 Bs).
-- ---------------------------------------------------------------------
WITH analyst AS (
    SELECT app_user_id FROM app_user WHERE email = 'analista@fundtrack.local'
),
base AS (
    SELECT c.church_id, c.district_id, c.type, c.treasurer_name, c.elder_name,
           p.period_id, p.month, p.status AS period_status,
           (SELECT dp.pastor_id FROM district_pastor dp
             WHERE dp.district_id = c.district_id AND dp.end_date IS NULL LIMIT 1) AS pastor_id,
           random() AS r_coverage, random() AS r_submitted, random() AS r_acms,
           random() AS r_split, random() AS r_diff, random() AS r_amount, random() AS r_status
    FROM church c
    JOIN period p ON p.year = 2026 AND p.month BETWEEN 6 AND 9
    WHERE c.status = 'active' AND c.type <> 'Filial'
),
picked AS (
    SELECT * FROM base
    WHERE r_coverage < CASE month WHEN 8 THEN 0.85 WHEN 9 THEN 0.50 ELSE 1 END
),
amounts AS (
    SELECT *,
           r_submitted >= 0.05 AS submitted,
           -- 3 % con saldo ACMS negativo (gastó más de lo que tenía).
           CASE WHEN r_acms < 0.03 THEN -round((r_acms * 10000)::numeric, 2)
                ELSE round((200 + r_acms * 6000)::numeric, 2) END AS acms,
           CASE WHEN r_diff < 0.15
                THEN round(((10 + r_amount * 290) * CASE WHEN r_amount < 0.6 THEN -1 ELSE 1 END)::numeric, 2)
                ELSE 0 END AS diff
    FROM picked
),
final AS (
    SELECT *,
           CASE WHEN NOT submitted THEN 0 ELSE GREATEST(acms + diff, 0) END AS counted
    FROM amounts
)
INSERT INTO local_fund_count (
    church_id, period_id, district_id, pastor_id, church_type, submission_status,
    acms_balance, local_cash, bank_statement_balance, total_amount, difference, difference_status,
    count_date, treasurer_name, elder_name, signed_by_pastor, signed_by_elder, observation,
    created_by, created_at)
SELECT f.church_id, f.period_id, f.district_id, f.pastor_id, f.type,
       CASE WHEN f.submitted THEN 'submitted' ELSE 'not_submitted' END,
       f.acms,
       CASE WHEN f.submitted THEN round(f.counted * f.r_split::numeric, 2) ELSE 0 END,
       CASE WHEN f.submitted THEN f.counted - round(f.counted * f.r_split::numeric, 2) ELSE 0 END,
       f.counted,
       CASE WHEN f.submitted THEN f.counted - f.acms ELSE 0 END,
       CASE
           WHEN NOT f.submitted OR f.counted - f.acms = 0 THEN 'none'
           WHEN f.period_status = 'closed' THEN CASE WHEN f.r_status < 0.6 THEN 'resolved' ELSE 'pending' END
           WHEN f.month = 8 THEN CASE WHEN f.r_status < 0.6 THEN 'pending' ELSE 'resolved' END
           ELSE 'pending'
       END,
       CASE WHEN f.submitted THEN make_date(2026, f.month, 1) + INTERVAL '1 month' - INTERVAL '1 day' END,
       f.treasurer_name, f.elder_name,
       f.submitted AND f.r_split > 0.1, f.submitted AND f.r_split > 0.2,
       CASE WHEN NOT f.submitted THEN 'La iglesia no entregó el arqueo del mes.' END,
       (SELECT app_user_id FROM analyst),
       (make_date(2026, f.month, 1) + INTERVAL '1 month' + INTERVAL '3 days 15 hours')::timestamp
FROM final f
ON CONFLICT (church_id, period_id) DO NOTHING;

-- ---------------------------------------------------------------------
-- 4. Seguimiento de las diferencias resueltas (de pendiente a resuelta)
-- ---------------------------------------------------------------------
INSERT INTO difference_follow_up (local_fund_count_id, previous_status, new_status, justification, app_user_id, followed_up_at)
SELECT lfc.local_fund_count_id, 'pending', 'resolved',
       'La Misión revisó el ACMS y da la diferencia por cerrada.',
       lfc.created_by, lfc.created_at + INTERVAL '12 days'
FROM local_fund_count lfc
JOIN app_user u ON u.app_user_id = lfc.created_by AND u.email = 'analista@fundtrack.local'
WHERE lfc.difference_status = 'resolved'
  AND NOT EXISTS (SELECT 1 FROM difference_follow_up f WHERE f.local_fund_count_id = lfc.local_fund_count_id);

COMMIT;

-- Resumen de lo que quedó.
SELECT p.month, COUNT(*) AS arqueos,
       COUNT(*) FILTER (WHERE lfc.submission_status = 'not_submitted') AS no_presento,
       COUNT(*) FILTER (WHERE lfc.difference_status = 'pending') AS pendientes,
       COUNT(*) FILTER (WHERE lfc.difference_status = 'resolved') AS resueltas
FROM local_fund_count lfc
JOIN period p ON p.period_id = lfc.period_id
GROUP BY p.month ORDER BY p.month;
