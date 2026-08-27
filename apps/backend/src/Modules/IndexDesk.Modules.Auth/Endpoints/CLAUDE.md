# Endpoints/ — Minimal API do módulo Auth (scoped)

> Companion to [`../CLAUDE.md`](../CLAUDE.md) (módulo Auth). Contexto geral:
> [`../../../CLAUDE.md`](../../../CLAUDE.md). **Do not modify code** when only instruction updates are requested.

## Responsabilidade

Handlers HTTP do grupo `/api/v1/auth` (tag `Auth`), mapeados por `AuthEndpoints.MapAuthEndpoints`
(chamado a partir de `AuthModuleExtensions.MapAuthEndpoints`). Handlers são finos: delegam ao
`IAuthService`, setam/limpam o cookie e traduzem o `Error` para status HTTP.

## Rotas (`AuthEndpoints.cs`)

| Método | Rota | `WithName` | Resumo |
| :--- | :--- | :--- | :--- |
| POST | `/signup` | `SignUp` | 201 + `Location /api/v1/auth/me`; grava cookie de refresh. |
| POST | `/signin` | `SignIn` | 200 `AuthResponse`; grava cookie de refresh. |
| POST | `/refresh` | `RefreshToken` | lê o cookie, rotaciona o par de tokens, regrava o cookie; 200 ou erro. |
| POST | `/signout` | `SignOut` | revoga o refresh atual (cookie opcional) e limpa o cookie; sempre 204. |
| GET | `/me` | `GetCurrentUser` | requer autorização; 200 `UserDto` com `preferences` · 401 sem claim sub válida · 404 usuário ausente. |
| PATCH | `/api/v1/users/me` | `UpdateUserPreferences` | requer autorização; 200 `UserDto` com preferências atualizadas · 400 sem hideValues. |

IP (`RemoteIpAddress`) e `UserAgent` são extraídos aqui e passados ao serviço para auditoria do
refresh token.

## Regras locais

- Erro → HTTP exclusivamente via `ErrorToResult`: `Auth.Unauthorized` → **401 vazio**;
  `Conflict` → **409** `{message}`; demais códigos → **400** `{message}`. Nunca deixar exceção
  estourar como 500 para falha de negócio.
- Expiração do cookie vem de `Jwt:RefreshTokenExpirationDays` (fallback 7) — helper privado
  `RefreshExpiry`. Não hardcodar dias no handler.
- `/me` aceita tanto `JwtRegisteredClaimNames.Sub` quanto `ClaimTypes.NameIdentifier`.
- Novo endpoint: handler fino, `.WithName` + `.WithSummary`, teste de integração em
  `tests/IndexDesk.IntegrationTests` e linha na tabela do `../CLAUDE.md`.
