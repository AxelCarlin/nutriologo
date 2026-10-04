# Fase I5 — Chat/orb

## Backend

- `POST /api/conversacion/mensaje`: `IChatInterpreter` (Ollama Qwen) → `StructuredOutputValidator` → intención → `ClarificationPolicy` → `RegisterMealValidator` → `MealRegistrar` (compartido con `POST /api/comidas`) → `RegistroComida` persistido.
- Estructura de respuesta: `tipo: registrado | aclaracion`, pregunta priorizada, motivo.
- Errores: IA no disponible/salida inválida → 502; validación → 400 con aclaración. Nada se persiste ante ambigüedad o alimentos desconocidos (guard G8 verificado e2e: "Comí pizza" → aclaración, sin persistencia).

## Verificación

- "Comí dos huevos y dos tortillas" → `{"tipo":"registrado","id":14}`; `GET /api/balance` devuelve la suma derivada (7.46 kcal en el run de prueba — limitación PIEZA→g documentada en I4).
- "Comí pizza" → aclaración "preparación", sin persistencia. Todo con el modelo real Qwen 7B Q4_K_M y guard de G8.
- `MealRegistrar` refactorizado para compartir persistencia entre formulario y chat.
- Frontend `ConversacionView` (`/chat`): historial, input, estados de envío/error, confirmación visual; build ✅, navegación actualizada.

## Tests

- Backend: Domain 73/73, Application 43/43, Infrastructure 3/3 (incl. Ollama real). Frontend Vitest 1/1.
- E2E manual verificado con curl contra Ollama real (flujo feliz + aclaración por pizza + aclaración por preparación).
