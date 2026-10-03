-- Seed mínimo Fase D — determinista e idempotente.
-- Requiere NutriologoDB.sql cargado y extensión vector (ver README).
-- Todas las filas usan IdUsuarioCrea = 1 (usuario sistema creado abajo).

INSERT INTO "Usuario" ("Id", "Nombre", "Correo", "ContrasenaHash", "FechaCreacion", "FechaModificacion", "Estatus", "IdUsuarioCrea")
SELECT 1, 'Sistema', 'sistema@nutriologo.local', 'NO_LOGIN', now(), now(), 1, 1
WHERE NOT EXISTS (SELECT 1 FROM "Usuario" WHERE "Id" = 1);

INSERT INTO "SexoBiologico" ("Codigo","Descripcion","FechaCreacion","FechaModificacion","Estatus","IdUsuarioCrea")
SELECT v.c, v.d, now(), now(), 1, 1 FROM (VALUES ('MASCULINO','Masculino'),('FEMENINO','Femenino')) v(c,d)
WHERE NOT EXISTS (SELECT 1 FROM "SexoBiologico" s WHERE s."Codigo" = v.c);

INSERT INTO "NivelActividadFisica" ("Codigo","Descripcion","FactorActividad","Orden","FechaCreacion","FechaModificacion","Estatus","IdUsuarioCrea")
SELECT v.c, v.d, v.f, v.o, now(), now(), 1, 1 FROM (VALUES
 ('SEDENTARIO','Sedentario',1.2000,1),('LIGERO','Ligero',1.3750,2),('MODERADO','Moderado',1.5500,3),
 ('ACTIVO','Activo',1.7250,4),('MUY_ACTIVO','Muy activo',1.9000,5)) v(c,d,f,o)
WHERE NOT EXISTS (SELECT 1 FROM "NivelActividadFisica" n WHERE n."Codigo" = v.c);

INSERT INTO "TipoObjetivo" ("Codigo","Descripcion","FechaCreacion","FechaModificacion","Estatus","IdUsuarioCrea")
SELECT v.c, v.d, now(), now(), 1, 1 FROM (VALUES
 ('PERDER_PESO','Perder peso'),('MANTENER_PESO','Mantener peso'),('GANAR_PESO','Ganar peso')) v(c,d)
WHERE NOT EXISTS (SELECT 1 FROM "TipoObjetivo" t WHERE t."Codigo" = v.c);

INSERT INTO "MetodoCalculoEnergetico" ("Codigo","Descripcion","Formula","Version","FechaCreacion","FechaModificacion","Estatus","IdUsuarioCrea")
SELECT 'MIFFLIN_ST_JEOR','Mifflin-St Jeor','10p+6.25a-5e+s','1.0',now(),now(),1,1
WHERE NOT EXISTS (SELECT 1 FROM "MetodoCalculoEnergetico" m WHERE m."Codigo"='MIFFLIN_ST_JEOR');

INSERT INTO "TipoFuenteMetaCalorica" ("Codigo","Descripcion","FechaCreacion","FechaModificacion","Estatus","IdUsuarioCrea")
SELECT v.c, v.d, now(), now(), 1, 1 FROM (VALUES
 ('CALCULADA','Calculada por el sistema'),('USUARIO','Proporcionada por el usuario'),('PROFESIONAL','Proporcionada por profesional')) v(c,d)
WHERE NOT EXISTS (SELECT 1 FROM "TipoFuenteMetaCalorica" t WHERE t."Codigo" = v.c);

INSERT INTO "NivelRiesgo" ("Codigo","Descripcion","Orden","FechaCreacion","FechaModificacion","Estatus","IdUsuarioCrea")
SELECT v.c, v.d, v.o, now(), now(), 1, 1 FROM (VALUES ('BAJO','Riesgo bajo',1),('MODERADO','Riesgo moderado',2),('ALTO','Riesgo alto',3)) v(c,d,o)
WHERE NOT EXISTS (SELECT 1 FROM "NivelRiesgo" n WHERE n."Codigo" = v.c);

INSERT INTO "TipoUnidadMedida" ("Codigo","Descripcion","FechaCreacion","FechaModificacion","Estatus","IdUsuarioCrea")
SELECT v.c, v.d, now(), now(), 1, 1 FROM (VALUES
 ('MASA','Masa'),('VOLUMEN','Volumen'),('ENERGIA','Energía'),('PIEZA','Pieza')) v(c,d)
WHERE NOT EXISTS (SELECT 1 FROM "TipoUnidadMedida" t WHERE t."Codigo" = v.c);

INSERT INTO "UnidadMedida" ("IdTipoUnidadMedida","Codigo","Descripcion","Simbolo","FechaCreacion","FechaModificacion","Estatus","IdUsuarioCrea")
SELECT t."Id", v.c, v.d, v.s, now(), now(), 1, 1 FROM (VALUES
 ('G','Gramo','g','MASA'),('KG','Kilogramo','kg','MASA'),('ML','Mililitro','ml','VOLUMEN'),
 ('L','Litro','l','VOLUMEN'),('TAZA','Taza','taza','VOLUMEN'),('KCAL','Kilocaloría','kcal','ENERGIA'),
 ('PIEZA','Pieza','pza','PIEZA')) v(c,d,s,tc)
JOIN "TipoUnidadMedida" t ON t."Codigo" = v.tc
WHERE NOT EXISTS (SELECT 1 FROM "UnidadMedida" u WHERE u."Codigo" = v.c);

INSERT INTO "TipoNutriente" ("Codigo","Descripcion","FechaCreacion","FechaModificacion","Estatus","IdUsuarioCrea")
SELECT v.c, v.d, now(), now(), 1, 1 FROM (VALUES ('MACRO','Macronutriente'),('ENERGIA','Energía')) v(c,d)
WHERE NOT EXISTS (SELECT 1 FROM "TipoNutriente" t WHERE t."Codigo" = v.c);

INSERT INTO "Nutriente" ("IdTipoNutriente","IdUnidadMedida","Codigo","Descripcion","FechaCreacion","FechaModificacion","Estatus","IdUsuarioCrea")
SELECT tn."Id", um."Id", v.c, v.d, now(), now(), 1, 1 FROM (VALUES
 ('ENERGIA','Energía','ENERGIA','KCAL'),('PROTEINA','Proteína','MACRO','G'),('GRASA','Grasa','MACRO','G'),
 ('CARBOHIDRATO','Carbohidrato','MACRO','G'),('FIBRA','Fibra','MACRO','G')) v(c,d,tn,um)
JOIN "TipoNutriente" tn ON tn."Codigo" = v.tn
JOIN "UnidadMedida" um ON um."Codigo" = v.um
WHERE NOT EXISTS (SELECT 1 FROM "Nutriente" n WHERE n."Codigo" = v.c);

INSERT INTO "FuenteAlimento" ("Codigo","Descripcion","UrlFuente","FechaCreacion","FechaModificacion","Estatus","IdUsuarioCrea")
SELECT v.c, v.d, v.u, now(), now(), 1, 1 FROM (VALUES
 ('SEED_DEV','Datos de desarrollo',NULL),('USDA','USDA FoodData Central','https://fdc.nal.usda.gov/')) v(c,d,u)
WHERE NOT EXISTS (SELECT 1 FROM "FuenteAlimento" f WHERE f."Codigo" = v.c);

INSERT INTO "CategoriaAlimento" ("IdCategoriaPadre","Codigo","Descripcion","Nivel","FechaCreacion","FechaModificacion","Estatus","IdUsuarioCrea")
SELECT NULL, v.c, v.d, 1, now(), now(), 1, 1 FROM (VALUES
 ('PROTEINAS','Proteínas'),('CEREALES','Cereales y granos'),('VERDURAS','Verduras'),('FRUTAS','Frutas'),
 ('LACTEOS','Lácteos'),('GRASAS_ACEITES','Grasas y aceites'),('BEBIDAS','Bebidas'),('SALSAS','Salsas y condimentos')) v(c,d)
WHERE NOT EXISTS (SELECT 1 FROM "CategoriaAlimento" c WHERE c."Codigo" = v.c);

INSERT INTO "CocinaAlimentaria" ("Codigo","Descripcion","FechaCreacion","FechaModificacion","Estatus","IdUsuarioCrea")
SELECT v.c, v.d, now(), now(), 1, 1 FROM (VALUES ('MEXICANA','Mexicana'),('INTERNACIONAL','Internacional')) v(c,d)
WHERE NOT EXISTS (SELECT 1 FROM "CocinaAlimentaria" c WHERE c."Codigo" = v.c);

INSERT INTO "Marca" ("Nombre","Descripcion","FechaCreacion","FechaModificacion","Estatus","IdUsuarioCrea")
SELECT 'Genérica','Marca genérica de desarrollo',now(),now(),1,1
WHERE NOT EXISTS (SELECT 1 FROM "Marca" m WHERE m."Nombre"='Genérica');

INSERT INTO "EstadoPreparacionAlimento" ("Codigo","Descripcion","FechaCreacion","FechaModificacion","Estatus","IdUsuarioCrea")
SELECT v.c, v.d, now(), now(), 1, 1 FROM (VALUES ('CRUDO','Crudo'),('COCIDO','Cocido')) v(c,d)
WHERE NOT EXISTS (SELECT 1 FROM "EstadoPreparacionAlimento" e WHERE e."Codigo" = v.c);

INSERT INTO "MetodoPreparacion" ("Codigo","Descripcion","FechaCreacion","FechaModificacion","Estatus","IdUsuarioCrea")
SELECT v.c, v.d, now(), now(), 1, 1 FROM (VALUES ('CRUDO','Crudo'),('HERVIDO','Hervido'),('FRITO','Frito'),('HORNEADO','Horneado')) v(c,d)
WHERE NOT EXISTS (SELECT 1 FROM "MetodoPreparacion" m WHERE m."Codigo" = v.c);

-- Alimentos de prueba (porciones y macros aproximados por 100 g).
INSERT INTO "Alimento" ("IdCategoriaAlimento","Nombre","Descripcion","EsReceta","FechaCreacion","FechaModificacion","Estatus","IdUsuarioCrea")
SELECT c."Id", v.n, v.d, false, now(), now(), 1, 1 FROM (VALUES
 ('Huevo','Huevo de gallina','PROTEINAS'),('Arroz','Arroz cocido','CEREALES'),('Pollo','Pechuga de pollo cocida','PROTEINAS'),
 ('Tortilla','Tortilla de maíz','CEREALES'),('Frijoles','Frijoles cocidos','CEREALES'),('Aguacate','Aguacate','FRUTAS'),
 ('Aceite','Aceite vegetal','GRASAS_ACEITES'),('Salsa','Salsa cruda','SALSAS'),('Agua','Agua','BEBIDAS'),('Refresco','Refresco cola','BEBIDAS')) v(n,d,cat)
JOIN "CategoriaAlimento" c ON c."Codigo" = v.cat
WHERE NOT EXISTS (SELECT 1 FROM "Alimento" a WHERE a."Nombre" = v.n);

INSERT INTO "AlimentoFuente" ("IdAlimento","IdFuenteAlimento","NombreFuente","VersionFuente","FechaCreacion","FechaModificacion","Estatus","IdUsuarioCrea")
SELECT a."Id", f."Id", a."Nombre", 'seed-d-1', now(), now(), 1, 1 FROM "Alimento" a
JOIN "FuenteAlimento" f ON f."Codigo" = 'SEED_DEV'
WHERE NOT EXISTS (SELECT 1 FROM "AlimentoFuente" af WHERE af."IdAlimento" = a."Id" AND af."IdFuenteAlimento" = f."Id");

INSERT INTO "BaseNutricionalAlimento" ("IdAlimentoFuente","CantidadBase","IdUnidadMedida","FechaCreacion","FechaModificacion","Estatus","IdUsuarioCrea")
SELECT af."Id", 100, u."Id", now(), now(), 1, 1 FROM "AlimentoFuente" af
JOIN "UnidadMedida" u ON u."Codigo" = 'G'
WHERE NOT EXISTS (SELECT 1 FROM "BaseNutricionalAlimento" b WHERE b."IdAlimentoFuente" = af."Id");

INSERT INTO "AlimentoNutriente" ("IdAlimentoFuente","IdNutriente","Cantidad","FechaCreacion","FechaModificacion","Estatus","IdUsuarioCrea")
SELECT af."Id", n."Id", v.cant, now(), now(), 1, 1 FROM (VALUES
 ('Huevo','ENERGIA',155),('Huevo','PROTEINA',13),('Huevo','GRASA',11),('Huevo','CARBOHIDRATO',1.1),
 ('Arroz','ENERGIA',130),('Arroz','PROTEINA',2.7),('Arroz','GRASA',0.3),('Arroz','CARBOHIDRATO',28),
 ('Pollo','ENERGIA',165),('Pollo','PROTEINA',31),('Pollo','GRASA',3.6),('Pollo','CARBOHIDRATO',0),
 ('Tortilla','ENERGIA',218),('Tortilla','PROTEINA',5.7),('Tortilla','GRASA',2.9),('Tortilla','CARBOHIDRATO',45),
 ('Frijoles','ENERGIA',127),('Frijoles','PROTEINA',8.7),('Frijoles','GRASA',0.5),('Frijoles','CARBOHIDRATO',22.8),
 ('Aguacate','ENERGIA',160),('Aguacate','PROTEINA',2),('Aguacate','GRASA',14.7),('Aguacate','CARBOHIDRATO',8.5),
 ('Aceite','ENERGIA',884),('Aceite','PROTEINA',0),('Aceite','GRASA',100),('Aceite','CARBOHIDRATO',0),
 ('Salsa','ENERGIA',20),('Salsa','PROTEINA',1),('Salsa','GRASA',0.2),('Salsa','CARBOHIDRATO',4),
 ('Agua','ENERGIA',0),('Agua','PROTEINA',0),('Agua','GRASA',0),('Agua','CARBOHIDRATO',0),
 ('Refresco','ENERGIA',42),('Refresco','PROTEINA',0),('Refresco','GRASA',0),('Refresco','CARBOHIDRATO',10.6)) v(nombre,nutriente,cant)
JOIN "AlimentoFuente" af ON af."NombreFuente" = v.nombre
JOIN "Nutriente" n ON n."Codigo" = v.nutriente
WHERE NOT EXISTS (SELECT 1 FROM "AlimentoNutriente" an WHERE an."IdAlimentoFuente" = af."Id" AND an."IdNutriente" = n."Id");

INSERT INTO "ConversionUnidad" ("IdUnidadOrigen","IdUnidadDestino","FactorConversion","Formula","FechaCreacion","FechaModificacion","Estatus","IdUsuarioCrea")
SELECT o."Id", d."Id", v.f, v.formula, now(), now(), 1, 1 FROM (VALUES
 ('KG','G',1000,'kg*1000'),('G','KG',0.001,'g/1000'),('L','ML',1000,'l*1000'),('ML','L',0.001,'ml/1000'),('TAZA','ML',240,'taza*240')) v(o,d,f,formula)
JOIN "UnidadMedida" o ON o."Codigo" = v.o
JOIN "UnidadMedida" d ON d."Codigo" = v.d
WHERE NOT EXISTS (SELECT 1 FROM "ConversionUnidad" c WHERE c."IdUnidadOrigen" = o."Id" AND c."IdUnidadDestino" = d."Id");

INSERT INTO "PorcionAlimento" ("IdAlimento","IdUnidadMedida","Descripcion","Cantidad","CantidadGramos","FechaCreacion","FechaModificacion","Estatus","IdUsuarioCrea")
SELECT a."Id", u."Id", v.descr, v.cant, v.gramos, now(), now(), 1, 1 FROM (VALUES
 ('Huevo','1 pieza','PIEZA',1,50),('Arroz','1 taza','G',160,160),('Pollo','1 pechuga','G',170,170),
 ('Tortilla','1 pieza','PIEZA',1,30),('Frijoles','1 taza','G',172,172),('Aguacate','1/2 pieza','PIEZA',0.5,100),
 ('Aceite','1 cucharada','ML',15,14),('Salsa','2 cucharadas','G',30,30),('Agua','1 vaso','ML',250,250),('Refresco','1 lata','ML',355,355)) v(nombre,descr,um,cant,gramos)
JOIN "Alimento" a ON a."Nombre" = v.nombre
JOIN "UnidadMedida" u ON u."Codigo" = v.um
WHERE NOT EXISTS (SELECT 1 FROM "PorcionAlimento" p WHERE p."IdAlimento" = a."Id" AND p."Descripcion" = v.descr);

-- Reglas de riesgo por defecto (bandas de fase C).
INSERT INTO "ReglaRiesgoObjetivo" ("Codigo","Descripcion","RitmoMinimo","RitmoMaximo","IdNivelRiesgo","Orden","FechaCreacion","FechaModificacion","Estatus","IdUsuarioCrea")
SELECT v.c, v.d, v.min, v.max, n."Id", v.o, now(), now(), 1, 1 FROM (VALUES
 ('RITMO_LENTO','Ritmo lento/seguro',NULL::decimal,0.5,'BAJO',1),
 ('RITMO_MODERADO','Ritmo moderado',0.5,1.0,'MODERADO',2),
 ('RITMO_AGRESIVO','Ritmo agresivo',1.0,NULL::decimal,'ALTO',3)) v(c,d,min,max,nivel,o)
JOIN "NivelRiesgo" n ON n."Codigo" = v.nivel
WHERE NOT EXISTS (SELECT 1 FROM "ReglaRiesgoObjetivo" r WHERE r."Codigo" = v.c);

INSERT INTO "ReglaRiesgoMetaCalorica" ("Codigo","Descripcion","CaloriasMinimas","DeficitMaximo","IdNivelRiesgo","Orden","FechaCreacion","FechaModificacion","Estatus","IdUsuarioCrea")
SELECT v.c, v.d, v.min, v.max, n."Id", v.o, now(), now(), 1, 1 FROM (VALUES
 ('DEFICIT_EXCESIVO','Déficit excesivo',NULL::decimal,500,'ALTO',1),
 ('INGESTA_MUY_BAJA','Meta por debajo del mínimo',1200,NULL::decimal,'ALTO',2),
 ('DEFICIT_MODERADO','Déficit moderado',NULL::decimal,1000,'MODERADO',3),
 ('SIN_RIESGO','Sin riesgo destacado',NULL,NULL,'BAJO',4)) v(c,d,min,max,nivel,o)
JOIN "NivelRiesgo" n ON n."Codigo" = v.nivel
WHERE NOT EXISTS (SELECT 1 FROM "ReglaRiesgoMetaCalorica" r WHERE r."Codigo" = v.c);

-- Re-sincronizar secuencias identity tras inserciones explícitas.
DO $$
DECLARE t text;
BEGIN
  FOR t IN SELECT table_name FROM information_schema.tables WHERE table_schema='public'
  LOOP
    BEGIN
      EXECUTE format('SELECT setval(pg_get_serial_sequence(%L,''Id''), COALESCE((SELECT MAX("Id") FROM %I),1))', t, t);
    EXCEPTION WHEN OTHERS THEN NULL;
    END;
  END LOOP;
END $$;
