-- Código de sección con formato configurable, por ejemplo 02S-2603-D1 (semestre 2, carrera 2603, sección D1).
-- La sección guarda su semestre (0 si no aplica), su identificador en nombre y el código armado.

ALTER TABLE carreras ADD COLUMN IF NOT EXISTS codigo VARCHAR(20) NULL;
CREATE UNIQUE INDEX IF NOT EXISTS uq_carreras_codigo ON carreras (codigo);
ALTER TABLE carreras ADD CONSTRAINT IF NOT EXISTS ck_carreras_codigo CHECK (codigo IS NULL OR codigo <> '');

ALTER TABLE secciones ADD COLUMN IF NOT EXISTS semestre TINYINT NOT NULL DEFAULT 0;
ALTER TABLE secciones ADD COLUMN IF NOT EXISTS codigo VARCHAR(60) NOT NULL DEFAULT '';
UPDATE secciones SET codigo = nombre WHERE codigo = '';
CREATE UNIQUE INDEX IF NOT EXISTS uq_secciones_identificador ON secciones (carrera_id, semestre, nombre);
ALTER TABLE secciones DROP INDEX IF EXISTS uq_secciones_carrera;
CREATE UNIQUE INDEX IF NOT EXISTS uq_secciones_codigo ON secciones (carrera_id, codigo);
ALTER TABLE secciones ADD CONSTRAINT IF NOT EXISTS ck_secciones_semestre CHECK (semestre BETWEEN 0 AND 20);
ALTER TABLE secciones ADD CONSTRAINT IF NOT EXISTS ck_secciones_codigo CHECK (codigo <> '');

CREATE TABLE IF NOT EXISTS configuracion (
    clave VARCHAR(40) NOT NULL PRIMARY KEY,
    valor VARCHAR(200) NOT NULL,
    actualizado_en DATETIME(6) NOT NULL DEFAULT UTC_TIMESTAMP(6),
    CONSTRAINT ck_configuracion_clave CHECK (clave IN ('formato_seccion')),
    CONSTRAINT ck_configuracion_valor CHECK (valor <> '')
) ENGINE=InnoDB;

INSERT IGNORE INTO configuracion (clave, valor) VALUES ('formato_seccion', '{seccion}');
