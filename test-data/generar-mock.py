#!/usr/bin/env python3
"""
Generador de CSV mock para probar la configuración de Smelter (Cobranza Móvil).

Produce DOS archivos consistentes (día 1 y día 2) para probar el ciclo completo:
- Día 1 (cobranza-mock-1000.csv,      FechaCarga 2026-06-20)
- Día 2 (cobranza-mock-1000-dia2.csv, FechaCarga 2026-06-21) = el "día siguiente" del día 1.

El día 2 ejercita lo más jugoso del modelo:
- `cuenta` se PISA (upsert por NumeroCuenta): mismas cuentas con DiasMora+1, saldos y agente actualizados.
- `cargadiaria` ACUMULA (alt key NumeroCuenta+FechaCarga): tras importar ambos días -> 2000 snapshots.
- `asignacion`: ~10% de las Diarias se reasignan a otro agente (prueba el cambio de owner).

Todas las FECHAS en formato ISO **yyyy-mm-dd** (Smelter las parsea sin DATEPARSE).
Reproducible (seed fija). Uso:  python3 generar-mock.py [N]   (default N=1000)
"""
import csv, random, sys
from collections import Counter

random.seed(2026)
N = int(sys.argv[1]) if len(sys.argv) > 1 else 1000
RATE = 36.60
FECHA_D1 = "2026-06-20"
FECHA_D2 = "2026-06-21"
OUT_D1 = "test-data/cobranza-mock-1000.csv"
OUT_D2 = "test-data/cobranza-mock-1000-dia2.csv"

FIRST = ["José","Juan","Carlos","Luis","Francisco","Denis","Norman","Wilfredo","Marvin","Róger",
         "María","Ana","Rosa","Martha","Xiomara","Yelba","Karla","Sandra","Gloria","Damaris",
         "Pedro","Mario","Ramón","Álvaro","Eveling"]
LAST = ["López","García","Martínez","Hernández","González","Rodríguez","Pérez","Sánchez","Ramírez",
        "Flores","Gómez","Díaz","Cruz","Morales","Reyes","Gutiérrez","Ortiz","Mendoza","Castro",
        "Vargas","Téllez","Zeledón","Bermúdez","Calero","Aguirre","Salazar","Mejía","Obando"]
DEPARTAMENTOS = ["Managua","León","Granada","Masaya","Chinandega","Estelí","Matagalpa","Carazo",
                 "Rivas","Jinotega","Nueva Segovia","Boaco","Chontales","Madriz"]
TRABAJOS = ["Supermercado La Colonia","Zona Franca Las Mercedes","BAC Credomatic","Claro Nicaragua",
            "Alcaldía de Managua","Hospital Vivian Pellas","Universidad UCA","Tip Top Industrial",
            "Walmart Nicaragua","Café Soluble","Ingenio San Antonio","Independiente"]
PRODUCTOS = [("TC001","Tarjeta Clásica"),("TC002","Tarjeta Oro"),("TC003","Tarjeta Platino"),
             ("PP010","Préstamo Personal"),("PV020","Préstamo Vehículo"),("PH030","Préstamo Hipotecario"),
             ("PE040","Préstamo Express")]
AGENTES = ["Carlos Mendoza","María López","Denis Gutiérrez","Xiomara Reyes","Norman Castro",
           "Karla Vargas","Wilfredo Díaz","Sandra Téllez","Marvin Obando","Gloria Mejía",
           "Róger Salazar","Damaris Calero","Mario Aguirre","Eveling Bermúdez","Álvaro Zeledón",
           "Yelba Ortiz","Ramón Cruz","Martha Flores"]
FIADORES = ["","","",
            "Comercial El Halcón","Distribuidora Selecta","Inversiones del Norte","Fianzas Centroamérica",
            "Crédito Seguro SA","Garantías del Pacífico","Avales Nicaragüenses","Respaldo Total"]
TIPO_ESPECIAL = ["","","","","","Reestructurado","Judicial","VIP","Convenio de pago"]
NOTAS = ["","","","","","Cliente promete pagar fin de mes","Cambió de domicilio","No contesta el teléfono",
         "Negocio cerrado temporalmente","Solicita arreglo de pago","Familiar atiende"]
REFS = ["de la rotonda Metrocentro","de la iglesia El Calvario","del parque central","de la gasolinera Uno",
        "del mercado Oriental","de la escuela pública","del puente El Edén","de la farmacia Cosep"]
DIRS = ["lago","sur","arriba","abajo"]

COLS = ["Cliente","CIF","DireccionCasa","DireccionEC","DireccionTrabajo","Departamento","NumeroCelular",
        "NumeroCasa","NumeroCuenta","SaldoVencidoNIO","SaldoVencidoUSD","TotalSaldoVencido","ContagiadoUSD",
        "PagoMinimoNIO","PagoMinimoUSD","DiasMora","NivelMora","FechaUltimoPago","FechaPago","Ciclo",
        "CodigoProducto","TipoProducto","NombreFianza","DireccionFianza","RetiroNIO","RetiroUSD",
        "TipoEspecial","Nota","Distrito","Trabajo","TipoAsignacion","Agente","FechaCarga"]

def cedula():
    return (f"{random.randint(1,799):03d}-{random.randint(1,28):02d}{random.randint(1,12):02d}"
            f"{random.randint(60,99):02d}-{random.randint(0,9999):04d}{random.choice('ABCDEFGHJKLMNPQRTUVWXY')}")

def direccion():
    s = (f"{random.choice(REFS)} {random.randint(1,8)} cuadras al {random.choice(DIRS)} "
         f"- casa #{random.randint(100,9999)}")
    return s[0].upper() + s[1:]

def fecha(anio_min, anio_max, mes_min=1, mes_max=12):
    # ISO yyyy-mm-dd. Orden de randint preservado (día, mes, año) para reproducibilidad.
    d = random.randint(1, 28); m = random.randint(mes_min, mes_max); y = random.randint(anio_min, anio_max)
    return f"{y}-{m:02d}-{d:02d}"

def fecha_pasada():
    # Anterior a FechaCarga del día 1: todo 2025, o ene-may 2026.
    return fecha(2025, 2025) if random.random() < 0.5 else fecha(2026, 2026, 1, 5)

def nivel(dias):
    return 1 if dias<=30 else 2 if dias<=60 else 3 if dias<=90 else 4 if dias<=120 else 5

def derivados(sv_nio, sv_usd):
    total = round(sv_nio / RATE + sv_usd, 2)
    pm_nio = round(sv_nio * random.uniform(0.05, 0.15), 2)
    pm_usd = round(sv_usd * random.uniform(0.05, 0.15), 2)
    return total, pm_nio, pm_usd

# --- clientes (datos consistentes por CIF) ---
clientes = []
for _ in range(int(N * 0.66)):
    casa = direccion()
    clientes.append({
        "Cliente": f"{random.choice(FIRST)} {random.choice(FIRST)} {random.choice(LAST)} {random.choice(LAST)}",
        "CIF": cedula(),
        "DireccionCasa": casa,
        "DireccionEC": casa if random.random() < 0.6 else direccion(),
        "DireccionTrabajo": direccion(),
        "Departamento": random.choice(DEPARTAMENTOS),
        "NumeroCelular": f"{random.choice('578')}{random.randint(1000000,9999999)}",
        "NumeroCasa": f"2{random.randint(2000000,7999999)}",
        "Distrito": f"Distrito {random.choice('IVX')}",
        "Trabajo": random.choice(TRABAJOS),
    })

def tipo_asignacion():
    r = random.random()
    return "Diaria" if r < 0.78 else ("Incobrable" if r < 0.95 else "Especial")

# --- DÍA 1 ---
dia1 = []
usados = set()
while len(dia1) < N:
    c = random.choice(clientes)
    cod, tprod = random.choice(PRODUCTOS)
    while True:
        ncuenta = f"{random.choice(['5412','4556','7011','8030'])}{random.randint(100000000,999999999)}"
        if ncuenta not in usados:
            usados.add(ncuenta); break
    if random.random() < 0.7:
        sv_nio = round(random.uniform(800, 60000), 2); sv_usd = 0.0
    else:
        sv_nio = 0.0; sv_usd = round(random.uniform(50, 3000), 2)
    total, pm_nio, pm_usd = derivados(sv_nio, sv_usd)
    contagiado = round(total * random.uniform(1.2, 4.0), 2)
    dias = random.randint(1, 180)
    fiad = random.choice(FIADORES)
    dia1.append({
        **c,
        "NumeroCuenta": ncuenta,
        "SaldoVencidoNIO": sv_nio, "SaldoVencidoUSD": sv_usd,
        "TotalSaldoVencido": total, "ContagiadoUSD": contagiado,
        "PagoMinimoNIO": pm_nio, "PagoMinimoUSD": pm_usd,
        "DiasMora": dias, "NivelMora": nivel(dias),
        "FechaUltimoPago": fecha_pasada(), "FechaPago": fecha(2026, 2026, 6, 7),
        "Ciclo": random.choice(["05","10","15","20","25","28"]),
        "CodigoProducto": cod, "TipoProducto": tprod,
        "NombreFianza": fiad, "DireccionFianza": direccion() if fiad else "",
        "RetiroNIO": sv_nio, "RetiroUSD": sv_usd,
        "TipoEspecial": random.choice(TIPO_ESPECIAL), "Nota": random.choice(NOTAS),
        "TipoAsignacion": tipo_asignacion(),
        "Agente": random.choice(AGENTES),
        "FechaCarga": FECHA_D1,
    })

# --- DÍA 2 (derivado del día 1) ---
n_pagos = 0; n_reasig = 0
dia2 = []
for r in dia1:
    r2 = dict(r)
    dias = int(r["DiasMora"]) + 1
    sv_nio = float(r["SaldoVencidoNIO"]); sv_usd = float(r["SaldoVencidoUSD"])
    if random.random() < 0.15:                      # pago parcial ayer
        f = random.uniform(0.2, 0.7)
        sv_nio = round(sv_nio * (1 - f), 2); sv_usd = round(sv_usd * (1 - f), 2)
        r2["FechaUltimoPago"] = FECHA_D1
        n_pagos += 1
    else:                                           # interés de mora leve
        sv_nio = round(sv_nio * random.uniform(1.0, 1.01), 2)
        sv_usd = round(sv_usd * random.uniform(1.0, 1.01), 2)
    total, pm_nio, pm_usd = derivados(sv_nio, sv_usd)
    r2.update({"SaldoVencidoNIO": sv_nio, "SaldoVencidoUSD": sv_usd, "TotalSaldoVencido": total,
               "PagoMinimoNIO": pm_nio, "PagoMinimoUSD": pm_usd, "RetiroNIO": sv_nio, "RetiroUSD": sv_usd,
               "DiasMora": dias, "NivelMora": nivel(dias), "FechaCarga": FECHA_D2})
    if r["TipoAsignacion"] == "Diaria" and random.random() < 0.10:   # reasignación
        otros = [a for a in AGENTES if a != r["Agente"]]
        r2["Agente"] = random.choice(otros); n_reasig += 1
    dia2.append(r2)

def escribir(path, rows):
    with open(path, "w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=COLS); w.writeheader(); w.writerows(rows)

escribir(OUT_D1, dia1)
escribir(OUT_D2, dia2)

# --- resumen ---
cifs = {r["CIF"] for r in dia1}
tipos = Counter(r["TipoAsignacion"] for r in dia1)
nio = sum(1 for r in dia1 if r["SaldoVencidoNIO"] > 0)
print(f"DÍA 1 -> {OUT_D1}   (FechaCarga {FECHA_D1})")
print(f"DÍA 2 -> {OUT_D2}   (FechaCarga {FECHA_D2})")
print(f"Filas por archivo:      {len(dia1)}")
print(f"Clientes únicos (CIF):  {len(cifs)}   |  máx cuentas/cliente: {max(Counter(r['CIF'] for r in dia1).values())}")
print(f"Agentes: {len({r['Agente'] for r in dia1})}  Productos: {len({r['CodigoProducto'] for r in dia1})}  Fiadores: {len({r['NombreFianza'] for r in dia1 if r['NombreFianza']})}")
print(f"TipoAsignacion (día 1): {dict(tipos)}")
print(f"Moneda (día 1):         NIO={nio}  USD={len(dia1)-nio}")
print(f"Día 2 — pagos parciales: {n_pagos}   reasignaciones: {n_reasig}")
print(f"Tras importar ambos días: cuenta=1000 (pisa)  |  cargadiaria=2000 (acumula)")
print(f"Fechas en ISO yyyy-mm-dd. Columnas: {len(COLS)}")
