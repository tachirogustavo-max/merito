-- Esquema inicial: cuentas, estructura académica, matrícula, notas y publicaciones.

CREATE TABLE IF NOT EXISTS usuarios (
    id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
    cedula VARCHAR(16) NOT NULL,
    nombres VARCHAR(100) NOT NULL,
    apellidos VARCHAR(100) NOT NULL,
    rol VARCHAR(20) NOT NULL,
    clave_hash VARCHAR(250) NULL,
    activo BOOLEAN NOT NULL DEFAULT TRUE,
    creado_en DATETIME(6) NOT NULL DEFAULT UTC_TIMESTAMP(6),
    CONSTRAINT uq_usuarios_cedula UNIQUE (cedula),
    CONSTRAINT ck_usuarios_rol CHECK (rol IN ('administrador','docente','estudiante'))
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS sesiones (
    huella CHAR(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL PRIMARY KEY,
    usuario_id BIGINT NOT NULL,
    vence_en DATETIME(6) NOT NULL,
    CONSTRAINT fk_sesiones_usuario FOREIGN KEY (usuario_id) REFERENCES usuarios(id) ON DELETE RESTRICT,
    INDEX ix_sesiones_usuario (usuario_id),
    INDEX ix_sesiones_vencimiento (vence_en)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS codigos_acceso (
    huella CHAR(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL PRIMARY KEY,
    usuario_id BIGINT NOT NULL,
    vence_en DATETIME(6) NOT NULL,
    CONSTRAINT fk_codigos_usuario FOREIGN KEY (usuario_id) REFERENCES usuarios(id) ON DELETE RESTRICT,
    CONSTRAINT uq_codigo_usuario UNIQUE (usuario_id)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS periodos (
    id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
    nombre VARCHAR(60) NOT NULL,
    cerrado BOOLEAN NOT NULL DEFAULT FALSE,
    cerrado_en DATETIME(6) NULL,
    CONSTRAINT uq_periodos_nombre UNIQUE (nombre)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS carreras (
    id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
    nombre VARCHAR(120) NOT NULL,
    CONSTRAINT uq_carreras_nombre UNIQUE (nombre)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS materias (
    id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
    nombre VARCHAR(120) NOT NULL,
    CONSTRAINT uq_materias_nombre UNIQUE (nombre)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS secciones (
    id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
    carrera_id BIGINT NOT NULL,
    nombre VARCHAR(60) NOT NULL,
    CONSTRAINT fk_secciones_carrera FOREIGN KEY (carrera_id) REFERENCES carreras(id) ON DELETE RESTRICT,
    CONSTRAINT uq_secciones_carrera UNIQUE (carrera_id, nombre)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS ofertas (
    id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
    periodo_id BIGINT NOT NULL,
    seccion_id BIGINT NOT NULL,
    materia_id BIGINT NOT NULL,
    docente_id BIGINT NOT NULL,
    revision INT NOT NULL DEFAULT 1,
    CONSTRAINT fk_ofertas_periodo FOREIGN KEY (periodo_id) REFERENCES periodos(id) ON DELETE RESTRICT,
    CONSTRAINT fk_ofertas_seccion FOREIGN KEY (seccion_id) REFERENCES secciones(id) ON DELETE RESTRICT,
    CONSTRAINT fk_ofertas_materia FOREIGN KEY (materia_id) REFERENCES materias(id) ON DELETE RESTRICT,
    CONSTRAINT fk_ofertas_docente FOREIGN KEY (docente_id) REFERENCES usuarios(id) ON DELETE RESTRICT,
    CONSTRAINT uq_ofertas_grupo UNIQUE (periodo_id, seccion_id, materia_id),
    INDEX ix_ofertas_docente (docente_id, periodo_id)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS inscripciones (
    id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
    oferta_id BIGINT NOT NULL,
    estudiante_id BIGINT NOT NULL,
    estado VARCHAR(12) NOT NULL,
    propuesta_por BIGINT NOT NULL,
    aprobada_por BIGINT NULL,
    CONSTRAINT fk_inscripciones_oferta FOREIGN KEY (oferta_id) REFERENCES ofertas(id) ON DELETE RESTRICT,
    CONSTRAINT fk_inscripciones_estudiante FOREIGN KEY (estudiante_id) REFERENCES usuarios(id) ON DELETE RESTRICT,
    CONSTRAINT fk_inscripciones_proponente FOREIGN KEY (propuesta_por) REFERENCES usuarios(id) ON DELETE RESTRICT,
    CONSTRAINT fk_inscripciones_aprobador FOREIGN KEY (aprobada_por) REFERENCES usuarios(id) ON DELETE RESTRICT,
    CONSTRAINT uq_inscripciones UNIQUE (oferta_id, estudiante_id),
    CONSTRAINT uq_inscripciones_oferta UNIQUE (id, oferta_id),
    CONSTRAINT ck_inscripciones_estado CHECK (estado IN ('pendiente','confirmada')),
    INDEX ix_inscripciones_estudiante (estudiante_id, estado)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS evaluaciones (
    id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
    oferta_id BIGINT NOT NULL,
    nombre VARCHAR(100) NOT NULL,
    peso DECIMAL(5,2) NOT NULL,
    orden INT NOT NULL,
    CONSTRAINT fk_evaluaciones_oferta FOREIGN KEY (oferta_id) REFERENCES ofertas(id) ON DELETE RESTRICT,
    CONSTRAINT uq_evaluaciones_orden UNIQUE (oferta_id, orden),
    CONSTRAINT uq_evaluaciones_nombre UNIQUE (oferta_id, nombre),
    CONSTRAINT uq_evaluaciones_oferta UNIQUE (id, oferta_id),
    CONSTRAINT ck_evaluaciones_peso CHECK (peso > 0 AND peso <= 100)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS notas (
    oferta_id BIGINT NOT NULL,
    evaluacion_id BIGINT NOT NULL,
    inscripcion_id BIGINT NOT NULL,
    valor DECIMAL(4,2) NOT NULL,
    actualizado_en DATETIME(6) NOT NULL DEFAULT UTC_TIMESTAMP(6),
    PRIMARY KEY (evaluacion_id, inscripcion_id),
    CONSTRAINT fk_notas_evaluacion FOREIGN KEY (evaluacion_id, oferta_id) REFERENCES evaluaciones(id, oferta_id) ON DELETE RESTRICT,
    CONSTRAINT fk_notas_inscripcion FOREIGN KEY (inscripcion_id, oferta_id) REFERENCES inscripciones(id, oferta_id) ON DELETE RESTRICT,
    CONSTRAINT ck_notas_valor CHECK (valor >= 1 AND valor <= 20)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS publicaciones (
    id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
    oferta_id BIGINT NOT NULL,
    revision INT NOT NULL,
    publicada_por BIGINT NOT NULL,
    publicada_en DATETIME(6) NOT NULL DEFAULT UTC_TIMESTAMP(6),
    CONSTRAINT fk_publicaciones_oferta FOREIGN KEY (oferta_id) REFERENCES ofertas(id) ON DELETE RESTRICT,
    CONSTRAINT fk_publicaciones_usuario FOREIGN KEY (publicada_por) REFERENCES usuarios(id) ON DELETE RESTRICT,
    CONSTRAINT uq_publicaciones_revision UNIQUE (oferta_id, revision)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS cambios_notas (
    id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
    oferta_id BIGINT NOT NULL,
    evaluacion_id BIGINT NOT NULL,
    inscripcion_id BIGINT NOT NULL,
    actor_id BIGINT NOT NULL,
    revision INT NOT NULL,
    anterior DECIMAL(4,2) NULL,
    nueva DECIMAL(4,2) NULL,
    ocurrido_en DATETIME(6) NOT NULL DEFAULT UTC_TIMESTAMP(6),
    CONSTRAINT fk_cambios_evaluacion FOREIGN KEY (evaluacion_id, oferta_id) REFERENCES evaluaciones(id, oferta_id) ON DELETE RESTRICT,
    CONSTRAINT fk_cambios_inscripcion FOREIGN KEY (inscripcion_id, oferta_id) REFERENCES inscripciones(id, oferta_id) ON DELETE RESTRICT,
    CONSTRAINT fk_cambios_actor FOREIGN KEY (actor_id) REFERENCES usuarios(id) ON DELETE RESTRICT,
    CONSTRAINT ck_cambios_anterior CHECK (anterior IS NULL OR (anterior >= 1 AND anterior <= 20)),
    CONSTRAINT ck_cambios_nueva CHECK (nueva IS NULL OR (nueva >= 1 AND nueva <= 20)),
    INDEX ix_cambios_planilla (oferta_id, revision)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS resultados_publicados (
    publicacion_id BIGINT NOT NULL,
    estudiante_id BIGINT NOT NULL,
    evaluacion_id BIGINT NOT NULL,
    nombre_evaluacion VARCHAR(100) NOT NULL,
    peso DECIMAL(5,2) NOT NULL,
    valor DECIMAL(4,2) NULL,
    orden INT NOT NULL,
    PRIMARY KEY (publicacion_id, estudiante_id, evaluacion_id),
    CONSTRAINT fk_resultados_publicacion FOREIGN KEY (publicacion_id) REFERENCES publicaciones(id) ON DELETE RESTRICT,
    CONSTRAINT fk_resultados_estudiante FOREIGN KEY (estudiante_id) REFERENCES usuarios(id) ON DELETE RESTRICT,
    CONSTRAINT fk_resultados_evaluacion FOREIGN KEY (evaluacion_id) REFERENCES evaluaciones(id) ON DELETE RESTRICT,
    CONSTRAINT ck_resultados_peso CHECK (peso > 0 AND peso <= 100),
    CONSTRAINT ck_resultados_valor CHECK (valor IS NULL OR (valor >= 1 AND valor <= 20)),
    INDEX ix_resultados_estudiante (estudiante_id, publicacion_id)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS auditoria (
    id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
    actor_id BIGINT NOT NULL,
    accion VARCHAR(60) NOT NULL,
    referencia VARCHAR(100) NOT NULL,
    ocurrido_en DATETIME(6) NOT NULL DEFAULT UTC_TIMESTAMP(6),
    CONSTRAINT fk_auditoria_actor FOREIGN KEY (actor_id) REFERENCES usuarios(id) ON DELETE RESTRICT,
    INDEX ix_auditoria_fecha (ocurrido_en),
    INDEX ix_auditoria_actor (actor_id, ocurrido_en)
) ENGINE=InnoDB;
