-- =====================================================================
-- Migración 06: sin estado "justificada" en las diferencias.
-- El ACMS es el que cubre lo que falta o sobra: el sistema solo registra
-- la diferencia, que queda pendiente y, si la Misión lo marca, resuelta.
-- 1. Las diferencias justificadas vuelven a pendiente y su paso de
--    seguimiento "justificada" se borra; en las resueltas, el paso a
--    resuelta queda como de pendiente a resuelta.
-- 2. Las restricciones de estado ya no aceptan 'justified'.
-- 3. Se vuelven a crear las 3 funciones que contaban justificadas
--    (Inicio, Pendientes del mes y Resumen por región).
-- Todo en una transacción.
-- =====================================================================

BEGIN;

UPDATE local_fund_count SET difference_status = 'pending' WHERE difference_status = 'justified';
DELETE FROM difference_follow_up WHERE new_status = 'justified';
UPDATE difference_follow_up SET previous_status = 'pending' WHERE previous_status = 'justified';

ALTER TABLE local_fund_count DROP CONSTRAINT ck_local_fund_count_difference_status;
ALTER TABLE local_fund_count
    ADD CONSTRAINT ck_local_fund_count_difference_status CHECK (difference_status IN ('none', 'pending', 'resolved'));

ALTER TABLE difference_follow_up DROP CONSTRAINT ck_difference_follow_up_previous_status;
ALTER TABLE difference_follow_up DROP CONSTRAINT ck_difference_follow_up_new_status;
ALTER TABLE difference_follow_up
    ADD CONSTRAINT ck_difference_follow_up_previous_status CHECK (previous_status IN ('none', 'pending', 'resolved')),
    ADD CONSTRAINT ck_difference_follow_up_new_status CHECK (new_status IN ('none', 'pending', 'resolved'));

DROP FUNCTION IF EXISTS sp_get_dashboard_by_region;
CREATE OR REPLACE FUNCTION sp_get_dashboard_by_region(
    p_period_id BIGINT,
    p_restricted_district_id BIGINT
)
RETURNS TABLE (
    region_id BIGINT,
    region_name VARCHAR,
    active_churches INT,
    submitted INT,
    not_submitted INT,
    missing INT,
    differences_pending INT,
    differences_resolved INT,
    negative_acms INT,
    total_acms_balance NUMERIC,
    total_counted NUMERIC,
    total_difference NUMERIC
)
LANGUAGE sql
STABLE
AS $$
    WITH scoped_churches AS (
        SELECT c.church_id, d.region_id
        FROM church c
        JOIN district d ON d.district_id = c.district_id
        WHERE c.status = 'active'
          AND c.type <> 'Filial'
          AND (p_restricted_district_id IS NULL OR c.district_id = p_restricted_district_id)
    ),
    period_counts AS (
        SELECT lfc.*, d.region_id
        FROM local_fund_count lfc
        JOIN district d ON d.district_id = lfc.district_id
        WHERE lfc.period_id = p_period_id
          AND (p_restricted_district_id IS NULL OR lfc.district_id = p_restricted_district_id)
    )
    SELECT
        r.region_id,
        r.name AS region_name,
        CAST((SELECT COUNT(*) FROM scoped_churches sc WHERE sc.region_id = r.region_id) AS INT) AS active_churches,
        CAST((SELECT COUNT(*) FROM period_counts k WHERE k.region_id = r.region_id AND k.submission_status = 'submitted') AS INT) AS submitted,
        CAST((SELECT COUNT(*) FROM period_counts k WHERE k.region_id = r.region_id AND k.submission_status = 'not_submitted') AS INT) AS not_submitted,
        CAST((SELECT COUNT(*) FROM scoped_churches sc
              WHERE sc.region_id = r.region_id
                AND NOT EXISTS (SELECT 1 FROM local_fund_count x WHERE x.church_id = sc.church_id AND x.period_id = p_period_id)) AS INT) AS missing,
        CAST((SELECT COUNT(*) FROM period_counts k WHERE k.region_id = r.region_id AND k.difference_status = 'pending') AS INT) AS differences_pending,
        CAST((SELECT COUNT(*) FROM period_counts k WHERE k.region_id = r.region_id AND k.difference_status = 'resolved') AS INT) AS differences_resolved,
        CAST((SELECT COUNT(*) FROM period_counts k WHERE k.region_id = r.region_id AND k.acms_balance < 0) AS INT) AS negative_acms,
        COALESCE((SELECT SUM(k.acms_balance) FROM period_counts k WHERE k.region_id = r.region_id), 0) AS total_acms_balance,
        COALESCE((SELECT SUM(k.total_amount) FROM period_counts k WHERE k.region_id = r.region_id), 0) AS total_counted,
        COALESCE((SELECT SUM(k.difference) FROM period_counts k WHERE k.region_id = r.region_id), 0) AS total_difference
    FROM region r
    WHERE EXISTS (SELECT 1 FROM scoped_churches sc WHERE sc.region_id = r.region_id)
       OR EXISTS (SELECT 1 FROM period_counts k WHERE k.region_id = r.region_id)
    ORDER BY r.name;
$$;

DROP FUNCTION IF EXISTS sp_report_pending;
CREATE OR REPLACE FUNCTION sp_report_pending(
    p_period_id BIGINT,
    p_region_id BIGINT,
    p_district_id BIGINT,
    p_restricted_district_id BIGINT
)
RETURNS TABLE (
    region_name VARCHAR,
    district_name VARCHAR,
    pastor_name TEXT,
    church_id BIGINT,
    church_code VARCHAR,
    church_name VARCHAR,
    church_type VARCHAR,
    pending_kind VARCHAR,
    acms_balance NUMERIC,
    total_amount NUMERIC,
    difference NUMERIC,
    difference_status VARCHAR
)
LANGUAGE sql
STABLE
AS $$
    SELECT
        r.name AS region_name,
        d.name AS district_name,
        COALESCE(pa.last_names || ', ' || pa.first_names, cp.last_names || ', ' || cp.first_names) AS pastor_name,
        c.church_id,
        c.code AS church_code,
        c.name AS church_name,
        c.type AS church_type,
        CAST(CASE
            WHEN lfc.local_fund_count_id IS NULL THEN 'missing'
            WHEN lfc.submission_status = 'not_submitted' THEN 'not_submitted'
            ELSE 'difference'
        END AS VARCHAR) AS pending_kind,
        lfc.acms_balance,
        lfc.total_amount,
        lfc.difference,
        lfc.difference_status
    FROM church c
    LEFT JOIN local_fund_count lfc ON lfc.church_id = c.church_id AND lfc.period_id = p_period_id
    JOIN district d ON d.district_id = COALESCE(lfc.district_id, c.district_id)
    JOIN region r ON r.region_id = d.region_id
    LEFT JOIN pastor pa ON pa.pastor_id = lfc.pastor_id
    LEFT JOIN LATERAL (
        SELECT p.last_names, p.first_names
        FROM district_pastor dp
        JOIN pastor p ON p.pastor_id = dp.pastor_id
        WHERE dp.district_id = d.district_id AND dp.end_date IS NULL
    ) cp ON TRUE
    WHERE c.type <> 'Filial'
      AND (c.status = 'active' OR lfc.local_fund_count_id IS NOT NULL)
      AND (lfc.local_fund_count_id IS NULL
           OR lfc.submission_status = 'not_submitted'
           OR lfc.difference_status = 'pending')
      AND (p_restricted_district_id IS NULL OR d.district_id = p_restricted_district_id)
      AND (p_district_id IS NULL OR d.district_id = p_district_id)
      AND (p_region_id IS NULL OR r.region_id = p_region_id)
    ORDER BY r.name, d.name, c.name;
$$;

DROP FUNCTION IF EXISTS sp_report_region_summary;
CREATE OR REPLACE FUNCTION sp_report_region_summary(
    p_year INT,
    p_month_from INT,
    p_month_to INT,
    p_restricted_district_id BIGINT
)
RETURNS TABLE (
    period_month INT,
    region_name VARCHAR,
    active_churches INT,
    submitted INT,
    not_submitted INT,
    missing INT,
    differences_pending INT,
    differences_resolved INT,
    negative_acms INT,
    total_acms_balance NUMERIC,
    total_counted NUMERIC,
    total_difference NUMERIC
)
LANGUAGE sql
STABLE
AS $$
    WITH periods AS (
        SELECT pe.period_id, pe.month
        FROM period pe
        WHERE pe.year = p_year AND pe.month BETWEEN p_month_from AND p_month_to
    ),
    scoped_churches AS (
        SELECT c.church_id, d.region_id
        FROM church c
        JOIN district d ON d.district_id = c.district_id
        WHERE c.status = 'active'
          AND c.type <> 'Filial'
          AND (p_restricted_district_id IS NULL OR c.district_id = p_restricted_district_id)
    ),
    counts AS (
        SELECT lfc.*, d.region_id
        FROM local_fund_count lfc
        JOIN district d ON d.district_id = lfc.district_id
        WHERE lfc.period_id IN (SELECT period_id FROM periods)
          AND (p_restricted_district_id IS NULL OR lfc.district_id = p_restricted_district_id)
    )
    SELECT
        p.month AS period_month,
        r.name AS region_name,
        CAST((SELECT COUNT(*) FROM scoped_churches sc WHERE sc.region_id = r.region_id) AS INT) AS active_churches,
        CAST((SELECT COUNT(*) FROM counts k WHERE k.period_id = p.period_id AND k.region_id = r.region_id AND k.submission_status = 'submitted') AS INT) AS submitted,
        CAST((SELECT COUNT(*) FROM counts k WHERE k.period_id = p.period_id AND k.region_id = r.region_id AND k.submission_status = 'not_submitted') AS INT) AS not_submitted,
        CAST((SELECT COUNT(*) FROM scoped_churches sc
              WHERE sc.region_id = r.region_id
                AND NOT EXISTS (SELECT 1 FROM local_fund_count x WHERE x.church_id = sc.church_id AND x.period_id = p.period_id)) AS INT) AS missing,
        CAST((SELECT COUNT(*) FROM counts k WHERE k.period_id = p.period_id AND k.region_id = r.region_id AND k.difference_status = 'pending') AS INT) AS differences_pending,
        CAST((SELECT COUNT(*) FROM counts k WHERE k.period_id = p.period_id AND k.region_id = r.region_id AND k.difference_status = 'resolved') AS INT) AS differences_resolved,
        CAST((SELECT COUNT(*) FROM counts k WHERE k.period_id = p.period_id AND k.region_id = r.region_id AND k.acms_balance < 0) AS INT) AS negative_acms,
        COALESCE((SELECT SUM(k.acms_balance) FROM counts k WHERE k.period_id = p.period_id AND k.region_id = r.region_id), 0) AS total_acms_balance,
        COALESCE((SELECT SUM(k.total_amount) FROM counts k WHERE k.period_id = p.period_id AND k.region_id = r.region_id), 0) AS total_counted,
        COALESCE((SELECT SUM(k.difference) FROM counts k WHERE k.period_id = p.period_id AND k.region_id = r.region_id), 0) AS total_difference
    FROM periods p
    CROSS JOIN region r
    WHERE EXISTS (SELECT 1 FROM scoped_churches sc WHERE sc.region_id = r.region_id)
       OR EXISTS (SELECT 1 FROM counts k WHERE k.period_id = p.period_id AND k.region_id = r.region_id)
    ORDER BY p.month, r.name;
$$;

COMMIT;
