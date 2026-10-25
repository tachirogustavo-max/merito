-- Registro y recuperación de contraseña con un código de seis cifras enviado al correo.
-- Del código solo se guarda la huella.

ALTER TABLE usuarios ADD COLUMN IF NOT EXISTS correo VARCHAR(254) NULL;
CREATE UNIQUE INDEX IF NOT EXISTS uq_usuarios_correo ON usuarios (correo);
-- En minúsculas (comparación binaria, no por intercalación) y con forma de correo.
ALTER TABLE usuarios ADD CONSTRAINT IF NOT EXISTS ck_usuarios_correo CHECK (correo IS NULL OR (
    CAST(correo AS BINARY) = CAST(LOWER(correo) AS BINARY) AND correo LIKE '_%@_%._%' AND correo NOT LIKE '% %' AND correo NOT LIKE '%@%@%'));

CREATE TABLE IF NOT EXISTS verificaciones_correo (
    usuario_id BIGINT NOT NULL PRIMARY KEY,
    huella CHAR(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    vence_en DATETIME(6) NOT NULL,
    intentos TINYINT NOT NULL DEFAULT 0,
    enviado_en DATETIME(6) NOT NULL DEFAULT UTC_TIMESTAMP(6),
    CONSTRAINT fk_verificaciones_usuario FOREIGN KEY (usuario_id) REFERENCES usuarios(id) ON DELETE RESTRICT,
    CONSTRAINT ck_verificaciones_intentos CHECK (intentos BETWEEN 0 AND 5)
) ENGINE=InnoDB
