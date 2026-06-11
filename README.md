# DmEletrico

Suplemento (add-in) do Autodesk Revit para automação BIM de projetos elétricos
de baixa tensão, em conformidade com a **ABNT NBR 5410**. Sem autenticação,
login ou licença — todas as funcionalidades ficam disponíveis após o
carregamento do `.addin`.

## Estado atual

**Esqueleto completo e compilável.** A infraestrutura (Ribbon dinâmica,
habilitação por `IExternalCommandAvailability`, injeção de parâmetros
compartilhados, motor de cálculo NBR 5410 e diálogo de Setup em WPF) está
implementada. Os módulos de roteamento, anotação e documentação estão
registrados como comandos com stubs prontos para implementação.

| Módulo | Comando | Atalho | Status |
|---|---|---|---|
| 2 — Setup | `DmSetup` | — | ✅ Funcional (WPF + injeção de parâmetros) |
| 3 — Conduit Builder | `DmConduitBuilder` | CB | 🟡 Stub |
| 4 — Route Fit | `DmRouteFit` | RF | 🟡 Stub |
| 5 — Motor de cálculo | (interno) | — | ✅ `ElectricalCalculator` + tabelas |
| 6 — Desconectados | `DmCheckDisconnected` | — | ✅ Varredura funcional |
| 7 — Auto/Manual TAG | `DmAutoTag` / `DmManualTag` | MT | 🟡 Stub |
| 8 — Central de Doc. | `DmDocCenter` | DC | 🟡 Stub |
| 9 — Quadros de Cargas | `DmLoadSchedule` | — | 🟡 Stub |
| 10 — Unifilar | `DmUnifilar` | — | 🟡 Stub |
| 13 — Quantitativos | `DmMaterials` | — | 🟡 Stub |

## Requisitos de build

- **.NET SDK 8.0** (alvo primário Revit **2025**, `net8.0-windows`).
- Os assemblies da Revit API vêm dos pacotes NuGet `Nice3point.Revit.Api.*`
  (reference-only) — **não é necessário** ter o Revit instalado para compilar.

```powershell
dotnet build DmEletrico.sln -c Debug
```

A versão alvo é controlada por `RevitVersion` em `Directory.Build.props`. Para
outra versão:

```powershell
dotnet build -p:RevitVersion=2024   # net48, pacotes 2024.*
```

> Revit 2022–2024 usam **.NET Framework 4.8** (`net48`); 2025+ usa **.NET 8**.
> A `Directory.Build.props` seleciona o TFM automaticamente pela `RevitVersion`.

## Instalação no Revit

No build **Debug**, o `.csproj` copia `Resources/DmEletrico.addin` para
`%AppData%\Autodesk\Revit\Addins\<versao>\`. Edite o caminho do `<Assembly>`
nesse `.addin` para o `DmEletrico.dll` gerado, ou use um caminho absoluto fixo.

Ao abrir o Revit, surge a aba **DmEletrico**. O botão **Setup** está sempre
ativo; os demais ficam cinza até o modelo conter elementos elétricos
(`OST_ElectricalFixtures`, `OST_LightingFixtures`, `OST_ElectricalEquipment`).

## Estrutura

```
DmEletrico.sln
Directory.Build.props            # RevitVersion → TFM + versão dos pacotes
src/DmEletrico/
  Application/                   # IExternalApplication + construção da Ribbon
  Availability/                  # IExternalCommandAvailability (habilitação dinâmica)
  Commands/                      # Comandos Dm* (IExternalCommand)
  Core/                          # Parâmetros, settings, injeção, cálculo
    Calculation/                 # Motor NBR 5410 + tabelas
  UI/Setup/                      # Diálogo WPF de Setup
  Resources/                     # .addin, atalhos de teclado
```

## Aviso técnico — tabelas NBR 5410

As tabelas em `Core/Calculation/Nbr5410Tables.cs` (FCT, FCA, capacidade de
condução, seções) são uma **base de partida representativa** e devem ser
revisadas/completadas contra a edição vigente da norma antes do uso em projeto
real, em especial as tabelas de capacidade por método de instalação.
