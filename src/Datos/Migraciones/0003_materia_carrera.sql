-- Carrera de cada materia. Las migraciones 0005 y 0006 la reemplazan por el pensum.

ALTER TABLE materias ADD COLUMN IF NOT EXISTS carrera_id BIGINT NULL;
ALTER TABLE materias ADD CONSTRAINT fk_materias_carrera FOREIGN KEY IF NOT EXISTS (carrera_id) REFERENCES carreras(id) ON DELETE RESTRICT;
