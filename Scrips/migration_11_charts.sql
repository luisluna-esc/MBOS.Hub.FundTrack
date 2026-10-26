-- Migración 11: nuevo menú "Gráficas" (para todos los roles) y la función sp_chart_counts que lo
-- alimenta. No cambia tablas.
BEGIN;

-- ---------------------------------------------------------------------
-- Gráficas: por cada mes del año y cada región, cuántas iglesias debían presentar arqueo y en qué
-- quedó cada una (sin diferencia, con diferencia, no presentó, sin registrar), con los montos.
-- Mismo criterio que el registro del mes: Iglesias y Grupos activos, más los que ya tienen arqueo.
-- La pantalla suma estas filas por mes y por región.
-- ---------------------------------------------------------------------
DROP FUNCTION IF EXISTS sp_chart_counts;
CREATE OR REPLACE FUNCTION sp_chart_counts(
    p_year INT,
    p_region_id BIGINT,
    p_district_id BIGINT,
    p_restricted_district_id BIGINT
)
RETURNS TABLE (
    period_month INT,
    region_id BIGINT,
    region_name TEXT,
    expected INT,
    no_difference INT,
    with_difference INT,
    not_submitted INT,
    missing INT,
    acms_submitted NUMERIC,
    custody_total NUMERIC,
    shortage NUMERIC,
    surplus NUMERIC
)
LANGUAGE sql
STABLE
AS $$
    SELECT
        pe.month,
        r.region_id,
        r.name::TEXT,
        CAST(COUNT(*) AS INT),
        CAST(COUNT(*) FILTER (WHERE lfc.submission_status = 'submitted' AND lfc.difference = 0) AS INT),
        CAST(COUNT(*) FILTER (WHERE lfc.submission_status = 'submitted' AND lfc.difference <> 0) AS INT),
        CAST(COUNT(*) FILTER (WHERE lfc.submission_status = 'not_submitted') AS INT),
        CAST(COUNT(*) FILTER (WHERE lfc.local_fund_count_id IS NULL) AS INT),
        COALESCE(SUM(lfc.acms_balance) FILTER (WHERE lfc.submission_status = 'submitted'), 0),
        COALESCE(SUM(lfc.total_amount) FILTER (WHERE lfc.submission_status = 'submitted'), 0),
        COALESCE(SUM(lfc.difference) FILTER (WHERE lfc.submission_status = 'submitted' AND lfc.difference < 0), 0),
        COALESCE(SUM(lfc.difference) FILTER (WHERE lfc.submission_status = 'submitted' AND lfc.difference > 0), 0)
    FROM period pe
    CROSS JOIN church c
    LEFT JOIN local_fund_count lfc ON lfc.church_id = c.church_id AND lfc.period_id = pe.period_id
    JOIN district d ON d.district_id = COALESCE(lfc.district_id, c.district_id)
    JOIN region r ON r.region_id = d.region_id
    WHERE pe.year = p_year
      AND ((c.status = 'active' AND c.type <> 'Filial') OR lfc.local_fund_count_id IS NOT NULL)
      AND (p_restricted_district_id IS NULL OR d.district_id = p_restricted_district_id)
      AND (p_district_id IS NULL OR d.district_id = p_district_id)
      AND (p_region_id IS NULL OR r.region_id = p_region_id)
    GROUP BY pe.month, r.region_id, r.name
    ORDER BY pe.month, r.name;
$$;

INSERT INTO menu (name, translation_key, route, icon, parent_menu_id, display_order, is_active)
SELECT 'Gráficas', 'nav.charts', '/charts', 'chart', NULL, 5, TRUE
WHERE NOT EXISTS (SELECT 1 FROM menu m WHERE m.route = '/charts');

INSERT INTO menu_role (menu_id, role_id)
SELECT m.menu_id, r.role_id
FROM menu m
CROSS JOIN role r
WHERE r.is_active AND m.route = '/charts'
ON CONFLICT DO NOTHING;

COMMIT;
