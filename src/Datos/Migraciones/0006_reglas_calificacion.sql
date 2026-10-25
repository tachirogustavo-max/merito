-- Reglas de calificación configurables: escala, nota aprobatoria, redondeo e índice.
-- Al cerrar un período se guardan las UC, la nota aprobatoria y el redondeo vigentes.

-- La carrera de la materia la indica el pensum.
ALTER TABLE materias DROP FOREIGN KEY IF EXISTS fk_materias_carrera;
ALTER TABLE materias DROP COLUMN IF EXISTS carrera_id;
ALTER TABLE materias ADD COLUMN IF NOT EXISTS nota_aprobatoria DECIMAL(5,2) NULL;
ALTER TABLE materias ADD CONSTRAINT IF NOT EXISTS ck_materias_aprobatoria CHECK (nota_aprobatoria IS NULL OR nota_aprobatoria BETWEEN 0 AND 100);

ALTER TABLE configuracion DROP CONSTRAINT IF EXISTS ck_configuracion_clave;
ALTER TABLE configuracion ADD CONSTRAINT IF NOT EXISTS ck_configuracion_claves CHECK (clave IN
    ('formato_seccion','escala_minima','escala_maxima','nota_aprobatoria','redondeo','indice','indice_aplazadas'));
-- Valores de fábrica.
INSERT IGNORE INTO configuracion (clave, valor) VALUES
    ('escala_minima', '1'), ('escala_maxima', '20'), ('nota_aprobatoria', '10'),
    ('redondeo', 'media'), ('indice', 'ponderado'), ('indice_aplazadas', 'si');

-- La base admite notas de 0 a 100; la escala vigente la valida la lógica.
ALTER TABLE notas DROP CONSTRAINT IF EXISTS ck_notas_valor;
ALTER TABLE notas MODIFY valor DECIMAL(5,2) NOT NULL;
ALTER TABLE notas ADD CONSTRAINT IF NOT EXISTS ck_notas_rango CHECK (valor >= 0 AND valor <= 100);
ALTER TABLE cambios_notas DROP CONSTRAINT IF EXISTS ck_cambios_anterior;
ALTER TABLE cambios_notas DROP CONSTRAINT IF EXISTS ck_cambios_nueva;
ALTER TABLE cambios_notas MODIFY anterior DECIMAL(5,2) NULL;
ALTER TABLE cambios_notas MODIFY nueva DECIMAL(5,2) NULL;
ALTER TABLE cambios_notas ADD CONSTRAINT IF NOT EXISTS ck_cambios_anterior_rango CHECK (anterior IS NULL OR (anterior >= 0 AND anterior <= 100));
ALTER TABLE cambios_notas ADD CONSTRAINT IF NOT EXISTS ck_cambios_nueva_rango CHECK (nueva IS NULL OR (nueva >= 0 AND nueva <= 100));
ALTER TABLE resultados_publicados DROP CONSTRAINT IF EXISTS ck_resultados_valor;
ALTER TABLE resultados_publicados MODIFY valor DECIMAL(5,2) NULL;
ALTER TABLE resultados_publicados ADD CONSTRAINT IF NOT EXISTS ck_resultados_rango CHECK (valor IS NULL OR (valor >= 0 AND valor <= 100));

-- Reglas vigentes al cerrar.
ALTER TABLE ofertas ADD COLUMN IF NOT EXISTS uc_cierre TINYINT NULL;
ALTER TABLE ofertas ADD COLUMN IF NOT EXISTS aprobatoria_cierre DECIMAL(5,2) NULL;
ALTER TABLE ofertas ADD CONSTRAINT IF NOT EXISTS ck_ofertas_uc_cierre CHECK (uc_cierre IS NULL OR uc_cierre BETWEEN 0 AND 60);
ALTER TABLE ofertas ADD CONSTRAINT IF NOT EXISTS ck_ofertas_aprobatoria_cierre CHECK (aprobatoria_cierre IS NULL OR aprobatoria_cierre BETWEEN 0 AND 100);
ALTER TABLE periodos ADD COLUMN IF NOT EXISTS redondeo_cierre VARCHAR(10) NULL;

-- Períodos cerrados antes de esta migración: escala 1 a 20, aprobatoria 10 y redondeo de medio punto.
UPDATE periodos SET redondeo_cierre = 'media' WHERE cerrado = TRUE AND redondeo_cierre IS NULL;
UPDATE ofertas o JOIN periodos p ON p.id = o.periodo_id JOIN materias m ON m.id = o.materia_id
    SET o.uc_cierre = m.uc, o.aprobatoria_cierre = 10 WHERE p.cerrado = TRUE AND o.uc_cierre IS NULL;
-- IS NOT NULL explícito: un CHECK que da NULL se acepta.
ALTER TABLE periodos ADD CONSTRAINT IF NOT EXISTS ck_periodos_cierre CHECK (
    (cerrado = FALSE AND redondeo_cierre IS NULL)
    OR (cerrado = TRUE AND redondeo_cierre IS NOT NULL AND redondeo_cierre IN ('media','entero','ninguno')))
