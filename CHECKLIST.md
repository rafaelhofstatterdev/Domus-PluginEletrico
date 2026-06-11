# DmEletrico — Checklist de Desenvolvimento

Legenda: ✅ pronto · 🟡 parcial (precisa completar) · ☐ a desenvolver

> Exclusão acordada: **110 V é tratado como 127 V** (mesmo valor lógico). Não há
> item separado para 110 V — onde aparecer "110", mapear para 127.

---

## 1. Modelagem Física e Roteamento Automático

### 1.1 Conduit Builder
- [x] ✅ Leitura de topologia (X,Y,Z) dos conectores/dispositivos
- [x] ✅ Roteamento ortogonal em espinha (dispositivo → X → Y → painel)
- [ ] ☐ **Modo de roteamento "direto"** (linha mais curta) como opção além do ortogonal
  - Adicionar enum `ModoRoteamento { Ortogonal, Direto }` em `DmProjectSettings` + Setup
  - `OrthogonalRouter` ganha variante `RouteDireto(from, to)`
- [ ] ☐ **Routing Preferences do template**
  - Ler `RoutingPreferenceManager` do `ConduitType` selecionado no Setup
  - Usar tipos de curva/junção/transição definidos nas preferências ao criar fittings
  - Persistir o `ConduitType` escolhido no Setup (`Dm_ConduitTypeId`) e usá-lo no builder
- [ ] ☐ **Offset por tipo de ambiente (laje / parede / contrapiso)**
  - Trocar `Dm_AlturaRoteamento` único por 3 offsets configuráveis: `Dm_OffsetLaje`, `Dm_OffsetParede`, `Dm_OffsetContrapiso`
  - Classificar cada dispositivo por ambiente (parâmetro do dispositivo ou heurística por elevação) e escolher o offset
  - `OrthogonalRouter` recebe a elevação resolvida por dispositivo

### 1.2 Route Fit (completar)
- [x] ✅ Remoção de trechos degenerados e curvas órfãs
- [ ] 🟡 **Recalcular trechos após movimentação de dispositivo**
  - Armazenar, em cada conduíte criado, o `Id` do circuito e do dispositivo de origem (parâmetros `Dm_CircuitoOrigemId` / `Dm_DispositivoId`)
  - Detectar divergência entre a ponta do conduíte e a posição atual do conector
  - Re-traçar apenas os trechos afetados (reusar `ConduitBuilderService` para o circuito)
- [ ] ☐ Detecção de ângulos não suportados (não-ortogonais) e correção
- [x] ✅ Cobertura para eletrocalhas (`CableTray`) na limpeza

### 1.3 Dimensionamento físico do eletroduto (completar)
- [x] ✅ Soma de áreas de seção + taxa de ocupação → diâmetro nominal
- [ ] 🟡 **Somar múltiplos circuitos que compartilham o mesmo trecho**
  - Após criar a malha, identificar conduítes coincidentes/compartilhados (mesma `LocationCurve`)
  - Acumular nº de condutores e áreas de todos os circuitos no trecho
  - Redimensionar diâmetro pelo total
- [ ] ☐ Atualização do diâmetro em tempo real ao alterar carga (ver 4.x — reação a mudança)

---

## 2. Gestão Lógica: Circuitos, Quadros e Rastreamento

- [x] ✅ Atribuição de carga: potência, polos, tensão, tipo (`DmCircuitLoad`)
- [ ] ☐ **Criar e atribuir circuito (`ElectricalSystem`) ao QDC**
  - Comando `DmCreateCircuit`: a partir de dispositivos selecionados, `ElectricalSystem.Create(...)` e `SelectPanel(painel)`
  - Diálogo para escolher o QDC de destino
- [ ] ☐ **Numeração sequencial inteligente de circuitos**
  - Ao criar/atribuir, calcular o próximo número livre por QDC (considerar polos/fases ocupados)
  - Gravar em `Dm_NumeroCircuito` e/ou no número de circuito nativo
- [ ] ☐ **Gerenciador de circuitos / reorganização**
  - Painel WPF: mover circuito entre QDCs, renumerar, trocar de fase
  - Reaproveitar o balanceamento de fases existente
- [x] ✅ Balanceamento de fases A/B/C (`DmPhaseBalance`)
- [x] ✅ Rastreador de desconectados (sem circuito)
- [ ] 🟡 Distinguir "sem circuito" de "sem QDC atribuído" no rastreador (dois alertas)

---

## 3. Motor Algorítmico de Cálculo e Detalhamento

- [x] ✅ Cálculo: comprimento, VA, corrente de projeto, FCT, seção, queda %
- [ ] 🟡 **FCA real por agrupamento** (hoje fixo em 1)
  - Contar circuitos que compartilham cada trecho (depende de 1.3) e aplicar `Nbr5410Tables.Fca(n)`
- [ ] 🟡 **Janela de detalhamento — completar**
  - [ ] ☐ Adicionar linha **Voltagem**
  - [ ] ☐ Adicionar **mapeamento dos circuitos no trecho** (lista de circuitos que passam)
  - [ ] ☐ **Representação esquemática** dos circuitos (mini-desenho dos condutores: fase/neutro/terra)
- [x] ✅ Acesso por seleção + botão (clique-direito não é suportado pela API pública — manter)

---

## 4. Automação de Documentação 2D, TAGs e Anotações

- [x] ✅ Auto TAG (insere `IndependentTag`, atualização nativa)
- [ ] 🟡 **Simbologia de fiação na TAG (traços fase/neutro/terra)**
  - Criar/empacotar família de anotação `.rfa` que exibe `Dm_NumeroCircuito`, `Dm_SecaoAdotada` e os traços
  - Carregar a família no Setup se ausente
- [x] ✅ Manual TAG — inserção
- [ ] ☐ **Manual TAG — editar e remover**
  - Comando para selecionar TAG existente e editar conteúdo / apagar
  - Anti-sobreposição: deslocar TAG se colidir com outra
- [ ] 🟡 **Central de Documentação — gerenciador de vistas/tabelas/pranchas**
  - [ ] ☐ Listar e abrir vistas de planta elétrica
  - [ ] ☐ Listar/criar `ViewSheet` (pranchas) e posicionar viewports
  - [ ] ☐ Vincular quadros de cargas e unifilares às pranchas

---

## 5. Diagramação: Quadros de Cargas e Unifilares

- [x] ✅ Quadro de cargas (`ViewSchedule`)
- [ ] ☐ **Coluna de disjuntor (A)** no quadro de cargas
  - Calcular `Nbr5410Tables.DisjuntorComercial` por circuito e gravar `Dm_Disjuntor`
- [ ] ☐ **Um quadro por pavimento/zona** (QD-Superior, QD-Térreo, QD-Técnico)
  - Gerar um `ViewSchedule` por QDC com filtro pelo painel, nomeado pelo QDC
- [x] ✅ Diagrama unifilar (`ViewDrafting`: barramento, disjuntor geral, ramais, bitola, destino)
- [ ] 🟡 **Unifilar sincronizado (regeração)**
  - Comando "Atualizar unifilar" que apaga/recria o conteúdo do `ViewDrafting` do QDC
  - (Opcional avançado) `IUpdater` para refletir mudanças de carga automaticamente

---

## 6. Coordenação de Modelos (Revit Links)

- [x] ✅ Leitura de elétricos em `RevitLinkInstance` (com transformada)
- [ ] ☐ **Importação consolidada de quadros vinculados**
  - Mapear QDCs dos links e consolidar lista no hospedeiro (relatório + estrutura de dados)
- [ ] ☐ **Importação de cargas associadas**
  - Extrair potência/corrente/classificação dos dispositivos vinculados para totalização no hospedeiro
- [ ] ☐ **Identificadores de fiação interagindo com elementos vinculados**
  - Permitir TAG de conduíte do hospedeiro referenciando dados de circuito do link
- [ ] ☐ (Avançado) **Roteamento de conduíte cruzando fronteiras de modelos**

---

## 7. Atalhos de Teclado

- [x] ✅ Documentação dos atalhos CB / DC / MT / RF (`KeyboardShortcuts.txt`)
- [ ] ☐ **Aplicação/registro automático**
  - Gerar ou mesclar o `KeyboardShortcuts.xml` da versão do Revit
  - Detectar e reportar conflitos com comandos nativos antes de aplicar

---

## 8. Quantitativos, Lista de Materiais e BIM 5D

- [x] ✅ Eletrodutos por diâmetro (m) — `ViewSchedule`
- [ ] ☐ **Condutores por bitola (m)**
  - Opção A: modelar fios nativos (`Wire`) por circuito; Opção B: tabela calculada = Σ(comprimento do conduíte × nº de condutores) por seção
  - Gerar `ViewSchedule`/relatório por bitola
- [ ] ☐ **Perfilados / bandejas / eletrocalhas (`CableTray`)** no quantitativo
- [ ] ☐ **Dispositivos de proteção (disjuntores, DR, DPS)**
  - Requer modelagem desses elementos (família/parâmetros) ou extração do quadro de cargas
- [ ] 🟡 **Dispositivos terminais (tomadas, luminárias, caixas)** — ampliar o quadro por categoria/família além de `ElectricalFixtures`

---

## Itens transversais (infra)
- [ ] ☐ Parâmetros novos no Setup: `Dm_ConduitTypeId`, `Dm_OffsetLaje/Parede/Contrapiso`, `Dm_Disjuntor`, `Dm_CircuitoOrigemId`
- [ ] ☐ **Validação em execução dentro do Revit 2025** (nada testado em runtime ainda)
- [ ] ☐ Empacotar famílias mínimas (`.rfa`): TAG de conduíte, tipo de conduíte
- [ ] ☐ Revisar tabelas NBR 5410 (`Nbr5410Tables`, `ConduitSizing`) contra a norma vigente
- [ ] ☐ Testes unitários do núcleo puro (`ElectricalCalculator`, `OrthogonalRouter`, `ConduitSizing`, `PhaseBalanceService`)

---

## Ordem sugerida de execução (prioridade)
1. **Criar/atribuir circuitos a QDC + numeração sequencial** (§2) — destrava o fluxo lógico
2. **Multi-circuito no trecho + FCA real** (§1.3, §3) — dimensionamento correto
3. **Quadro de cargas com disjuntor + 1 por QDC** (§5) — entrega documental
4. **Quantitativo de condutores e disjuntores** (§8)
5. **Routing Preferences + offsets por ambiente** (§1.1)
6. **Route Fit com re-traçado** (§1.2)
7. **Central de Documentação: pranchas** (§4) e **Unifilar sincronizado** (§5)
8. **Importação consolidada de links** (§6)
9. **Família de TAG com simbologia** (§4) e **registro de atalhos** (§7)
10. **Validação no Revit + testes + revisão de tabelas** (infra)
