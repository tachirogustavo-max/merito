-- Código de un solo uso para crear la primera cuenta de administración desde el programa.
-- Solo se guarda su huella; el texto lo ve quien instala el servidor.

CREATE TABLE IF NOT EXISTS codigo_configuracion (
    huella CHAR(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL PRIMARY KEY,
    creado_en DATETIME(6) NOT NULL DEFAULT UTC_TIMESTAMP(6)
) ENGINE=InnoDB;
