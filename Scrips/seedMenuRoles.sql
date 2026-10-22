-- =====================================================================
-- Seed for menu_role. A sidebar menu item is visible to a role only if
-- there is a row here for that (menu, role) pair. A menu with no rows is
-- not visible to anyone. A group (menu without route) also needs its own
-- row, and is shown only if at least one of its children is visible.
-- =====================================================================

-- M-BOS: ve todos los menús.
INSERT INTO menu_role (menu_id, role_id)
SELECT m.menu_id, r.role_id FROM menu m, role r
WHERE r.name = 'M-BOS'
ON CONFLICT DO NOTHING;

-- Inicio, Arqueos y Catálogos: todos los roles.
INSERT INTO menu_role (menu_id, role_id)
SELECT m.menu_id, r.role_id
FROM menu m
LEFT JOIN menu p ON p.menu_id = m.parent_menu_id
CROSS JOIN role r
WHERE r.is_active
  AND (m.route = '/' OR m.name IN ('Arqueos', 'Catálogos', 'Reportes') AND m.parent_menu_id IS NULL OR p.name IN ('Arqueos', 'Catálogos', 'Reportes'))
ON CONFLICT DO NOTHING;

-- Auditoría: también Analista y Administrador (el grupo Administración
-- les aparece solo con esa opción adentro).
INSERT INTO menu_role (menu_id, role_id)
SELECT m.menu_id, r.role_id FROM menu m, role r
WHERE (m.route = '/admin/audit-logs' OR m.name = 'Administración' AND m.parent_menu_id IS NULL)
  AND r.name IN ('Analista', 'Administrador')
ON CONFLICT DO NOTHING;
