# Domain/ — Contratos do módulo Auth (scoped)

> Companion to [`../CLAUDE.md`](../CLAUDE.md) (módulo Auth). Contexto geral:
> [`../../../CLAUDE.md`](../../../CLAUDE.md). **Do not modify code** when only instruction updates are requested.

## Responsabilidade

DTOs de request/response da API de autenticação. Entidades persistidas (`UserEntity`,
`RefreshTokenEntity`, `RoleEntity`, `PermissionEntity` etc.) ficam em
`BuildingBlocks.Persistence/Entities` — não criar entidades aqui.

## Inventário (`Dtos/AuthDtos.cs`, namespace `IndexDesk.Modules.Auth.Domain.Dtos`)

| Record | Papel |
| :--- | :--- |
| `SignUpRequest(Email, Password, FullName)` | payload de `POST /api/v1/auth/signup`. |
| `SignInRequest(Email, Password)` | payload de `POST /api/v1/auth/signin`. |
| `UserDto(Id, Email, FullName, Roles, Permissions, Preferences)` | perfil retornado por signup/signin/refresh/me e patch /users/me; listas somente-leitura e record `UserPreferencesDto(HideValues)`. |
| `UserPreferencesDto(HideValues)` | preferências do usuário (ex.: toggle "Esconder dados"). |
| `UpdateUserPreferencesRequest(HideValues)` | payload de `PATCH /api/v1/users/me` (`HideValues` bool obrigatório). |
| `AuthResponse(AccessToken, RefreshToken, ExpiresIn, User)` | resposta com o par de tokens; o refresh também vai como cookie HttpOnly pelo endpoint. |

Todos são records `sealed` imutáveis, sem lógica nem validação interna (validação vive em
`Services/AuthService`).

## Regras locais

- Nunca incluir hash de senha, IP/UserAgent ou hashes de refresh token nestes DTOs — só dados que
  o próprio usuário pode ver.
- `RefreshToken` aparece no corpo **por design** (o endpoint o transforma em cookie); se algum dia
  sair do corpo, ajustar `/refresh` junto.
- Novo contrato = novo record `sealed` neste arquivo; requests/responses mutáveis violam a
  convenção do backend.
