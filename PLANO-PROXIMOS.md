# DmEletrico — Plano dos próximos 10 itens

Ordem de ataque priorizada pelo que destrava o fluxo central (carga → circuito →
conduíte → fiação → documentação) e pelo que está pendente/instável hoje.

---

## 1. Estabilizar e validar o fluxo central no Revit
**Por quê:** nada foi testado em runtime por nós; o fluxo precisa rodar fim-a-fim
sem erro. **O que fazer:** roteiro de teste reproduzível (QD + lâmpada + tomada),
validar Atribuir Carga → Criar Circuito → Construir Conduítes → Fiação Automática,
e corrigir o que aparecer. Entregar um modelo `.rvt` de exemplo no repo.

## 2. Circuito nativo do Revit (inicializar "força")
**Por quê:** hoje o circuito não aparece no Quadro de Cargas nativo / "força"
continua disponível. **O que fazer:** a partir do diagnóstico já adicionado no
"Criar Circuito", descobrir por que `ElectricalSystem.Create`/`SelectPanel` falha
(conector Power, classificação, painel). Se viável, criar o sistema nativo e
casar o número com o lógico; senão, assumir 100% lógico e gerar nosso próprio
Quadro de Cargas (item 4).

## 3. Anotação de fiação correta (família DMEletrico_Condutores)
**Por quê:** a anotação precisa refletir os condutores por circuito no trecho.
**O que fazer:** confirmar o mapeamento dos parâmetros da família
(`N_Fase/N_Neutro/N_Terra/N_Retorno/Bit_Fase/Bit_Terra/N_Circuito`); representar
**vários circuitos no mesmo conduíte** (ex.: C1 F+N e C2 F+N+T); ocultar bitolas
configuradas; realinhar no re-clique. Escolha de tamanho (Grande/Médio/Pequeno).

## 4. Quadro de Cargas do DmEletrico (por QD, a partir dos circuitos lógicos)
**Por quê:** se o nativo não popular, precisamos do nosso. **O que fazer:**
`ViewSchedule`/tabela por QD com circuito, descrição, tensão, potência (VA/W),
disjuntor (A), corrente (A), fase, tipo (Iluminação/TUG/TUE), seção. Ler dos
dispositivos (Dm_) e da topologia. Atualização sob demanda.

## 5. Motor de dimensionamento por circuito (FCT/FCA reais, seção, queda, disjuntor)
**Por quê:** o dimensionamento ainda é simplificado. **O que fazer:** por
circuito calcular corrente de projeto, FCT (temperatura), FCA (nº de circuitos
no trecho — já temos a topologia!), seção comercial, queda de tensão acumulada
do QD até o ponto, e disjuntor. Gravar no circuito/conduíte e expor na janela de
Detalhar Trecho. Revisar tabelas NBR 5410.

## 6. Roteamento — refinamentos
**Por quê:** casos ainda imperfeitos. **O que fazer:** reintroduzir offsets por
ambiente (laje/parede/contrapiso) como avançado; respeitar Routing Preferences /
fittings do tipo; suporte a eletrocalha/perfilado; curvas a 45°; Ajuste de Rotas
(Route Fit) topológico após mover dispositivo (re-traça mantendo NoA/NoB).

## 7. Gerenciador de circuitos e numeração inteligente
**Por quê:** organização dos circuitos por QD. **O que fazer:** numeração
sequencial inteligente (ímpares/pares por fase), renumerar, mover circuito entre
QDs, balancear fases lendo os circuitos lógicos, e o rastreador de desconectados
distinguindo "sem circuito" de "sem QD".

## 8. Diagrama Unifilar dinâmico (famílias DMEletrico_unifilar/Multifilar)
**Por quê:** entrega visual obrigatória. **O que fazer:** gerar unifilar por QD
usando as famílias `DMEletrico_unifilar_*` e `DMEletrico_Multifilar_*` (já na
pasta `familias`), com disjuntor geral, barramento, ramais (nº, bitola, disjuntor,
destino) a partir dos circuitos lógicos; comando "Atualizar" que regenera.

## 9. Quantitativos / BIM 5D completos
**Por quê:** fechar o orçamento. **O que fazer:** usar a topologia (condutores ×
comprimento por bitola, com margem de segurança) para a Tabela de Fiação; somar
eletrodutos por diâmetro, eletrocalhas/perfilados, disjuntores/DR/DPS, e
terminais (tomadas/luminárias/caixas). Gerar `ViewSchedule` nativo além da janela.

## 10. Central de Documentação e pranchas
**Por quê:** organizar as saídas. **O que fazer:** Central listando QDs/circuitos,
gerando e posicionando em pranchas (`ViewSheet`) os quadros de cargas, unifilares,
multifilares, legendas (`DMEletrico_Legenda*`) e quantitativos; atualização em
lote.

---

### Transversais (acompanham todos)
- Transferir tudo para parâmetros **nativos** da família (já iniciado) e validar
  que aparece em tags/tabelas nativas.
- Testes do núcleo puro (cálculo, ocupação, topologia) e roteiro de regressão.
- Empacotar/garantir o carregamento das famílias `DMEletrico_*` no Setup.
