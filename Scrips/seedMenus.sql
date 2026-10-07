-- =====================================================================
-- Seed data for the dynamic sidebar menu (menu table). Add new rows
-- here as new frontend sections get built; parent_menu_id nests items.
-- A menu is visible to a role only if seedMenuRoles.sql gives it a row.
-- =====================================================================

INSERT INTO menu (name, translation_key, route, icon, parent_menu_id, display_order, is_active) VALUES
('Inicio', 'nav.home', '/', 'home', NULL, 1, TRUE)
ON CONFLICT (route) DO NOTHING;

-- Grupos (sin ruta propia).
INSERT INTO menu (name, translation_key, route, icon, parent_menu_id, display_order, is_active)
SELECT v.name, v.translation_key, NULL, v.icon, NULL, v.display_order, TRUE
FROM (VALUES
    ('Arqueos', 'nav.funds', 'cash', 2),
    ('Catálogos', 'nav.catalogs', 'catalog', 3),
    ('Administración', 'nav.admin', 'settings', 99)
) AS v(name, translation_key, icon, display_order)
WHERE NOT EXISTS (SELECT 1 FROM menu m WHERE m.name = v.name AND m.parent_menu_id IS NULL);

INSERT INTO menu (name, translation_key, route, icon, parent_menu_id, display_order, is_active)
SELECT v.name, v.translation_key, v.route, v.icon, m.menu_id, v.display_order, TRUE
FROM menu m
JOIN (VALUES
    ('Arqueos', 'Registro de arqueos', 'nav.localFundCounts', '/funds/counts', 'cash', 1),
    ('Arqueos', 'Diferencias', 'nav.differences', '/funds/differences', 'alert', 2),
    ('Catálogos', 'Regiones', 'nav.regions', '/catalogs/regions', 'map', 1),
    ('Catálogos', 'Distritos', 'nav.districts', '/catalogs/districts', 'map-pin', 2),
    ('Catálogos', 'Pastores', 'nav.pastors', '/catalogs/pastors', 'user', 3),
    ('Catálogos', 'Iglesias', 'nav.churches', '/catalogs/churches', 'church', 4),
    ('Catálogos', 'Períodos', 'nav.periods', '/catalogs/periods', 'calendar', 5),
    ('Administración', 'Usuarios', 'nav.users', '/admin/users', 'users', 1),
    ('Administración', 'Roles', 'nav.roles', '/admin/roles', 'shield', 2),
    ('Administración', 'Auditoría', 'nav.auditLogs', '/admin/audit-logs', 'history', 3)
) AS v(parent_name, name, translation_key, route, icon, display_order)
  ON m.name = v.parent_name AND m.parent_menu_id IS NULL
ON CONFLICT (route) DO NOTHING;
