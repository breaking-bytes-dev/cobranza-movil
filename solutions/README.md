# Soluciones — Estrategia ALM de dos planos

> Adaptada de `bitbreaker01/sigil` (ver [docs/historico/desarrollo/10 §Fase 3](../docs/historico/desarrollo/10-estructura-del-repositorio.md)).

La solución `Cobranza Móvil` vive en **dos planos**, cada uno con un propósito distinto:

## Plano 1 — GitHub Releases (los desplegables)

Cada release `sol-vX.Y.Z.B` lleva como **assets** los dos zips exportados desde Dev con `pac solution export`:

| Asset | Rol |
|-------|-----|
| `*_unmanaged.zip` | **Fuente de verdad de Dev.** Es lo que se re-importa para reconstruir/continuar. |
| `*_managed.zip` | **Release sellado** para Test/Prod. No se edita. |

Los zips **no** entran al árbol de git (`.gitignore`): pesan, no diffean y su lugar es el Release.

## Plano 2 — `unpacked/` (el rastro en git)

`pac solution unpack` del zip **unmanaged**, con **poda** de los componentes que no son metadata pura:

- Se **excluye** `CanvasApps/` → la app del localizador **no soporta round-trip** del Solution Packager
  (misma limitación que sigil documenta para su Code App). Su fuente de verdad es el zip del Release.
- Queda: tablas (Entity.xml, formularios, vistas), choices, roles, flujos, variables de entorno,
  `Solution.xml` — todo **diffeable** commit a commit.

> ⚠️ `unpacked/` es **histórico/diff**, NO fuente de reconstrucción. Nunca edites acá esperando que
> vuelva al entorno.

## Ciclo (cuando la Fase 4 esté activa)

```
Portal Dev ──(workflow solution-release, manual con input version)──▶ Release sol-vX.Y.Z.B (zips)
                                                                        │
                     main ◀──(workflow solution-sync: unpack + poda + commit)──┘
```

Mientras tanto (manual): `pac solution export` → subir zips al Release → `pac solution unpack` local →
commit de `unpacked/`.
