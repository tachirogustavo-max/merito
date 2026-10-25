-- Esquema de la base de datos de Mérito, tal como lo crea el instalador en MariaDB 11.4.
-- Generado con mariadb-dump --no-data. Las tablas se crean con «Merito.Servidor --migrar»; este archivo es de consulta.

CREATE DATABASE IF NOT EXISTS merito CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
USE merito;

CREATE TABLE `auditoria` (
  `id` bigint(20) NOT NULL AUTO_INCREMENT,
  `actor_id` bigint(20) NOT NULL,
  `accion` varchar(60) NOT NULL,
  `referencia` varchar(100) NOT NULL,
  `ocurrido_en` datetime(6) NOT NULL DEFAULT utc_timestamp(6),
  PRIMARY KEY (`id`),
  KEY `ix_auditoria_fecha` (`ocurrido_en`),
  KEY `ix_auditoria_actor` (`actor_id`,`ocurrido_en`),
  CONSTRAINT `fk_auditoria_actor` FOREIGN KEY (`actor_id`) REFERENCES `usuarios` (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
CREATE TABLE `cambios_notas` (
  `id` bigint(20) NOT NULL AUTO_INCREMENT,
  `oferta_id` bigint(20) NOT NULL,
  `evaluacion_id` bigint(20) NOT NULL,
  `inscripcion_id` bigint(20) NOT NULL,
  `actor_id` bigint(20) NOT NULL,
  `revision` int(11) NOT NULL,
  `anterior` decimal(5,2) DEFAULT NULL,
  `nueva` decimal(5,2) DEFAULT NULL,
  `ocurrido_en` datetime(6) NOT NULL DEFAULT utc_timestamp(6),
  PRIMARY KEY (`id`),
  KEY `fk_cambios_evaluacion` (`evaluacion_id`,`oferta_id`),
  KEY `fk_cambios_inscripcion` (`inscripcion_id`,`oferta_id`),
  KEY `fk_cambios_actor` (`actor_id`),
  KEY `ix_cambios_planilla` (`oferta_id`,`revision`),
  CONSTRAINT `fk_cambios_actor` FOREIGN KEY (`actor_id`) REFERENCES `usuarios` (`id`),
  CONSTRAINT `fk_cambios_evaluacion` FOREIGN KEY (`evaluacion_id`, `oferta_id`) REFERENCES `evaluaciones` (`id`, `oferta_id`),
  CONSTRAINT `fk_cambios_inscripcion` FOREIGN KEY (`inscripcion_id`, `oferta_id`) REFERENCES `inscripciones` (`id`, `oferta_id`),
  CONSTRAINT `ck_cambios_anterior_rango` CHECK (`anterior` is null or `anterior` >= 0 and `anterior` <= 100),
  CONSTRAINT `ck_cambios_nueva_rango` CHECK (`nueva` is null or `nueva` >= 0 and `nueva` <= 100)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
CREATE TABLE `carreras` (
  `id` bigint(20) NOT NULL AUTO_INCREMENT,
  `nombre` varchar(120) NOT NULL,
  `codigo` varchar(20) DEFAULT NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uq_carreras_nombre` (`nombre`),
  UNIQUE KEY `uq_carreras_codigo` (`codigo`),
  CONSTRAINT `ck_carreras_codigo` CHECK (`codigo` is null or `codigo` <> '')
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
CREATE TABLE `codigo_configuracion` (
  `huella` char(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
  `creado_en` datetime(6) NOT NULL DEFAULT utc_timestamp(6),
  PRIMARY KEY (`huella`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
CREATE TABLE `codigos_acceso` (
  `huella` char(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
  `usuario_id` bigint(20) NOT NULL,
  `vence_en` datetime(6) NOT NULL,
  PRIMARY KEY (`huella`),
  UNIQUE KEY `uq_codigo_usuario` (`usuario_id`),
  CONSTRAINT `fk_codigos_usuario` FOREIGN KEY (`usuario_id`) REFERENCES `usuarios` (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
CREATE TABLE `configuracion` (
  `clave` varchar(40) NOT NULL,
  `valor` varchar(200) NOT NULL,
  `actualizado_en` datetime(6) NOT NULL DEFAULT utc_timestamp(6),
  PRIMARY KEY (`clave`),
  CONSTRAINT `ck_configuracion_valor` CHECK (`valor` <> ''),
  CONSTRAINT `ck_configuracion_claves` CHECK (`clave` in ('formato_seccion','escala_minima','escala_maxima','nota_aprobatoria','redondeo','indice','indice_aplazadas'))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
CREATE TABLE `evaluaciones` (
  `id` bigint(20) NOT NULL AUTO_INCREMENT,
  `oferta_id` bigint(20) NOT NULL,
  `nombre` varchar(100) NOT NULL,
  `peso` decimal(5,2) NOT NULL,
  `orden` int(11) NOT NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uq_evaluaciones_orden` (`oferta_id`,`orden`),
  UNIQUE KEY `uq_evaluaciones_nombre` (`oferta_id`,`nombre`),
  UNIQUE KEY `uq_evaluaciones_oferta` (`id`,`oferta_id`),
  CONSTRAINT `fk_evaluaciones_oferta` FOREIGN KEY (`oferta_id`) REFERENCES `ofertas` (`id`),
  CONSTRAINT `ck_evaluaciones_peso` CHECK (`peso` > 0 and `peso` <= 100)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
CREATE TABLE `inscripciones` (
  `id` bigint(20) NOT NULL AUTO_INCREMENT,
  `oferta_id` bigint(20) NOT NULL,
  `estudiante_id` bigint(20) NOT NULL,
  `estado` varchar(12) NOT NULL,
  `propuesta_por` bigint(20) NOT NULL,
  `aprobada_por` bigint(20) DEFAULT NULL,
  `periodo_id` bigint(20) NOT NULL,
  `materia_id` bigint(20) NOT NULL,
  `vigente` tinyint(4) GENERATED ALWAYS AS (if(`estado` = 'retirada',NULL,1)) STORED,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uq_inscripciones` (`oferta_id`,`estudiante_id`),
  UNIQUE KEY `uq_inscripciones_oferta` (`id`,`oferta_id`),
  UNIQUE KEY `uq_inscripciones_materia_periodo` (`estudiante_id`,`periodo_id`,`materia_id`,`vigente`),
  KEY `fk_inscripciones_proponente` (`propuesta_por`),
  KEY `fk_inscripciones_aprobador` (`aprobada_por`),
  KEY `ix_inscripciones_estudiante` (`estudiante_id`,`estado`),
  KEY `fk_inscripciones_oferta_materia` (`oferta_id`,`periodo_id`,`materia_id`),
  CONSTRAINT `fk_inscripciones_aprobador` FOREIGN KEY (`aprobada_por`) REFERENCES `usuarios` (`id`),
  CONSTRAINT `fk_inscripciones_estudiante` FOREIGN KEY (`estudiante_id`) REFERENCES `usuarios` (`id`),
  CONSTRAINT `fk_inscripciones_oferta` FOREIGN KEY (`oferta_id`) REFERENCES `ofertas` (`id`),
  CONSTRAINT `fk_inscripciones_oferta_materia` FOREIGN KEY (`oferta_id`, `periodo_id`, `materia_id`) REFERENCES `ofertas` (`id`, `periodo_id`, `materia_id`),
  CONSTRAINT `fk_inscripciones_proponente` FOREIGN KEY (`propuesta_por`) REFERENCES `usuarios` (`id`),
  CONSTRAINT `ck_inscripciones_estado` CHECK (`estado` in ('pendiente','confirmada','retirada'))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
CREATE TABLE `materias` (
  `id` bigint(20) NOT NULL AUTO_INCREMENT,
  `nombre` varchar(120) NOT NULL,
  `codigo` varchar(20) NOT NULL,
  `uc` tinyint(4) NOT NULL DEFAULT 0,
  `nota_aprobatoria` decimal(5,2) DEFAULT NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uq_materias_codigo` (`codigo`),
  CONSTRAINT `ck_materias_codigo` CHECK (`codigo` <> ''),
  CONSTRAINT `ck_materias_uc` CHECK (`uc` between 0 and 60),
  CONSTRAINT `ck_materias_aprobatoria` CHECK (`nota_aprobatoria` is null or `nota_aprobatoria` between 0 and 100)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
CREATE TABLE `notas` (
  `oferta_id` bigint(20) NOT NULL,
  `evaluacion_id` bigint(20) NOT NULL,
  `inscripcion_id` bigint(20) NOT NULL,
  `valor` decimal(5,2) NOT NULL,
  `actualizado_en` datetime(6) NOT NULL DEFAULT utc_timestamp(6),
  PRIMARY KEY (`evaluacion_id`,`inscripcion_id`),
  KEY `fk_notas_evaluacion` (`evaluacion_id`,`oferta_id`),
  KEY `fk_notas_inscripcion` (`inscripcion_id`,`oferta_id`),
  CONSTRAINT `fk_notas_evaluacion` FOREIGN KEY (`evaluacion_id`, `oferta_id`) REFERENCES `evaluaciones` (`id`, `oferta_id`),
  CONSTRAINT `fk_notas_inscripcion` FOREIGN KEY (`inscripcion_id`, `oferta_id`) REFERENCES `inscripciones` (`id`, `oferta_id`),
  CONSTRAINT `ck_notas_rango` CHECK (`valor` >= 0 and `valor` <= 100)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
CREATE TABLE `ofertas` (
  `id` bigint(20) NOT NULL AUTO_INCREMENT,
  `periodo_id` bigint(20) NOT NULL,
  `seccion_id` bigint(20) NOT NULL,
  `materia_id` bigint(20) NOT NULL,
  `docente_id` bigint(20) NOT NULL,
  `revision` int(11) NOT NULL DEFAULT 1,
  `carrera_id` bigint(20) NOT NULL,
  `uc_cierre` tinyint(4) DEFAULT NULL,
  `aprobatoria_cierre` decimal(5,2) DEFAULT NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uq_ofertas_grupo` (`periodo_id`,`seccion_id`,`materia_id`),
  UNIQUE KEY `uq_ofertas_periodo_materia` (`id`,`periodo_id`,`materia_id`),
  KEY `fk_ofertas_materia` (`materia_id`),
  KEY `ix_ofertas_docente` (`docente_id`,`periodo_id`),
  KEY `fk_ofertas_seccion_carrera` (`seccion_id`,`carrera_id`),
  KEY `fk_ofertas_pensum` (`carrera_id`,`materia_id`),
  CONSTRAINT `fk_ofertas_docente` FOREIGN KEY (`docente_id`) REFERENCES `usuarios` (`id`),
  CONSTRAINT `fk_ofertas_materia` FOREIGN KEY (`materia_id`) REFERENCES `materias` (`id`),
  CONSTRAINT `fk_ofertas_pensum` FOREIGN KEY (`carrera_id`, `materia_id`) REFERENCES `pensum` (`carrera_id`, `materia_id`),
  CONSTRAINT `fk_ofertas_periodo` FOREIGN KEY (`periodo_id`) REFERENCES `periodos` (`id`),
  CONSTRAINT `fk_ofertas_seccion` FOREIGN KEY (`seccion_id`) REFERENCES `secciones` (`id`),
  CONSTRAINT `fk_ofertas_seccion_carrera` FOREIGN KEY (`seccion_id`, `carrera_id`) REFERENCES `secciones` (`id`, `carrera_id`),
  CONSTRAINT `ck_ofertas_uc_cierre` CHECK (`uc_cierre` is null or `uc_cierre` between 0 and 60),
  CONSTRAINT `ck_ofertas_aprobatoria_cierre` CHECK (`aprobatoria_cierre` is null or `aprobatoria_cierre` between 0 and 100)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
CREATE TABLE `pensum` (
  `carrera_id` bigint(20) NOT NULL,
  `materia_id` bigint(20) NOT NULL,
  `semestre` tinyint(4) NOT NULL DEFAULT 0,
  PRIMARY KEY (`carrera_id`,`materia_id`),
  KEY `ix_pensum_materia` (`materia_id`),
  CONSTRAINT `fk_pensum_carrera` FOREIGN KEY (`carrera_id`) REFERENCES `carreras` (`id`),
  CONSTRAINT `fk_pensum_materia` FOREIGN KEY (`materia_id`) REFERENCES `materias` (`id`),
  CONSTRAINT `ck_pensum_semestre` CHECK (`semestre` between 0 and 20)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
CREATE TABLE `periodos` (
  `id` bigint(20) NOT NULL AUTO_INCREMENT,
  `nombre` varchar(60) NOT NULL,
  `cerrado` tinyint(1) NOT NULL DEFAULT 0,
  `cerrado_en` datetime(6) DEFAULT NULL,
  `redondeo_cierre` varchar(10) DEFAULT NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uq_periodos_nombre` (`nombre`),
  CONSTRAINT `ck_periodos_cierre` CHECK (`cerrado` = 0 and `redondeo_cierre` is null or `cerrado` = 1 and `redondeo_cierre` is not null and `redondeo_cierre` in ('media','entero','ninguno'))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
CREATE TABLE `publicaciones` (
  `id` bigint(20) NOT NULL AUTO_INCREMENT,
  `oferta_id` bigint(20) NOT NULL,
  `revision` int(11) NOT NULL,
  `publicada_por` bigint(20) NOT NULL,
  `publicada_en` datetime(6) NOT NULL DEFAULT utc_timestamp(6),
  PRIMARY KEY (`id`),
  UNIQUE KEY `uq_publicaciones_revision` (`oferta_id`,`revision`),
  KEY `fk_publicaciones_usuario` (`publicada_por`),
  CONSTRAINT `fk_publicaciones_oferta` FOREIGN KEY (`oferta_id`) REFERENCES `ofertas` (`id`),
  CONSTRAINT `fk_publicaciones_usuario` FOREIGN KEY (`publicada_por`) REFERENCES `usuarios` (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
CREATE TABLE `resultados_publicados` (
  `publicacion_id` bigint(20) NOT NULL,
  `estudiante_id` bigint(20) NOT NULL,
  `evaluacion_id` bigint(20) NOT NULL,
  `nombre_evaluacion` varchar(100) NOT NULL,
  `peso` decimal(5,2) NOT NULL,
  `valor` decimal(5,2) DEFAULT NULL,
  `orden` int(11) NOT NULL,
  PRIMARY KEY (`publicacion_id`,`estudiante_id`,`evaluacion_id`),
  KEY `fk_resultados_evaluacion` (`evaluacion_id`),
  KEY `ix_resultados_estudiante` (`estudiante_id`,`publicacion_id`),
  CONSTRAINT `fk_resultados_estudiante` FOREIGN KEY (`estudiante_id`) REFERENCES `usuarios` (`id`),
  CONSTRAINT `fk_resultados_evaluacion` FOREIGN KEY (`evaluacion_id`) REFERENCES `evaluaciones` (`id`),
  CONSTRAINT `fk_resultados_publicacion` FOREIGN KEY (`publicacion_id`) REFERENCES `publicaciones` (`id`),
  CONSTRAINT `ck_resultados_peso` CHECK (`peso` > 0 and `peso` <= 100),
  CONSTRAINT `ck_resultados_rango` CHECK (`valor` is null or `valor` >= 0 and `valor` <= 100)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
CREATE TABLE `secciones` (
  `id` bigint(20) NOT NULL AUTO_INCREMENT,
  `carrera_id` bigint(20) NOT NULL,
  `nombre` varchar(60) NOT NULL,
  `semestre` tinyint(4) NOT NULL DEFAULT 0,
  `codigo` varchar(60) NOT NULL DEFAULT '',
  PRIMARY KEY (`id`),
  UNIQUE KEY `uq_secciones_identificador` (`carrera_id`,`semestre`,`nombre`),
  UNIQUE KEY `uq_secciones_codigo` (`carrera_id`,`codigo`),
  UNIQUE KEY `uq_secciones_carrera_id` (`id`,`carrera_id`),
  CONSTRAINT `fk_secciones_carrera` FOREIGN KEY (`carrera_id`) REFERENCES `carreras` (`id`),
  CONSTRAINT `ck_secciones_semestre` CHECK (`semestre` between 0 and 20),
  CONSTRAINT `ck_secciones_codigo` CHECK (`codigo` <> '')
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
CREATE TABLE `sesiones` (
  `huella` char(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
  `usuario_id` bigint(20) NOT NULL,
  `vence_en` datetime(6) NOT NULL,
  PRIMARY KEY (`huella`),
  KEY `ix_sesiones_usuario` (`usuario_id`),
  KEY `ix_sesiones_vencimiento` (`vence_en`),
  CONSTRAINT `fk_sesiones_usuario` FOREIGN KEY (`usuario_id`) REFERENCES `usuarios` (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
CREATE TABLE `usuarios` (
  `id` bigint(20) NOT NULL AUTO_INCREMENT,
  `cedula` varchar(16) NOT NULL,
  `nombres` varchar(100) NOT NULL,
  `apellidos` varchar(100) NOT NULL,
  `rol` varchar(20) NOT NULL,
  `clave_hash` varchar(250) DEFAULT NULL,
  `activo` tinyint(1) NOT NULL DEFAULT 1,
  `creado_en` datetime(6) NOT NULL DEFAULT utc_timestamp(6),
  `correo` varchar(254) DEFAULT NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uq_usuarios_cedula` (`cedula`),
  UNIQUE KEY `uq_usuarios_correo` (`correo`),
  CONSTRAINT `ck_usuarios_rol` CHECK (`rol` in ('administrador','docente','estudiante')),
  CONSTRAINT `ck_usuarios_correo` CHECK (`correo` is null or cast(`correo` as char charset binary) = cast(lcase(`correo`) as char charset binary) and `correo` like '_%@_%._%' and `correo`  not like '% %' and `correo`  not like '%@%@%')
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
CREATE TABLE `verificaciones_correo` (
  `usuario_id` bigint(20) NOT NULL,
  `huella` char(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
  `vence_en` datetime(6) NOT NULL,
  `intentos` tinyint(4) NOT NULL DEFAULT 0,
  `enviado_en` datetime(6) NOT NULL DEFAULT utc_timestamp(6),
  PRIMARY KEY (`usuario_id`),
  CONSTRAINT `fk_verificaciones_usuario` FOREIGN KEY (`usuario_id`) REFERENCES `usuarios` (`id`),
  CONSTRAINT `ck_verificaciones_intentos` CHECK (`intentos` between 0 and 5)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
CREATE TABLE `versiones_esquema` (
  `version` int(11) NOT NULL,
  `aplicada_en` datetime(6) NOT NULL DEFAULT utc_timestamp(6),
  `nombre` varchar(120) DEFAULT NULL,
  `huella` char(64) DEFAULT NULL,
  PRIMARY KEY (`version`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
