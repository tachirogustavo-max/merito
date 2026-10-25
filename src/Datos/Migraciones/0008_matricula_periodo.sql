-- Matrícula del período: cada estudiante cursa una materia una sola vez por período.
-- La inscripción copia el período y la materia de su asignación; una clave foránea compuesta las mantiene iguales.
-- Una inscripción retirada conserva sus notas y no cuenta para el índice único.

CREATE UNIQUE INDEX IF NOT EXISTS uq_ofertas_periodo_materia ON ofertas (id, periodo_id, materia_id);

ALTER TABLE inscripciones ADD COLUMN IF NOT EXISTS periodo_id BIGINT NULL;
ALTER TABLE inscripciones ADD COLUMN IF NOT EXISTS materia_id BIGINT NULL;
UPDATE inscripciones i JOIN ofertas o ON o.id = i.oferta_id SET i.periodo_id = o.periodo_id, i.materia_id = o.materia_id;
ALTER TABLE inscripciones MODIFY periodo_id BIGINT NOT NULL;
ALTER TABLE inscripciones MODIFY materia_id BIGINT NOT NULL;
ALTER TABLE inscripciones ADD CONSTRAINT fk_inscripciones_oferta_materia FOREIGN KEY IF NOT EXISTS (oferta_id, periodo_id, materia_id)
    REFERENCES ofertas (id, periodo_id, materia_id) ON DELETE RESTRICT ON UPDATE RESTRICT;

ALTER TABLE inscripciones DROP CONSTRAINT IF EXISTS ck_inscripciones_estado;
ALTER TABLE inscripciones ADD CONSTRAINT IF NOT EXISTS ck_inscripciones_estado CHECK (estado IN ('pendiente','confirmada','retirada'));
-- NULL en las retiradas: el índice único admite varios NULL.
ALTER TABLE inscripciones ADD COLUMN IF NOT EXISTS vigente TINYINT AS (IF(estado = 'retirada', NULL, 1)) STORED;
CREATE UNIQUE INDEX IF NOT EXISTS uq_inscripciones_materia_periodo ON inscripciones (estudiante_id, periodo_id, materia_id, vigente)
