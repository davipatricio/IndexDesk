# Services/ — Lógica de aplicação do Auth (scoped)

> Companion to [`../CLAUDE.md`](../CLAUDE.md) (módulo Auth). Contexto geral:
> [`../../../CLAUDE.md`](../../../CLAUDE.md). **Do not modify code** when only instruction updates are requested.

## Responsabilidade

Casos de uso de autenticação sobre o `IndexDeskDbContext`: cadastro, login, rotação de refresh
token, logout e perfil. Retorna `Result<T>`/`Error` (código + mensagem) — nunca exceção para
falha de negócio.

## Inventário

| Tipo | Papel |
| :--- | :--- |
| `IAuthService` | Contrato: `SignUpAsync`, `SignInAsync`, `RefreshTokenAsync`, `SignOutAsync`, `GetCurrentUserAsync` (todos recebem IP/UserAgent quando aplicável). |
| `AuthService` | Implementação scoped: valida e normaliza entrada, hasheia senha via `IPasswordHasher`, emite par de tokens via `ITokenService`, persiste `RefreshTokenEntity` com hash + IP/UserAgent, monta o `UserDto` com roles/permissions. |

Dependências injetadas: `IndexDeskDbContext`, `IPasswordHasher`, `ITokenService`, `IConfiguration`.

## Regras locais (detalhes no `../CLAUDE.md` — não duplicar)

- Email normalizado trim + lowercase antes de qualquer query/duplicate-check; senha mínima 8 chars;
  signup exige role default `User` existente.
- `/refresh`: rotação obrigatória (`RevokedAt` + `ReplacedByTokenHash`) e **token reusado revoga
  todas as sessões ativas do usuário** — comportamento anti-ataque intencional, não corrigir.
- Refresh token persistido só como hash SHA256 (`ComputeTokenHash`); valor cru vive apenas em
  memória/cookie/resposta. Nunca logar credenciais, tokens ou hashes.
- Erros saem como códigos (`Auth.Unauthorized`, `Conflict`, `Error.Validation(...)` etc.) que os
  endpoints traduzem em status HTTP; adicionar código novo exige atualizar o switch em
  `Endpoints/AuthEndpoints.ErrorToResult`.
- Queries de usuário carregam a cadeia `UserRoles → Role → RolePermissions → Permission` com
  `Include`; manter `AsNoTracking` nas checagens de existência.
- Novo caso de uso: método no `IAuthService` + implementação aqui + teste unitário/integração
  conforme seção Testes do `../CLAUDE.md`.
