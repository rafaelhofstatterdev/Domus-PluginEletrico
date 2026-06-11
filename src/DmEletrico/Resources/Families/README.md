# Famílias do DmEletrico

Coloque aqui os arquivos `.rfa` que o plugin deve carregar automaticamente no
projeto durante o **Setup** (`FamilyLoaderService`). Em especial:

- **TAG de conduíte** (categoria *Conduit Tags* / `OST_ConduitTags`) que exibe os
  parâmetros `Dm_NumeroCircuito` e `Dm_SecaoAdotada` e a simbologia de traços
  (fase / neutro / terra).
- **Tipo(s) de conduíte** específicos do template, se desejado.

O Setup também procura famílias em `%AppData%\DmEletrico\Families`.

> Estas famílias não são versionadas no repositório porque dependem do template
> de cada escritório. Crie-as no editor de famílias do Revit (rótulos lendo os
> parâmetros compartilhados `Dm_`) e salve aqui.
