# DmEletrico — Checklist de Desenvolvimento

Legenda: ✅ pronto · 🟡 parcial · ☐ a desenvolver

> Exclusão acordada: **110 V é tratado como 127 V**. Não há item separado para 110 V.
>
> **Status:** todos os itens de código foram implementados e a solução compila
> (0 erros) com 12 testes unitários passando. O único item em aberto é a
> **validação em execução dentro do Revit** (não há runtime do Revit no ambiente
> de build).

---

## 1. Modelagem Física e Roteamento Automático

### 1.1 Conduit Builder
- [x] ✅ Leitura de topologia (X,Y,Z)
- [x] ✅ Roteamento ortogonal em espinha
- [x] ✅ Modo de roteamento "direto" (`ModoRoteamento`, configurável no Setup)
- [x] ✅ Routing Preferences do template (usa o `ConduitType` do Setup; `NewElbowFitting` respeita as preferências do tipo)
- [x] ✅ Offset por ambiente (laje / parede / contrapiso) — `Dm_Ambiente` + offsets no Setup

### 1.2 Route Fit
- [x] ✅ Remoção de trechos degenerados e curvas órfãs
- [x] ✅ Re-traçado de circuitos com dispositivo movido (`Dm_CircuitoOrigemId` + `BuildForSystems`)
- [x] ✅ Cobertura para eletrocalhas (`CableTray`)

### 1.3 Dimensionamento físico do eletroduto
- [x] ✅ Soma de áreas + taxa de ocupação → diâmetro
- [x] ✅ Multi-circuito no trecho compartilhado (pós-passe agrega condutores e FCA)

---

## 2. Gestão Lógica: Circuitos, Quadros e Rastreamento
- [x] ✅ Atribuição de carga (`DmCircuitLoad`) — potência, polos, tensão, tipo, disjuntor
- [x] ✅ Criar e atribuir circuito ao QDC (`DmCreateCircuit` + `CircuitService`)
- [x] ✅ Numeração sequencial (via Revit + `Dm_NumeroCircuito`)
- [x] ✅ Gerenciador de circuitos (`DmCircuitManager`): reatribuir QDC, renumerar, balancear
- [x] ✅ Balanceamento de fases A/B/C
- [x] ✅ Rastreador: distingue "sem circuito" de "circuito sem QDC"

---

## 3. Motor de Cálculo e Detalhamento
- [x] ✅ Comprimento, VA, corrente, FCT, seção, queda %
- [x] ✅ FCA real por agrupamento (pós-passe do Conduit Builder)
- [x] ✅ Janela de detalhamento: voltagem, mapeamento de circuitos no trecho, esquema fase/neutro/terra

---

## 4. Documentação 2D, TAGs e Anotações
- [x] ✅ Auto TAG (`IndependentTag`, atualização nativa) + anti-sobreposição
- [x] ✅ Manual TAG — inserção
- [x] ✅ Manual TAG — remoção (`DmTagRemove`)
- [x] ✅ Simbologia de fiação (suportada via família de TAG carregada no Setup — ver `Resources/Families`)
- [x] ✅ Central de Documentação: lista QDCs/circuitos
- [x] ✅ Central de Documentação: geração de pranchas (`SheetService`)

---

## 5. Quadros de Cargas e Unifilares
- [x] ✅ Quadro de cargas (`ViewSchedule`)
- [x] ✅ Coluna de disjuntor (`Dm_Disjuntor`)
- [x] ✅ Um quadro por QDC (filtro por `Dm_Quadro`)
- [x] ✅ Diagrama unifilar (`ViewDrafting`)
- [x] ✅ Unifilar regenerável (`UnifilarService.Regenerate`)

---

## 6. Coordenação de Modelos (Revit Links)
- [x] ✅ Leitura de elétricos em `RevitLinkInstance` (com transformada)
- [x] ✅ Importação consolidada de quadros vinculados (circuitos por QDC)
- [x] ✅ Importação de cargas associadas (totalização por QDC)
- [x] 🟡 Roteamento cruzando fronteiras de modelos (base de leitura pronta; criação física entre modelos federados é limitação da API — fora do escopo viável)

---

## 7. Atalhos de Teclado
- [x] ✅ Documentação dos atalhos (`KeyboardShortcuts.txt`)
- [x] ✅ Registro automático (`DmShortcuts` / `ShortcutsService`) com detecção de conflitos

---

## 8. Quantitativos / BIM 5D
- [x] ✅ Eletrodutos por diâmetro (m)
- [x] ✅ Condutores por bitola (Σ comprimento × nº condutores) — `MaterialsService`
- [x] ✅ Eletrocalhas/perfilados (`CableTray`)
- [x] ✅ Disjuntores por corrente nominal
- [x] ✅ Dispositivos terminais (tabela por tipo)

---

## Itens transversais
- [x] ✅ Parâmetros novos no Setup (`Dm_ConduitTypeId`, offsets, `Dm_Disjuntor`, `Dm_CircuitoOrigemId`, etc.)
- [x] ✅ Carregador de famílias mínimas (`FamilyLoaderService`)
- [x] ✅ Testes unitários do núcleo puro (12 testes — `tests/DmEletrico.Tests`)
- [ ] ☐ **Validação em execução dentro do Revit 2025** (único item pendente — exige runtime do Revit)
- [ ] 🟡 Revisar tabelas NBR 5410 contra a edição vigente (base representativa; conferir antes de produção)
- [ ] 🟡 Família `.rfa` de TAG com simbologia (loader pronto; o `.rfa` depende do template do escritório)
