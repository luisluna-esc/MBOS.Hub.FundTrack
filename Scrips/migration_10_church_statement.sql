-- Migración 10: nuevo reporte "Reporte por iglesia" (un mes por fila, con el arqueo de la iglesia
-- en el rango elegido). Agrega la función sp_report_church_statement y su opción en el menú de
-- Reportes para todos los roles. No cambia tablas.
BEGIN;

-- ---------------------------------------------------------------------
-- Reporte por iglesia: una fila por mes del rango elegido (aunque no tenga arqueo), con su saldo
-- ACMS, el detalle del arqueo y la diferencia. El Pastor solo ve las iglesias de su distrito.
-- ---------------------------------------------------------------------
DROP FUNCTION IF EXISTS sp_report_church_statement;
CREATE OR REPLACE FUNCTION sp_report_church_statement(
    p_church_id BIGINT,
    p_from_period_id BIGINT,
    p_to_period_id BIGINT,
    p_restricted_district_id BIGINT
)
RETURNS TABLE (
    church_code TEXT,
    church_name TEXT,
    church_type TEXT,
    district_name TEXT,
    region_name TEXT,
    pastor_name TEXT,
    period_year INT,
    period_month INT,
    period_status TEXT,
    has_count BOOLEAN,
    submission_status TEXT,
    summary_only BOOLEAN,
    acms_balance NUMERIC,
    bank_statement_balance NUMERIC,
    bank_deposit NUMERIC,
    local_cash NUMERIC,
    usd_amount_bs NUMERIC,
    documents_total NUMERIC,
    total_amount NUMERIC,
    difference NUMERIC,
    difference_status TEXT,
    count_date DATE,
    observation TEXT,
    correction_count INT
)
LANGUAGE sql
STABLE
AS $$
    WITH bounds AS (
        SELECT f.year * 12 + f.month AS from_key, t.year * 12 + t.month AS to_key
        FROM period f, period t
        WHERE f.period_id = p_from_period_id AND t.period_id = p_to_period_id
    )
    SELECT
        c.code::TEXT,
        c.name::TEXT,
        c.type::TEXT,
        d.name::TEXT,
        r.name::TEXT,
        COALESCE(pa.last_names || ', ' || pa.first_names, cp.last_names || ', ' || cp.first_names)::TEXT,
        pe.year,
        pe.month,
        pe.status::TEXT,
        lfc.local_fund_count_id IS NOT NULL,
        lfc.submission_status::TEXT,
        COALESCE(lfc.summary_only, FALSE),
        COALESCE(lfc.acms_balance, 0),
        COALESCE(lfc.bank_statement_balance, 0),
        COALESCE(lfc.bank_deposit, 0),
        COALESCE(lfc.local_cash, 0),
        COALESCE(lfc.usd_amount_bs, 0),
        COALESCE(lfc.documents_total, 0),
        COALESCE(lfc.total_amount, 0),
        COALESCE(lfc.difference, 0),
        lfc.difference_status::TEXT,
        lfc.count_date,
        lfc.observation::TEXT,
        CAST((SELECT COUNT(*) FROM local_fund_count_correction cr WHERE cr.local_fund_count_id = lfc.local_fund_count_id) AS INT)
    FROM church c
    CROSS JOIN bounds b
    JOIN period pe ON pe.year * 12 + pe.month BETWEEN b.from_key AND b.to_key
    LEFT JOIN local_fund_count lfc ON lfc.church_id = c.church_id AND lfc.period_id = pe.period_id
    JOIN district d ON d.district_id = COALESCE(lfc.district_id, c.district_id)
    JOIN region r ON r.region_id = d.region_id
    LEFT JOIN pastor pa ON pa.pastor_id = lfc.pastor_id
    LEFT JOIN LATERAL (
        SELECT p.last_names, p.first_names
        FROM district_pastor dp
        JOIN pastor p ON p.pastor_id = dp.pastor_id
        WHERE dp.district_id = d.district_id AND dp.end_date IS NULL
        LIMIT 1
    ) cp ON TRUE
    WHERE c.church_id = p_church_id
      AND (p_restricted_district_id IS NULL OR d.district_id = p_restricted_district_id)
    ORDER BY pe.year, pe.month;
$$;

INSERT INTO menu (name, translation_key, route, icon, parent_menu_id, display_order, is_active)
SELECT 'Reporte por iglesia', 'nav.reportChurchStatement', '/reports/church-statement', 'report', m.menu_id, 9, TRUE
FROM menu m
WHERE m.name = 'Reportes' AND m.parent_menu_id IS NULL
  AND NOT EXISTS (SELECT 1 FROM menu x WHERE x.route = '/reports/church-statement');

INSERT INTO menu_role (menu_id, role_id)
SELECT m.menu_id, r.role_id
FROM menu m
CROSS JOIN role r
WHERE r.is_active AND m.route = '/reports/church-statement'
ON CONFLICT DO NOTHING;

COMMIT;
