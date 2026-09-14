# Guía — Estrategia de carga de datos (>500 registros, sin lentitud)

> **Conclusión de la verificación (2026-07-27):** los datos están **COMPLETOS** (0 nulls en campos
> clave, el CIF matchea entre cuenta y dirección/teléfono). Lo que veíamos como "datos incompletos"
> era **truncamiento por el tope de carga**, NO datos faltantes. Este doc resuelve la carga a escala.

## 1. Volumen real (Dev, hoy)

| Tabla | Registros | ¿supera 500? | ¿supera 2000? |
|-------|:---:|:---:|:---:|
| Cuentas | 1.001 | sí | no |
| Clientes | 957 | sí | no |
| **Direcciones** | **2.696** | sí | **sí** |
| Teléfonos | 1.058 | sí | no |
| Asignaciones | 1.000 | sí | no |
| Fiadores | 1.737 | sí | no |

En un banco real esto será **decenas de miles**. La estrategia tiene que escalar a eso.

## 2. El problema, en una frase

`ClearCollect(col, 'Tabla')` (carga org-wide) **topa en el "Data row limit" (500 por defecto, máx 2000)**.
Cargar toda la organización en cada celular es inviable (lento por red + se corta).

## 3. La solución: NO cargar toda la organización — cargar SOLO lo del localizador

### Capa 1 (PRODUCCIÓN, celular) — **Perfil Offline** ← la solución real

El **perfil offline** (Configuración → Sin conexión de la app) hace que **cada dispositivo sincronice
SOLO la cartera de SU localizador** (sus asignaciones + las cuentas/clientes/direcciones/teléfonos
relacionadas). Ver [guía 13](13-perfil-offline-rendimiento.md).

- Cada localizador tiene ~60–200 registros por tabla → `ClearCollect` los trae **TODOS, al instante**
  (datos locales, **sin red**), **sin tope**.
- **Escala infinito**: aunque el banco tenga 500.000 clientes, cada celu sigue teniendo ~100.
- **El código de la app ya está diseñado para esto** — carga "lo que hay en el dispositivo".

**Requisitos para que funcione:**
1. **Owner de las asignaciones = el localizador** (automatización owner, [componente 14](../referencia/componentes/14-automatizacion-owner-asignacion.md)).
   Hoy en Dev las asignaciones son de SYSTEM (el flujo nunca corrió) → por eso en el celu no se acota.
2. **Perfil offline configurado** con las relaciones correctas (tabla raíz = Asignacion `All rows`;
   resto `Related rows` por su lookup). Detalle en guía 13.

### Capa 2 (NAVEGADOR / online, para testing) — Data row limit 2000 + Filters delegables

En el navegador (Play) **no hay perfil offline** → carga org-wide. Mitigación:
- **Data row limit = 2000** (Configuración → General). Ya está en 2000. Cubre cuentas/clientes/asignaciones/teléfonos (todas < 2000).
- **Direcciones (2696 > 2000)** → se cargan con `Filter` **delegable** por tipo Casa+Trabajo (1739 < 2000).
  Ya implementado en `App.OnStart`. El servidor devuelve todas las que matchean, paginando, sin toparse.
- Esto hace que el **navegador muestre todo hoy**, pero **NO escala** más allá de 2000 → es solo para demo/testing.

## 4. Regla de oro

> **La app NO debe cargar la organización. Debe cargar la cartera del localizador.**
> En el celu lo garantiza el **perfil offline** (rápido, sin red, sin tope, escala infinito).
> En el navegador es una aproximación (row limit + filtros) solo para probar.

## 5. Acciones pendientes para producción

- [ ] Correr/activar la **automatización de owner** (comp 14) → asignaciones dueñas del localizador.
- [ ] **Configurar el perfil offline** (guía 13) y sincronizar en un celu de prueba.
- [ ] Verificar con Monitor que `OnStart` baja de segundos a **décimas** con el perfil acotado.
