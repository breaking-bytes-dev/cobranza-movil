# El Import y el Diccionario de Datos (CSV de entrada)

> Documento explícito de **cómo entra el dato** a la solución y **qué significa cada columna** del CSV diario.
> Complementa la visión de [`00-arquitectura-macro.md`](00-arquitectura-macro.md) §3.1 y el detalle técnico
> de los mappings en [`../cobranza-import-config.md`](../referencia/cobranza-import-config.md) y
> [`../smelter-overview.md`](../referencia/smelter-overview.md).

---

## 1. El flujo, en lenguaje de negocio

1. El **core bancario** arma, cada día, la lista de **cuentas en mora a cobrar** y la exporta como **CSV**.
2. Ese CSV se sube a Dataverse y lo procesa **Smelter**, que lo **normaliza** (reparte cada fila en varias
   tablas relacionadas, deduplica y resuelve los vínculos por valor de negocio).
3. Resultado: las tablas de import quedan con el **estado del día**, y la app de campo puede mostrarle a cada
   localizador **su cartera**.

- **Frecuencia:** 1 vez al día (mañana).
- **Idempotencia:** reprocesar el mismo archivo **no duplica** (upsert por clave de negocio).
- **El core es la fuente de verdad** de la deuda; esta capa **no la modifica**.

---

## 2. Diccionario de datos — las 32 columnas del CSV

> Significado de **negocio** de cada columna (lo que el core entrega). El mapeo a tablas/tipos está en
> `cobranza-import-config.md`.

### Cliente y ubicación
| Columna | Significado |
|---------|-------------|
| `Cliente` | Nombre del cliente. |
| `CIF` | **Identificador único interno** del cliente (clave de negocio). |
| `DireccionCasa` | Dirección de domicilio del cliente. |
| `DireccionEC` | Dirección donde el cliente quiere recibir el **estado de cuenta**. |
| `DireccionTrabajo` | Dirección del trabajo del cliente. |
| `Departamento` | Departamento geográfico donde se ubica el cliente. |
| `Distrito` | Distrito donde **trabaja** el cliente. |
| `Trabajo` | Nombre del trabajo del cliente. |

### Teléfonos
| Columna | Significado |
|---------|-------------|
| `NumeroCelular` | Teléfono celular del cliente. |
| `NumeroCasa` | Teléfono de la casa del cliente. |

### La cuenta en mora
| Columna | Significado |
|---------|-------------|
| `NumeroCuenta` | Número de cuenta del producto en mora (**tarjeta o préstamo**). Clave de negocio de la cuenta. |
| `SaldoVencidoNIO` | Saldo vencido en **córdobas**. |
| `SaldoVencidoUSD` | Saldo vencido en **dólares**. |
| `TotalSaldoVencido` | Saldo vencido **NIO dolarizado + USD** (total dolarizado). |
| `ContagiadoUSD` | **Saldo total en riesgo** (el saldo total de la cuenta). |
| `PagoMinimoNIO` | Pago mínimo que el cliente debe hacer en **córdobas**. |
| `PagoMinimoUSD` | Pago mínimo que el cliente debe hacer en **dólares**. |
| `DiasMora` | Total de días en mora de la cuenta. |
| `NivelMora` | Nivel moratorio de la cuenta. |
| `FechaUltimoPago` | Última vez que el cliente pagó. |
| `FechaPago` | Fecha en que le **toca pagar** al cliente. |
| `Ciclo` | Ciclo de pago del cliente. |

### Producto y fianza
| Columna | Significado |
|---------|-------------|
| `CodigoProducto` | Código del producto en mora (para clasificarlo). |
| `TipoProducto` | Tipo de producto en mora. |
| `NombreFianza` | Nombre de la fianza de este producto. |
| `DireccionFianza` | Dirección de la fianza de este producto. |

### Cobro en campo (¡clave para la app de campo!)
| Columna | Significado |
|---------|-------------|
| `RetiroNIO` | **Dinero que el localizador va a ir a TRAER** donde el cliente, en córdobas. |
| `RetiroUSD` | **Dinero que el localizador va a ir a TRAER** donde el cliente, en dólares. |
| `TipoEspecial` | Tipo especial de encargo. |
| `Nota` | Nota aclaratoria sobre ese producto en específico. |

### Asignación
| Columna | Significado |
|---------|-------------|
| `TipoAsignacion` | Tipo de asignación: **Diaria**, **Incobrable** o **Especial** (ver §3). |
| `Agente` | Nombre del **Agente** (= localizador) al que se le asigna la tarea. |

---

## 3. Los 3 tipos de asignación (ciclos de vida distintos)

> El `TipoAsignacion` no es un simple atributo: **cada tipo se comporta diferente** y el import los trata
> distinto. Ver **DEC-012** y **DEC-013**.

| Tipo | Frecuencia | Comportamiento |
|------|-----------|----------------|
| **Diaria** | Cambia **a diario** | Saldo, agente, etc. cambian seguido. El **import diario la renueva**. |
| **Incobrable** | 1–2 veces al mes | Se queda **mucho tiempo con el mismo agente**; los saldos casi no se actualizan. **No viene en el CSV diario** → el import **no la borra** por no verla; se **cierra manualmente**. |
| **Especial** | Bajo demanda | **No se carga por CSV** (normalmente). Nace por un **flujo en la app de backoffice** con **aprobación del Supervisor**. |

### El flujo de la asignación Especial (no pasa por el CSV)
1. El rol **Solicitante** (con permiso) **selecciona o crea** el cliente, y **edita** sus datos maestros si
   hace falta (teléfonos, direcciones, etc.). *No es necesariamente un Backoffice* (DEC-014).
2. Escribe la asignación especial. **Siempre tiene número de cuenta.**
3. La asignación va al **Supervisor de localizadores**, que la **aprueba o rechaza** y **decide a qué
   localizador** se asigna.

> Esto implica que las tablas del import (`cliente`, `telefono`, `direccion`, `cuenta`) **dejan de ser
> solo-lectura**: el **Solicitante** también las crea/edita. Atención a la **colisión de CIF** con el upsert
> del import (un cliente creado a mano con un CIF que luego llega por CSV se **fusiona**; si el CIF está mal,
> duplica).

---

## 4. Política de import vs. cambios manuales (DEC-012)

- El import corre **1 vez al día** (mañana).
- Una **reasignación manual** hecha durante el día **se respeta hasta el próximo import**.
- Al día siguiente, el CSV es la **nueva foto** **solo para las Diarias**.
- El import **nunca** pisa **Incobrables** ni **Especiales**, ni el **orden** que el localizador puso a su
  cartera (ese orden vive en `sanic_cm_tbl_asignacion`, fuera del snapshot).

---

## 5. Cómo alimenta la capa operativa

| Del import… | …alimenta |
|-------------|-----------|
| `cuenta`, `cliente`, direcciones, teléfonos | La **cartera** que ve el localizador. |
| `RetiroNIO/USD` | El **objetivo de cobro** del día en la app de campo. |
| `TipoAsignacion` + `Agente` | Puebla `sanic_cm_tbl_asignacion` (Diaria/Incobrable). |
| `cargadiaria` (snapshot) | El **histórico financiero** diario (evolución de mora/saldos). |
