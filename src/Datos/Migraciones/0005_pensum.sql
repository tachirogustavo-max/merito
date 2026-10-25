-- Pensum por carrera. Cada materia existe una vez, identificada por su código, y puede estar en varios pensums.
-- Una asignación solo admite materias del pensum de la carrera de su sección.

ALTER TABLE materias ADD COLUMN IF NOT EXISTS codigo VARCHAR(20) NULL;
ALTER TABLE materias ADD COLUMN IF NOT EXISTS uc TINYINT NOT NULL DEFAULT 0;
-- Código provisional para las materias que no tenían uno.
UPDATE materias SET codigo = CONCAT('MAT-', id) WHERE codigo IS NULL OR codigo = '';
ALTER TABLE materias MODIFY codigo VARCHAR(20) NOT NULL;
CREATE UNIQUE INDEX IF NOT EXISTS uq_materias_codigo ON materias (codigo);
-- Dos materias pueden tener el mismo nombre; las distingue el código.
ALTER TABLE materias DROP INDEX IF EXISTS uq_materias_nombre;
ALTER TABLE materias ADD CONSTRAINT IF NOT EXISTS ck_materias_codigo CHECK (codigo <> '');
ALTER TABLE materias ADD CONSTRAINT IF NOT EXISTS ck_materias_uc CHECK (uc BETWEEN 0 AND 60);

CREATE TABLE IF NOT EXISTS pensum (
    carrera_id BIGINT NOT NULL,
    materia_id BIGINT NOT NULL,
    semestre TINYINT NOT NULL DEFAULT 0,
    PRIMARY KEY (carrera_id, materia_id),
    CONSTRAINT fk_pensum_carrera FOREIGN KEY (carrera_id) REFERENCES carreras(id) ON DELETE RESTRICT,
    CONSTRAINT fk_pensum_materia FOREIGN KEY (materia_id) REFERENCES materias(id) ON DELETE RESTRICT,
    CONSTRAINT ck_pensum_semestre CHECK (semestre BETWEEN 0 AND 20),
    INDEX ix_pensum_materia (materia_id)
) ENGINE=InnoDB;

-- Pensum inicial a partir de las materias y asignaciones existentes.
INSERT IGNORE INTO pensum (carrera_id, materia_id) SELECT carrera_id, id FROM materias WHERE carrera_id IS NOT NULL;
INSERT IGNORE INTO pensum (carrera_id, materia_id) SELECT DISTINCT s.carrera_id, o.materia_id FROM ofertas o JOIN secciones s ON s.id = o.seccion_id;

-- La asignación guarda la carrera de su sección para validar la materia contra ese pensum.
ALTER TABLE ofertas ADD COLUMN IF NOT EXISTS carrera_id BIGINT NULL;
UPDATE ofertas o JOIN secciones s ON s.id = o.seccion_id SET o.carrera_id = s.carrera_id WHERE o.carrera_id IS NULL OR o.carrera_id <> s.carrera_id;
ALTER TABLE ofertas MODIFY carrera_id BIGINT NOT NULL;
CREATE UNIQUE INDEX IF NOT EXISTS uq_secciones_carrera_id ON secciones (id, carrera_id);
ALTER TABLE ofertas ADD CONSTRAINT fk_ofertas_seccion_carrera FOREIGN KEY IF NOT EXISTS (seccion_id, carrera_id) REFERENCES secciones (id, carrera_id) ON DELETE RESTRICT ON UPDATE RESTRICT;
ALTER TABLE ofertas ADD CONSTRAINT fk_ofertas_pensum FOREIGN KEY IF NOT EXISTS (carrera_id, materia_id) REFERENCES pensum (carrera_id, materia_id) ON DELETE RESTRICT ON UPDATE RESTRICT
