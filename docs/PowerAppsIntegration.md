# Integración PowerApps + Vue Nutriologo

## Visión general
PowerApps se conecta a la aplicación Vue Nutriologo mediante **APIs REST** expuestas desde el backend (.NET). La aplicación Vue actúa como el "motor de cálculo" y PowerApps como la capa de presentación/interacción para dispositivos móviles.

## Endpoints disponibles para PowerApps

### 1. Obtener balance calórico
```powerapps
// Fórmula en PowerApps
With(
    ConnectorVue.GetBalance(DateTimeValue(txtFecha.Text)),
    MostrarTexto($"Has consumido {caloriasConsumidas} kcal hoy")
)
```

### 2. Obtener perfil de usuario
```powerapps
With(
    ConnectorVue.GetPerfil(),
    MostrarTexto($"Usuario: {perfil.nombre}, Peso: {perfil.peso} kg")
)
```

### 3. Calcular objetivo nutricional
```powerapps
With(
    ConnectorVue.CalcularObjetivo({
        Peso: Value(txtPeso.Text),
        Altura: Value(txtAltura.Text),
        Edad: Value(txtEdad.Text),
        Sexo: DropdownSexo.Selected.Result,
        FactorActividad: SliderFactor.Value,
        TipoObjetivo: DropdownObjetivo.Selected.Result
    }),
    MostrarTexto(
        $"Meta: {objetivo.metaCaloricaEstimada} kcal/day" & Char(10) &
        $"Riesgo: {objetivo.evaluacionObjetivo.descripcion}"
    )
)
```

### 4. Configurar conector personalizado en PowerApps

#### Paso a paso:
1. En PowerApps, ir a **Datos > Conectores personalizados > Nuevo**
2. Importar el archivo `docs/openapi/vue-api.yaml`
3. El conector generará automáticamente las funciones:
   - `GetBalance(fecha)`
   - `GetPerfil()`
   - `CalcularObjetivo(cuerpo)`

### 4. Seguridad
- Todos los endpoints usan **Autenticación JWT** (Bearer token)
- PowerApps debe obtener el token del usuario mediante login común
- El conector incluirá automáticamente el header `Authorization: Bearer {token}`

### 5. Flujo típico en una app PowerApps

```powerapps
// Pantalla principal - Al iniciar
With(
    ConnectorVue.IsAuthenticated(),
    If(
        Result,
            // Usuario autenticado: mostrar datos reales
            With(
                ConnectorVue.GetPerfil(),
                Set(
                    usuarioActual,
                    {nombre: perfil.nombre, peso: perfil.peso}
                )
            ),
            // Usuario no autenticado: mostrar pantalla de login
            Navigate("LoginScreen")
    )
)

// Pantalla de "Cómo voy hoy?"
// Al cargar la pantalla
Set(
    lblCalorias,
        ConnectorVue.GetBalance(DateTimeValue(txtFecha.Text)).caloriasConsumidas
)

Set(
    lblPeso,
        ConnectorVue.GetPerfil().peso

// Botón "Calcular objetivo"
UponSelect:
    Set(
        objResultado,
        ConnectorVue.CalcularObjetivo({
            Peso: Value(txtPeso.Text),
            Altura: Value(txtAltura.Text),
            Edad: Value(txtEdad.Text),
            Sexo: DropdownSexo.Selected.Result,
            FactorActividad: SliderFactor.Value,
            TipoObjetivo: DropdownObjetivo.Selected.Value
        })
    )
    // Mostrar resultados en controles de texto
    lblMeta.Text = "Meta: " & objResultado.metaCaloricaEstimada & " kcal"
    lblRiesgo.Text = "Riesgo: " & objResultado.evaluacionObjetivo.descripcion
```

### 6. Migración gradual

**Fase 1**: PowerApps solo lee datos de Vue (solo GET)
**Fase 2**: PowerApps escribe datos (POST para registrar comidas, etc.)
**Fase 3**: PowerApps tiene control total de flujo de trabajo

### 7. Consideraciones importantes

- **Latencia**: Las llamadas API atraviesan Cloudflare Tunnel → .NET API → Vue. ~200-400ms por llamada.
- **Autenticación**: El usuario debe estar logueado en ambas plataformas o usar auth común (Azure AD, etc.)
- **Respaldo**: Si la API de Vue falla, PowerApps puede tener fórmulas locales de respaldo.
- **Versionado**: Cualquier cambio en la API Vue requiere regenerar el conector en PowerApps.

### 8. Migración desde el frontend Vue actual

Los componentes Vue existentes (Inicio.vue, ConversacionView.vue) no necesitan cambios para esto. La API sigue siendo la misma. Lo único que PowerApps necesita es:
- El archivo OpenAPI generado (`docs/openapi/vue-api.yaml`)
- Credenciales de auth (JWT tokens)
- Acceso de red a `https://nexsmart.com.mx/api`