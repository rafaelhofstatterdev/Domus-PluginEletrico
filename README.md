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
| 3 — Conduit Builder | `DmConduitBuilder` | CB | ✅ Roteamento 3D + fittings + dimensionamento |
| 4 — Route Fit | `DmRouteFit` | RF | ✅ Limpeza de geometria inválida |
| 5 — Motor de cálculo | (interno) / `DmConduitDetail` | — | ✅ `ElectricalCalculator` + janela de detalhamento |
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

## Instalação no Revit (passo a passo)

### Pré-requisitos
- **Autodesk Revit 2025** instalado.
- **.NET SDK 8.0+** (ou Visual Studio 2022 17.8+) para compilar.

### Passos

1. **Clone o repositório**
   ```powershell
   git clone https://github.com/rafaelhofstatterdev/Domus-PluginEletrico.git
   cd Domus-PluginEletrico
   ```

2. **Compile a solução** (com o Revit fechado)
   ```powershell
   dotnet build DmEletrico.sln -c Debug
   ```
   No build **Debug**, o `.csproj` já copia automaticamente
   `Resources/DmEletrico.addin` para
   `%AppData%\Autodesk\Revit\Addins\2025\`.

3. **Confira o caminho do assembly no `.addin`**
   Abra `%AppData%\Autodesk\Revit\Addins\2025\DmEletrico.addin` e garanta que a
   tag `<Assembly>` aponta para o `DmEletrico.dll` gerado. O padrão é relativo:
   ```xml
   <Assembly>..\..\..\src\DmEletrico\bin\x64\Debug\DmEletrico.dll</Assembly>
   ```
   Se o Revit não carregar o suplemento, troque por um **caminho absoluto**, ex.:
   ```xml
   <Assembly>C:\Users\SEU_USUARIO\Desktop\Codigos\Domus-PluginEletrico\src\DmEletrico\bin\x64\Debug\DmEletrico.dll</Assembly>
   ```

4. **Abra o Revit 2025.** Na primeira carga o Revit pode exibir um aviso de
   segurança de suplemento — escolha **Always Load / Sempre carregar**.

5. **Valide:** abra (ou crie) um projeto. Deve surgir a aba **DmEletrico** na
   Ribbon. O botão **Setup** fica sempre ativo; os demais permanecem cinza até o
   modelo conter elementos elétricos (`OST_ElectricalFixtures`,
   `OST_LightingFixtures`, `OST_ElectricalEquipment`).

6. **Rode o Setup** uma vez para injetar os parâmetros compartilhados e liberar
   os comandos de cálculo/roteamento.

### Atualizar após mudanças no código
Feche o Revit, rode `dotnet build` novamente e reabra o Revit. (O Revit carrega
o `.dll` no início da sessão; não há recarga a quente.)

### Atalhos de teclado (opcional)
Importe os atalhos **CB / DC / MT / RF** conforme
`src/DmEletrico/Resources/KeyboardShortcuts.txt`, via
*Arquivo > Opções > Atalhos de Teclado > Importar*.

## Estrutura

```
DmEletrico.sln
Directory.Build.props            # RevitVersion → TFM + versão dos pacotes
src/DmEletrico/
  Application/                   # IExternalApplication + construção da Ribbon
  Availability/                  # IExternalCommandAvailability (habilitação dinâmica)
  Commands/                      # Comandos Dm* (IExternalCommand)
  Core/                          # Parâmetros, settings, injeção, cálculo
    Calculation/                 # Motor NBR 5410 + tabelas + dimensionamento de eletroduto
    Routing/                     # Roteamento ortogonal, Conduit Builder, Route Fit
  UI/Setup/                      # Diálogo WPF de Setup
  UI/Detail/                     # Janela WPF de detalhamento do trecho
  Resources/                     # .addin, atalhos de teclado
```

## Aviso técnico — tabelas NBR 5410

As tabelas em `Core/Calculation/Nbr5410Tables.cs` (FCT, FCA, capacidade de
condução, seções) são uma **base de partida representativa** e devem ser
revisadas/completadas contra a edição vigente da norma antes do uso em projeto
real, em especial as tabelas de capacidade por método de instalação.
