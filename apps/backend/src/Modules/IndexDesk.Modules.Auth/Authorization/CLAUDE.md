# Authorization/ — RBAC por permissão (scoped)

> Companion to [`../CLAUDE.md`](../CLAUDE.md) (módulo Auth). Contexto geral:
> [`../../../CLAUDE.md`](../../../CLAUDE.md). **Do not modify code** when only instruction updates are requested.

## Responsabilidade

Autorização baseada em permissões (`recurso:acao`) resolvidas on-the-fly a partir da claim
`"permission"` do JWT. Complementa o RBAC por roles: roles definem **quais** permissões o usuário
recebe no login (via `RolePermissions`); esta pasta decide se a request as satisfaz.

## Inventário

| Classe | Papel |
| :--- | :--- |
| `HasPermissionAttribute` | `AuthorizeAttribute` com `PolicyPrefix = "PERMISSION:"`; `[HasPermission("portfolio:write")]` gera policy nomeada `PERMISSION:portfolio:write`. `AllowMultiple`, herdável. |
| `PermissionAuthorizationPolicyProvider` | `IAuthorizationPolicyProvider`: policy com prefixo `PERMISSION:` vira `PermissionRequirement` na hora; qualquer outra delega ao `DefaultAuthorizationPolicyProvider` (ex.: `AdminOnly`, `SuperAdminOnly`). |
| `PermissionRequirement` | `IAuthorizationRequirement` imutável carregando o slug da permissão. |
| `PermissionAuthorizationHandler` | Concede sucesso somente se `context.User.HasClaim("permission", requirement.Permission)` e usuário autenticado. |

Registrados como **singletons** em `AuthModuleExtensions.AddAuthModule`
(`IAuthorizationPolicyProvider` + `IAuthorizationHandler`).

## Regras locais

- O slug da policy é comparado **literalmente** com a claim — case-sensitive no handler
  (`HasClaim`); use sempre lowercase pontuação consistente nos slugs de permissão.
- Permissões viajam dentro do access token: alterar `RolePermissions` só surte efeito após novo
  login/refresh (token vive ~15 min). Não "corrigir" lendo banco no handler sem decisão registrada.
- Para exigir múltiplas permissões no mesmo endpoint, empilhe `[HasPermission]` (cada vira uma policy).
- Novas policies por role nomeadas (`AdminOnly` etc.) ficam no `AddAuthorizationBuilder` do módulo,
  não aqui.
