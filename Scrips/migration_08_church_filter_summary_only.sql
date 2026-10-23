-- =====================================================================
-- Migración 08:
-- 1. local_fund_count.summary_only: la iglesia trajo su arqueo ya hecho y
--    solo se registran el saldo ACMS y el total del arqueo (sin el detalle
--    de banco, efectivo, dólares ni documentos, y sin pedir observación si
--    hay diferencia).
-- 2. Las funciones de listado de arqueos devuelven summary_only.
-- 3. Seis reportes reciben p_church_id para filtrar una sola iglesia.
-- Todo en una transacción.
-- =====================================================================

BEGIN;

ALTER TABLE local_fund_count ADD COLUMN summary_only BOOLEAN NOT NULL DEFAULT FALSE;

DROP FUNCTION IF EXISTS sp_get_local_fund_counts;
CREATE OR REPLACE FUNCTION sp_get_local_fund_counts(
    p_local_fund_count_id BIGINT,
    p_period_id BIGINT,
    p_year INT,
    p_church_id BIGINT,
    p_district_id BIGINT,
    p_region_id BIGINT,
    p_pastor_id BIGINT,
    p_submission_status VARCHAR,
    p_difference_status VARCHAR,
    p_search VARCHAR,
    p_restricted_district_id BIGINT,
    p_page_number INT,
    p_page_size INT
)
RETURNS TABLE (
    local_fund_count_id BIGINT,
    church_id BIGINT,
    church_code VARCHAR,
    church_name VARCHAR,
    period_id BIGINT,
    period_year INT,
    period_month INT,
    period_status VARCHAR,
    district_id BIGINT,
    district_name VARCHAR,
    region_id BIGINT,
    region_name VARCHAR,
    pastor_id BIGINT,
    pastor_name TEXT,
    church_type VARCHAR,
    submission_status VARCHAR,
    summary_only BOOLEAN,
    acms_balance NUMERIC,
    bank_statement_balance NUMERIC,
    bank_deposit NUMERIC,
    local_cash NUMERIC,
    usd_amount NUMERIC,
    exchange_rate NUMERIC,
    usd_amount_bs NUMERIC,
    documents_total NUMERIC,
    total_amount NUMERIC,
    difference NUMERIC,
    difference_status VARCHAR,
    count_date DATE,
    treasurer_name VARCHAR,
    elder_name VARCHAR,
    signed_by_pastor BOOLEAN,
    signed_by_elder BOOLEAN,
    observation VARCHAR,
    attachment_count INT,
    correction_count INT,
    created_by_id BIGINT,
    created_by_full_name VARCHAR,
    created_at TIMESTAMPTZ,
    updated_by_id BIGINT,
    updated_by_full_name VARCHAR,
    updated_at TIMESTAMPTZ,
    total_records INT
)
LANGUAGE sql
STABLE
AS $$
    SELECT
        lfc.local_fund_count_id,
        lfc.church_id,
        c.code AS church_code,
        c.name AS church_name,
        lfc.period_id,
        pe.year AS period_year,
        pe.month AS period_month,
        pe.status AS period_status,
        lfc.district_id,
        d.name AS district_name,
        r.region_id,
        r.name AS region_name,
        lfc.pastor_id,
        (pa.last_names || ', ' || pa.first_names) AS pastor_name,
        lfc.church_type,
        lfc.submission_status,
        lfc.summary_only,
        lfc.acms_balance,
        lfc.bank_statement_balance,
        lfc.bank_deposit,
        lfc.local_cash,
        lfc.usd_amount,
        lfc.exchange_rate,
        lfc.usd_amount_bs,
        lfc.documents_total,
        lfc.total_amount,
        lfc.difference,
        lfc.difference_status,
        lfc.count_date,
        lfc.treasurer_name,
        lfc.elder_name,
        lfc.signed_by_pastor,
        lfc.signed_by_elder,
        lfc.observation,
        CAST((SELECT COUNT(*) FROM local_fund_count_attachment a WHERE a.local_fund_count_id = lfc.local_fund_count_id) AS INT) AS attachment_count,
        CAST((SELECT COUNT(*) FROM local_fund_count_correction cr WHERE cr.local_fund_count_id = lfc.local_fund_count_id) AS INT) AS correction_count,
        lfc.created_by AS created_by_id,
        cu.full_name AS created_by_full_name,
        lfc.created_at AT TIME ZONE 'UTC' AS created_at,
        lfc.updated_by AS updated_by_id,
        uu.full_name AS updated_by_full_name,
        lfc.updated_at AT TIME ZONE 'UTC' AS updated_at,
        CAST(COUNT(*) OVER() AS INT) AS total_records
    FROM local_fund_count lfc
    JOIN church c ON c.church_id = lfc.church_id
    JOIN period pe ON pe.period_id = lfc.period_id
    JOIN district d ON d.district_id = lfc.district_id
    JOIN region r ON r.region_id = d.region_id
    LEFT JOIN pastor pa ON pa.pastor_id = lfc.pastor_id
    LEFT JOIN app_user cu ON cu.app_user_id = lfc.created_by
    LEFT JOIN app_user uu ON uu.app_user_id = lfc.updated_by
    WHERE (p_restricted_district_id IS NULL OR lfc.district_id = p_restricted_district_id)
      AND (p_local_fund_count_id IS NULL OR lfc.local_fund_count_id = p_local_fund_count_id)
      AND (p_period_id IS NULL OR lfc.period_id = p_period_id)
      AND (p_year IS NULL OR pe.year = p_year)
      AND (p_church_id IS NULL OR lfc.church_id = p_church_id)
      AND (p_district_id IS NULL OR lfc.district_id = p_district_id)
      AND (p_region_id IS NULL OR d.region_id = p_region_id)
      AND (p_pastor_id IS NULL OR lfc.pastor_id = p_pastor_id)
      AND (p_submission_status IS NULL OR lfc.submission_status = p_submission_status)
      AND (p_difference_status IS NULL OR lfc.difference_status = p_difference_status)
      AND (p_search IS NULL OR c.name ILIKE '%' || p_search || '%' OR c.code ILIKE '%' || p_search || '%')
    ORDER BY pe.year DESC, pe.month DESC, c.name
    LIMIT p_page_size OFFSET (p_page_number - 1) * p_page_size;
$$;

DROP FUNCTION IF EXISTS sp_get_period_counts;
CREATE OR REPLACE FUNCTION sp_get_period_counts(
    p_period_id BIGINT,
    p_region_id BIGINT,
    p_district_id BIGINT,
    p_restricted_district_id BIGINT
)
RETURNS TABLE (
    church_id BIGINT,
    church_code VARCHAR,
    church_name VARCHAR,
    church_type VARCHAR,
    district_id BIGINT,
    district_name VARCHAR,
    region_id BIGINT,
    region_name VARCHAR,
    pastor_id BIGINT,
    pastor_name TEXT,
    period_year INT,
    period_month INT,
    period_status VARCHAR,
    local_fund_count_id BIGINT,
    count_church_type VARCHAR,
    submission_status VARCHAR,
    summary_only BOOLEAN,
    acms_balance NUMERIC,
    bank_statement_balance NUMERIC,
    bank_deposit NUMERIC,
    local_cash NUMERIC,
    usd_amount NUMERIC,
    exchange_rate NUMERIC,
    usd_amount_bs NUMERIC,
    documents_total NUMERIC,
    total_amount NUMERIC,
    difference NUMERIC,
    difference_status VARCHAR,
    count_date DATE,
    treasurer_name VARCHAR,
    elder_name VARCHAR,
    signed_by_pastor BOOLEAN,
    signed_by_elder BOOLEAN,
    observation VARCHAR,
    attachment_count INT,
    correction_count INT,
    created_by_id BIGINT,
    created_by_full_name VARCHAR,
    created_at TIMESTAMPTZ,
    updated_by_id BIGINT,
    updated_by_full_name VARCHAR,
    updated_at TIMESTAMPTZ,
    filial_count INT
)
LANGUAGE sql
STABLE
AS $$
    SELECT
        c.church_id,
        c.code AS church_code,
        c.name AS church_name,
        c.type AS church_type,
        d.district_id,
        d.name AS district_name,
        r.region_id,
        r.name AS region_name,
        COALESCE(lfc.pastor_id, cp.pastor_id) AS pastor_id,
        COALESCE(pa.last_names || ', ' || pa.first_names, cp.last_names || ', ' || cp.first_names) AS pastor_name,
        pe.year AS period_year,
        pe.month AS period_month,
        pe.status AS period_status,
        lfc.local_fund_count_id,
        lfc.church_type AS count_church_type,
        lfc.submission_status,
        lfc.summary_only,
        lfc.acms_balance,
        lfc.bank_statement_balance,
        lfc.bank_deposit,
        lfc.local_cash,
        lfc.usd_amount,
        lfc.exchange_rate,
        lfc.usd_amount_bs,
        lfc.documents_total,
        lfc.total_amount,
        lfc.difference,
        lfc.difference_status,
        lfc.count_date,
        lfc.treasurer_name,
        lfc.elder_name,
        lfc.signed_by_pastor,
        lfc.signed_by_elder,
        lfc.observation,
        CAST((SELECT COUNT(*) FROM local_fund_count_attachment a WHERE a.local_fund_count_id = lfc.local_fund_count_id) AS INT) AS attachment_count,
        CAST((SELECT COUNT(*) FROM local_fund_count_correction cr WHERE cr.local_fund_count_id = lfc.local_fund_count_id) AS INT) AS correction_count,
        lfc.created_by AS created_by_id,
        cu.full_name AS created_by_full_name,
        lfc.created_at AT TIME ZONE 'UTC' AS created_at,
        lfc.updated_by AS updated_by_id,
        uu.full_name AS updated_by_full_name,
        lfc.updated_at AT TIME ZONE 'UTC' AS updated_at,
        CAST((SELECT COUNT(*) FROM church f WHERE f.parent_church_id = c.church_id AND f.status = 'active') AS INT) AS filial_count
    FROM church c
    CROSS JOIN period pe
    LEFT JOIN local_fund_count lfc ON lfc.church_id = c.church_id AND lfc.period_id = pe.period_id
    JOIN district d ON d.district_id = COALESCE(lfc.district_id, c.district_id)
    JOIN region r ON r.region_id = d.region_id
    LEFT JOIN pastor pa ON pa.pastor_id = lfc.pastor_id
    LEFT JOIN LATERAL (
        SELECT p.pastor_id, p.last_names, p.first_names
        FROM district_pastor dp
        JOIN pastor p ON p.pastor_id = dp.pastor_id
        WHERE dp.district_id = d.district_id AND dp.end_date IS NULL
    ) cp ON TRUE
    LEFT JOIN app_user cu ON cu.app_user_id = lfc.created_by
    LEFT JOIN app_user uu ON uu.app_user_id = lfc.updated_by
    WHERE pe.period_id = p_period_id
      AND ((c.status = 'active' AND c.type <> 'Filial') OR lfc.local_fund_count_id IS NOT NULL)
      AND (p_restricted_district_id IS NULL OR d.district_id = p_restricted_district_id)
      AND (p_district_id IS NULL OR d.district_id = p_district_id)
      AND (p_region_id IS NULL OR r.region_id = p_region_id)
    ORDER BY r.name, d.name, c.name;
$$;

DROP FUNCTION IF EXISTS sp_report_local_fund_year;
CREATE OR REPLACE FUNCTION sp_report_local_fund_year(
    p_year INT,
    p_month_from INT,
    p_month_to INT,
    p_region_id BIGINT,
    p_district_id BIGINT,
    p_restricted_district_id BIGINT,
    p_church_id BIGINT
)
RETURNS TABLE (
    region_name VARCHAR,
    district_name VARCHAR,
    pastor_name TEXT,
    church_id BIGINT,
    church_code VARCHAR,
    church_name VARCHAR,
    church_type VARCHAR,
    month INT,
    submission_status VARCHAR,
    acms_balance NUMERIC,
    total_amount NUMERIC,
    difference NUMERIC,
    difference_status VARCHAR
)
LANGUAGE sql
STABLE
AS $$
    WITH year_counts AS (
        SELECT lfc.*, pe.month
        FROM local_fund_count lfc
        JOIN period pe ON pe.period_id = lfc.period_id
        WHERE pe.year = p_year AND pe.month BETWEEN p_month_from AND p_month_to
    ),
    churches AS (
        SELECT c.church_id, c.code, c.name, c.type, d.district_id, d.name AS district_name, r.region_id, r.name AS region_name
        FROM church c
        JOIN district d ON d.district_id = c.district_id
        JOIN region r ON r.region_id = d.region_id
        WHERE c.type <> 'Filial'
          AND (c.status = 'active' OR EXISTS (SELECT 1 FROM year_counts y WHERE y.church_id = c.church_id))
          AND (p_restricted_district_id IS NULL OR c.district_id = p_restricted_district_id)
          AND (p_district_id IS NULL OR c.district_id = p_district_id)
          AND (p_region_id IS NULL OR r.region_id = p_region_id)
          AND (p_church_id IS NULL OR c.church_id = p_church_id)
    )
    SELECT
        ch.region_name,
        ch.district_name,
        (p.last_names || ', ' || p.first_names) AS pastor_name,
        ch.church_id,
        ch.code AS church_code,
        ch.name AS church_name,
        ch.type AS church_type,
        y.month,
        y.submission_status,
        y.acms_balance,
        y.total_amount,
        y.difference,
        y.difference_status
    FROM churches ch
    LEFT JOIN year_counts y ON y.church_id = ch.church_id
    LEFT JOIN district_pastor dp ON dp.district_id = ch.district_id AND dp.end_date IS NULL
    LEFT JOIN pastor p ON p.pastor_id = dp.pastor_id
    ORDER BY ch.region_name, ch.district_name, ch.name, y.month;
$$;

DROP FUNCTION IF EXISTS sp_report_pending;
CREATE OR REPLACE FUNCTION sp_report_pending(
    p_period_id BIGINT,
    p_region_id BIGINT,
    p_district_id BIGINT,
    p_restricted_district_id BIGINT,
    p_church_id BIGINT
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
      AND (p_church_id IS NULL OR c.church_id = p_church_id)
    ORDER BY r.name, d.name, c.name;
$$;

DROP FUNCTION IF EXISTS sp_report_differences;
CREATE OR REPLACE FUNCTION sp_report_differences(
    p_year INT,
    p_status VARCHAR,
    p_region_id BIGINT,
    p_district_id BIGINT,
    p_restricted_district_id BIGINT,
    p_church_id BIGINT
)
RETURNS TABLE (
    region_name VARCHAR,
    district_name VARCHAR,
    pastor_name TEXT,
    church_code VARCHAR,
    church_name VARCHAR,
    church_type VARCHAR,
    period_month INT,
    acms_balance NUMERIC,
    total_amount NUMERIC,
    difference NUMERIC,
    difference_status VARCHAR,
    observation VARCHAR,
    registered_at TIMESTAMPTZ,
    last_follow_up_at TIMESTAMPTZ,
    last_justification VARCHAR,
    last_follow_up_by VARCHAR,
    resolved_at TIMESTAMPTZ
)
LANGUAGE sql
STABLE
AS $$
    SELECT
        r.name AS region_name,
        d.name AS district_name,
        (pa.last_names || ', ' || pa.first_names) AS pastor_name,
        c.code AS church_code,
        c.name AS church_name,
        lfc.church_type,
        pe.month AS period_month,
        lfc.acms_balance,
        lfc.total_amount,
        lfc.difference,
        lfc.difference_status,
        lfc.observation,
        lfc.created_at AT TIME ZONE 'UTC' AS registered_at,
        lf.followed_up_at AT TIME ZONE 'UTC' AS last_follow_up_at,
        lf.justification AS last_justification,
        fu.full_name AS last_follow_up_by,
        rs.followed_up_at AT TIME ZONE 'UTC' AS resolved_at
    FROM local_fund_count lfc
    JOIN church c ON c.church_id = lfc.church_id
    JOIN period pe ON pe.period_id = lfc.period_id
    JOIN district d ON d.district_id = lfc.district_id
    JOIN region r ON r.region_id = d.region_id
    LEFT JOIN pastor pa ON pa.pastor_id = lfc.pastor_id
    LEFT JOIN LATERAL (
        SELECT f.followed_up_at, f.justification, f.app_user_id
        FROM difference_follow_up f
        WHERE f.local_fund_count_id = lfc.local_fund_count_id
        ORDER BY f.followed_up_at DESC, f.difference_follow_up_id DESC
        LIMIT 1
    ) lf ON TRUE
    LEFT JOIN app_user fu ON fu.app_user_id = lf.app_user_id
    LEFT JOIN LATERAL (
        SELECT f.followed_up_at
        FROM difference_follow_up f
        WHERE f.local_fund_count_id = lfc.local_fund_count_id AND f.new_status = 'resolved'
        ORDER BY f.followed_up_at DESC
        LIMIT 1
    ) rs ON TRUE
    WHERE lfc.submission_status = 'submitted'
      AND lfc.difference <> 0
      AND pe.year = p_year
      AND (p_status IS NULL OR lfc.difference_status = p_status)
      AND (p_restricted_district_id IS NULL OR lfc.district_id = p_restricted_district_id)
      AND (p_district_id IS NULL OR lfc.district_id = p_district_id)
      AND (p_region_id IS NULL OR r.region_id = p_region_id)
      AND (p_church_id IS NULL OR c.church_id = p_church_id)
    ORDER BY r.name, d.name, c.name, pe.month;
$$;

DROP FUNCTION IF EXISTS sp_report_negative_acms;
CREATE OR REPLACE FUNCTION sp_report_negative_acms(
    p_year INT,
    p_month_from INT,
    p_month_to INT,
    p_region_id BIGINT,
    p_district_id BIGINT,
    p_restricted_district_id BIGINT,
    p_church_id BIGINT
)
RETURNS TABLE (
    region_name VARCHAR,
    district_name VARCHAR,
    pastor_name TEXT,
    church_id BIGINT,
    church_code VARCHAR,
    church_name VARCHAR,
    church_type VARCHAR,
    period_month INT,
    acms_balance NUMERIC
)
LANGUAGE sql
STABLE
AS $$
    SELECT
        r.name AS region_name,
        d.name AS district_name,
        (pa.last_names || ', ' || pa.first_names) AS pastor_name,
        c.church_id,
        c.code AS church_code,
        c.name AS church_name,
        lfc.church_type,
        pe.month AS period_month,
        lfc.acms_balance
    FROM local_fund_count lfc
    JOIN church c ON c.church_id = lfc.church_id
    JOIN period pe ON pe.period_id = lfc.period_id
    JOIN district d ON d.district_id = lfc.district_id
    JOIN region r ON r.region_id = d.region_id
    LEFT JOIN pastor pa ON pa.pastor_id = lfc.pastor_id
    WHERE lfc.acms_balance < 0
      AND pe.year = p_year
      AND pe.month BETWEEN p_month_from AND p_month_to
      AND (p_restricted_district_id IS NULL OR lfc.district_id = p_restricted_district_id)
      AND (p_district_id IS NULL OR lfc.district_id = p_district_id)
      AND (p_region_id IS NULL OR r.region_id = p_region_id)
      AND (p_church_id IS NULL OR c.church_id = p_church_id)
    ORDER BY r.name, d.name, c.name, pe.month;
$$;

DROP FUNCTION IF EXISTS sp_report_corrections;
CREATE OR REPLACE FUNCTION sp_report_corrections(
    p_year INT,
    p_region_id BIGINT,
    p_district_id BIGINT,
    p_restricted_district_id BIGINT,
    p_church_id BIGINT
)
RETURNS TABLE (
    registered_at TIMESTAMPTZ,
    region_name VARCHAR,
    district_name VARCHAR,
    church_code VARCHAR,
    church_name VARCHAR,
    count_year INT,
    count_month INT,
    applied_year INT,
    applied_month INT,
    field_name VARCHAR,
    previous_amount NUMERIC,
    corrected_amount NUMERIC,
    reason VARCHAR,
    app_user_full_name VARCHAR
)
LANGUAGE sql
STABLE
AS $$
    SELECT
        cr.registered_at AT TIME ZONE 'UTC' AS registered_at,
        r.name AS region_name,
        d.name AS district_name,
        c.code AS church_code,
        c.name AS church_name,
        cp.year AS count_year,
        cp.month AS count_month,
        ap.year AS applied_year,
        ap.month AS applied_month,
        cr.field_name,
        cr.previous_amount,
        cr.corrected_amount,
        cr.reason,
        u.full_name AS app_user_full_name
    FROM local_fund_count_correction cr
    JOIN local_fund_count lfc ON lfc.local_fund_count_id = cr.local_fund_count_id
    JOIN church c ON c.church_id = lfc.church_id
    JOIN period cp ON cp.period_id = lfc.period_id
    JOIN period ap ON ap.period_id = cr.applied_period_id
    JOIN district d ON d.district_id = lfc.district_id
    JOIN region r ON r.region_id = d.region_id
    LEFT JOIN app_user u ON u.app_user_id = cr.app_user_id
    WHERE ap.year = p_year
      AND (p_restricted_district_id IS NULL OR lfc.district_id = p_restricted_district_id)
      AND (p_district_id IS NULL OR lfc.district_id = p_district_id)
      AND (p_region_id IS NULL OR r.region_id = p_region_id)
      AND (p_church_id IS NULL OR c.church_id = p_church_id)
    ORDER BY cr.registered_at, c.name;
$$;

DROP FUNCTION IF EXISTS sp_report_church_movements;
CREATE OR REPLACE FUNCTION sp_report_church_movements(
    p_date_from DATE,
    p_date_to DATE,
    p_change_type VARCHAR,
    p_region_id BIGINT,
    p_district_id BIGINT,
    p_restricted_district_id BIGINT,
    p_church_id BIGINT
)
RETURNS TABLE (
    changed_at DATE,
    change_type VARCHAR,
    region_name VARCHAR,
    district_name VARCHAR,
    church_code VARCHAR,
    church_name VARCHAR,
    church_type VARCHAR,
    old_value VARCHAR,
    new_value VARCHAR,
    reason VARCHAR,
    app_user_full_name VARCHAR
)
LANGUAGE sql
STABLE
AS $$
    SELECT
        h.changed_at,
        h.change_type,
        r.name AS region_name,
        d.name AS district_name,
        c.code AS church_code,
        c.name AS church_name,
        c.type AS church_type,
        h.old_value,
        h.new_value,
        h.reason,
        u.full_name AS app_user_full_name
    FROM church_history h
    JOIN church c ON c.church_id = h.church_id
    JOIN district d ON d.district_id = c.district_id
    JOIN region r ON r.region_id = d.region_id
    LEFT JOIN app_user u ON u.app_user_id = h.app_user_id
    WHERE h.changed_at BETWEEN p_date_from AND p_date_to
      AND (p_change_type IS NULL OR h.change_type = p_change_type)
      AND (p_restricted_district_id IS NULL OR c.district_id = p_restricted_district_id)
      AND (p_district_id IS NULL OR c.district_id = p_district_id)
      AND (p_region_id IS NULL OR r.region_id = p_region_id)
      AND (p_church_id IS NULL OR c.church_id = p_church_id)
    ORDER BY h.changed_at, c.name;
$$;

COMMIT;
