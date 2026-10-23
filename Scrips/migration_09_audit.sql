-- Migración 09: la auditoría muestra solo quién creó, editó o eliminó qué y cuándo, sin IP ni
-- navegador, y con el nombre del registro (record_label). Solo cambia la función de consulta:
-- la tabla audit_log queda igual.
BEGIN;

-- ---------------------------------------------------------------------
-- Auditoría: quién creó, editó o eliminó qué y cuándo. Solo esas tres acciones (no las descargas
-- de reportes), sin las sesiones (refresh_token) ni los ingresos al sistema (app_user que solo
-- cambia la fecha de ingreso o los intentos fallidos). record_label dice de qué registro se trata
-- (la iglesia y el mes de un arqueo, el nombre de una iglesia, etc.) para no mostrar solo un id.
-- ---------------------------------------------------------------------
DROP FUNCTION IF EXISTS sp_get_audit_logs;
CREATE OR REPLACE FUNCTION sp_get_audit_logs(
    p_app_user_id BIGINT,
    p_table_name VARCHAR,
    p_record_id BIGINT,
    p_action VARCHAR,
    p_page_number INT,
    p_page_size INT
)
RETURNS TABLE (
    audit_log_id BIGINT,
    app_user_id BIGINT,
    app_user_full_name VARCHAR,
    action VARCHAR,
    table_name VARCHAR,
    record_id BIGINT,
    record_label TEXT,
    old_value TEXT,
    new_value TEXT,
    created_at TIMESTAMPTZ,
    total_records INT
)
LANGUAGE sql
STABLE
AS $$
    WITH logs AS (
        SELECT
            al.*,
            COALESCE(al.new_value, al.old_value) AS v,
            CAST(COUNT(*) OVER() AS INT) AS total_records
        FROM audit_log al
        WHERE al.action IN ('INSERT', 'UPDATE', 'DELETE')
          AND al.table_name <> 'refresh_token'
          AND NOT (
              al.table_name = 'app_user' AND al.action = 'UPDATE'
              AND (al.old_value - ARRAY['LastLogin', 'FailedLoginAttempts', 'LockedUntil'])
                = (al.new_value - ARRAY['LastLogin', 'FailedLoginAttempts', 'LockedUntil'])
          )
          AND (p_app_user_id IS NULL OR al.app_user_id = p_app_user_id)
          AND (p_table_name IS NULL OR al.table_name = p_table_name)
          AND (p_record_id IS NULL OR al.record_id = p_record_id)
          AND (p_action IS NULL OR al.action = p_action)
        ORDER BY al.created_at DESC, al.audit_log_id DESC
        LIMIT p_page_size OFFSET (p_page_number - 1) * p_page_size
    )
    SELECT
        l.audit_log_id,
        l.app_user_id,
        u.full_name AS app_user_full_name,
        l.action,
        l.table_name,
        l.record_id,
        CASE l.table_name
            WHEN 'local_fund_count' THEN (
                SELECT concat_ws(' · ', c.code || ' ' || c.name, lpad(p.month::text, 2, '0') || '/' || p.year)
                FROM church c
                LEFT JOIN period p ON p.period_id = (l.v->>'PeriodId')::bigint
                WHERE c.church_id = (l.v->>'ChurchId')::bigint)
            WHEN 'local_fund_count_document' THEN COALESCE((
                SELECT concat_ws(' · ', c.code || ' ' || c.name, lpad(p.month::text, 2, '0') || '/' || p.year, l.v->>'Detail')
                FROM local_fund_count lc
                JOIN church c ON c.church_id = lc.church_id
                JOIN period p ON p.period_id = lc.period_id
                WHERE lc.local_fund_count_id = (l.v->>'LocalFundCountId')::bigint), l.v->>'Detail')
            WHEN 'local_fund_count_attachment' THEN (
                SELECT concat_ws(' · ', c.code || ' ' || c.name, lpad(p.month::text, 2, '0') || '/' || p.year)
                FROM local_fund_count lc
                JOIN church c ON c.church_id = lc.church_id
                JOIN period p ON p.period_id = lc.period_id
                WHERE lc.local_fund_count_id = (l.v->>'LocalFundCountId')::bigint)
            WHEN 'local_fund_count_correction' THEN (
                SELECT concat_ws(' · ', c.code || ' ' || c.name, lpad(p.month::text, 2, '0') || '/' || p.year)
                FROM local_fund_count lc
                JOIN church c ON c.church_id = lc.church_id
                JOIN period p ON p.period_id = lc.period_id
                WHERE lc.local_fund_count_id = (l.v->>'LocalFundCountId')::bigint)
            WHEN 'difference_follow_up' THEN (
                SELECT concat_ws(' · ', c.code || ' ' || c.name, lpad(p.month::text, 2, '0') || '/' || p.year)
                FROM local_fund_count lc
                JOIN church c ON c.church_id = lc.church_id
                JOIN period p ON p.period_id = lc.period_id
                WHERE lc.local_fund_count_id = (l.v->>'LocalFundCountId')::bigint)
            WHEN 'church_history' THEN (SELECT c.code || ' ' || c.name FROM church c WHERE c.church_id = (l.v->>'ChurchId')::bigint)
            WHEN 'church' THEN concat_ws(' ', l.v->>'Code', l.v->>'Name')
            WHEN 'pastor' THEN concat_ws(' ', l.v->>'FirstNames', l.v->>'LastNames')
            WHEN 'district_pastor' THEN concat_ws(' · ',
                (SELECT pa.first_names || ' ' || pa.last_names FROM pastor pa WHERE pa.pastor_id = (l.v->>'PastorId')::bigint),
                (SELECT d.name FROM district d WHERE d.district_id = (l.v->>'DistrictId')::bigint))
            WHEN 'period' THEN lpad(l.v->>'Month', 2, '0') || '/' || (l.v->>'Year')
            WHEN 'app_user_role' THEN concat_ws(' · ',
                (SELECT au.full_name FROM app_user au WHERE au.app_user_id = (l.v->>'AppUserId')::bigint),
                (SELECT r.name FROM role r WHERE r.role_id = (l.v->>'RoleId')::bigint))
            WHEN 'menu_role' THEN concat_ws(' · ',
                (SELECT r.name FROM role r WHERE r.role_id = (l.v->>'RoleId')::bigint),
                (SELECT m.name FROM menu m WHERE m.menu_id = (l.v->>'MenuId')::bigint))
            ELSE COALESCE(l.v->>'FullName', l.v->>'Name', l.v->>'Username')
        END AS record_label,
        l.old_value::TEXT,
        l.new_value::TEXT,
        l.created_at AT TIME ZONE 'UTC' AS created_at,
        l.total_records
    FROM logs l
    LEFT JOIN app_user u ON u.app_user_id = l.app_user_id
    ORDER BY l.created_at DESC, l.audit_log_id DESC;
$$;

COMMIT;
