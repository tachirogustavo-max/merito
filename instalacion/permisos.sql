-- Permisos mínimos por tabla. Lo aplica aplicar-permisos.ps1, que sustituye {BD}, {SERVICIO}, {RESPALDO} y {EQUIPO}.
-- La cuenta del servicio no puede crear ni alterar tablas, ni borrar historial, auditoría o publicaciones.

REVOKE ALL PRIVILEGES, GRANT OPTION FROM '{SERVICIO}'@'{EQUIPO}';

GRANT SELECT ON `{BD}`.versiones_esquema TO '{SERVICIO}'@'{EQUIPO}';
GRANT SELECT, DELETE ON `{BD}`.codigo_configuracion TO '{SERVICIO}'@'{EQUIPO}';
-- Cuentas: la cédula, los nombres y el rol no se modifican después del alta.
GRANT SELECT, INSERT ON `{BD}`.usuarios TO '{SERVICIO}'@'{EQUIPO}';
-- El correo lo corrige administración o la matrícula mientras la cuenta no esté registrada.
GRANT UPDATE (clave_hash, activo, correo) ON `{BD}`.usuarios TO '{SERVICIO}'@'{EQUIPO}';
GRANT SELECT, INSERT, DELETE ON `{BD}`.sesiones TO '{SERVICIO}'@'{EQUIPO}';
GRANT SELECT, INSERT, DELETE ON `{BD}`.codigos_acceso TO '{SERVICIO}'@'{EQUIPO}';
-- Códigos enviados por correo: se crean, se cuentan los intentos y se borran al usarse.
GRANT SELECT, INSERT, DELETE ON `{BD}`.verificaciones_correo TO '{SERVICIO}'@'{EQUIPO}';
GRANT UPDATE (intentos) ON `{BD}`.verificaciones_correo TO '{SERVICIO}'@'{EQUIPO}';

-- Estructura académica: se crea y se corrige el nombre; no se borra.
-- Una carga sin evaluaciones, inscripciones ni publicaciones sí puede retirarse.
GRANT SELECT, INSERT ON `{BD}`.periodos TO '{SERVICIO}'@'{EQUIPO}';
GRANT UPDATE (nombre, cerrado, cerrado_en, redondeo_cierre) ON `{BD}`.periodos TO '{SERVICIO}'@'{EQUIPO}';
GRANT SELECT, INSERT ON `{BD}`.carreras TO '{SERVICIO}'@'{EQUIPO}';
GRANT UPDATE (nombre, codigo) ON `{BD}`.carreras TO '{SERVICIO}'@'{EQUIPO}';
-- Formato del código de sección y reglas de calificación: se leen y se cambian, no se crean claves nuevas.
GRANT SELECT, UPDATE (valor, actualizado_en) ON `{BD}`.configuracion TO '{SERVICIO}'@'{EQUIPO}';
GRANT SELECT, INSERT ON `{BD}`.materias TO '{SERVICIO}'@'{EQUIPO}';
GRANT UPDATE (codigo, nombre, uc, nota_aprobatoria) ON `{BD}`.materias TO '{SERVICIO}'@'{EQUIPO}';
-- Pensum: una materia entra y sale del pensum de una carrera; no sale si ya tiene asignaciones (lo impide la clave foránea).
GRANT SELECT, INSERT, DELETE ON `{BD}`.pensum TO '{SERVICIO}'@'{EQUIPO}';
GRANT UPDATE (semestre) ON `{BD}`.pensum TO '{SERVICIO}'@'{EQUIPO}';
GRANT SELECT, INSERT ON `{BD}`.secciones TO '{SERVICIO}'@'{EQUIPO}';
GRANT UPDATE (nombre, semestre, codigo) ON `{BD}`.secciones TO '{SERVICIO}'@'{EQUIPO}';
GRANT SELECT, INSERT, DELETE ON `{BD}`.ofertas TO '{SERVICIO}'@'{EQUIPO}';
GRANT UPDATE (docente_id, revision, uc_cierre, aprobatoria_cierre) ON `{BD}`.ofertas TO '{SERVICIO}'@'{EQUIPO}';

-- Matrícula: una propuesta pendiente puede confirmarse o rechazarse.
GRANT SELECT, INSERT, DELETE ON `{BD}`.inscripciones TO '{SERVICIO}'@'{EQUIPO}';
GRANT UPDATE (estado, aprobada_por) ON `{BD}`.inscripciones TO '{SERVICIO}'@'{EQUIPO}';

-- Plan de evaluación y borrador de notas.
GRANT SELECT, INSERT, DELETE ON `{BD}`.evaluaciones TO '{SERVICIO}'@'{EQUIPO}';
GRANT UPDATE (nombre, peso) ON `{BD}`.evaluaciones TO '{SERVICIO}'@'{EQUIPO}';
GRANT SELECT, INSERT, DELETE ON `{BD}`.notas TO '{SERVICIO}'@'{EQUIPO}';
GRANT UPDATE (valor, actualizado_en) ON `{BD}`.notas TO '{SERVICIO}'@'{EQUIPO}';

-- Lo publicado y el historial son de solo agregar.
GRANT SELECT, INSERT ON `{BD}`.publicaciones TO '{SERVICIO}'@'{EQUIPO}';
GRANT SELECT, INSERT ON `{BD}`.resultados_publicados TO '{SERVICIO}'@'{EQUIPO}';
GRANT SELECT, INSERT ON `{BD}`.cambios_notas TO '{SERVICIO}'@'{EQUIPO}';
GRANT SELECT, INSERT ON `{BD}`.auditoria TO '{SERVICIO}'@'{EQUIPO}';

-- Respaldo: solo lectura y la vista coherente que necesita mariadb-dump --single-transaction.
REVOKE ALL PRIVILEGES, GRANT OPTION FROM '{RESPALDO}'@'{EQUIPO}';
GRANT SELECT, SHOW VIEW, TRIGGER, EVENT, LOCK TABLES ON `{BD}`.* TO '{RESPALDO}'@'{EQUIPO}';
