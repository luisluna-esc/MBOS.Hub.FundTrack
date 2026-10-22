-- =====================================================================
-- Migración 05: reportes.
-- 1. Funciones de reportes (están en storedProcedures.sql, desde
--    "Reporte Fondo Local del año"): se aplican con
--    psql -f storedProcedures.sql o solo esa parte.
-- 2. Menú "Reportes" con sus 8 pantallas, visible para todos los roles:
--    el backend deja ver los reportes a cualquier usuario con sesión y al
--    Pastor solo de su distrito.
-- Se puede correr más de una vez.
-- =====================================================================

BEGIN;

INSERT INTO menu (name, translation_key, route, icon, parent_menu_id, display_order, is_active)
SELECT 'Reportes', 'nav.reports', NULL, 'report', NULL, 4, TRUE
WHERE NOT EXISTS (SELECT 1 FROM menu m WHERE m.name = 'Reportes' AND m.parent_menu_id IS NULL);

INSERT INTO menu (name, translation_key, route, icon, parent_menu_id, display_order, is_active)
SELECT v.name, v.translation_key, v.route, 'report', m.menu_id, v.display_order, TRUE
FROM menu m
JOIN (VALUES
    ('Arqueo del fondo local', 'nav.reportLocalFundCount', '/reports/local-fund-count', 0),
    ('Fondo Local anual', 'nav.reportLocalFund', '/reports/local-fund', 1),
    ('Pendientes del mes', 'nav.reportPending', '/reports/pending', 2),
    ('Diferencias por estado', 'nav.reportDifferences', '/reports/differences', 3),
    ('Cumplimiento', 'nav.reportCompliance', '/reports/compliance', 4),
    ('Saldo ACMS negativo', 'nav.reportNegativeAcms', '/reports/negative-acms', 5),
    ('Resumen por región', 'nav.reportRegionSummary', '/reports/region-summary', 6),
    ('Correcciones', 'nav.reportCorrections', '/reports/corrections', 7),
    ('Movimientos de iglesias', 'nav.reportChurchMovements', '/reports/church-movements', 8)
) AS v(name, translation_key, route, display_order) ON TRUE
WHERE m.name = 'Reportes' AND m.parent_menu_id IS NULL
ON CONFLICT (route) DO NOTHING;

-- Todos los roles activos ven el grupo y sus reportes.
INSERT INTO menu_role (menu_id, role_id)
SELECT m.menu_id, r.role_id
FROM menu m
LEFT JOIN menu p ON p.menu_id = m.parent_menu_id
CROSS JOIN role r
WHERE r.is_active
  AND ((m.name = 'Reportes' AND m.parent_menu_id IS NULL) OR p.name = 'Reportes')
ON CONFLICT DO NOTHING;

COMMIT;
