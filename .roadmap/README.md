# Roadmap tracking

Este diretório é a fonte estruturada do roadmap de implementação do IndexDesk.

## Estrutura

- `roadmap.json`: estado real do projeto, documentos-fonte e ordem das fases.
- `phases/*.json`: épicos e tarefas agrupados por fase.
- `decisions.json`: decisões abertas/aceitas que podem alterar a arquitetura.
- `risks.json`: riscos com probabilidade, impacto e mitigação.
- `schema.json`: contrato JSON Schema Draft 2020-12.
- `scripts/validate-roadmap.ts`: valida IDs, enums, referências e dependências.
- `scripts/generate-roadmap.ts`: gera o `ROADMAP.md` determinístico.
- `../ROADMAP.md`: dashboard para leitura humana; não edite manualmente.

## Fluxo de atualização

1. Localize a tarefa e altere o JSON da fase correspondente.
2. Atualize `status`, `notes`, `assignee` e `completed_at` quando aplicável.
3. Mantenha `depends_on` e `blocks` coerentes; não marque uma tarefa como `complete` sem critérios aceitos.
4. Execute `bun run roadmap:validate`.
5. Execute `bun run roadmap:generate`.
6. Revise o diff de `ROADMAP.md` e os documentos-fonte relacionados.

O estado inicial é intencionalmente `documentation_only`: os documentos definem requisitos, não implementação entregue. O tracking do roadmap já está scaffoldado (`roadmap_tracking_scaffolded: true`), mas o produto ainda não possui código. Ao criar o primeiro código, atualize `current_state` em `roadmap.json` e os comandos reais em `CLAUDE.md`.

## Toolchain planejada

- Web: Oxlint latest + Oxfmt latest, TypeScript 7 quando publicado/validado, `module: preserve`, `moduleResolution: bundler`, target/lib modernos e `noEmit`.
- C#: CSharpier como dotnet tool local para whitespace/layout; `dotnet format style` e `dotnet format analyzers` para SDK/analyzers. Não executar `dotnet format whitespace` junto com CSharpier.
- Turborepo deve orquestrar `lint`, `format`, `typecheck`, `build`, `test` e `dev` para os dois workspaces.
