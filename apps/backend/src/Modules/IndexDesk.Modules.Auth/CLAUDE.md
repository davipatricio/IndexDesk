# IndexDesk.Modules.Auth — Autenticação & RBAC (scoped)

> Companion to [`../../CLAUDE.md`](../../CLAUDE.md). Regras específicas deste módulo.
> Contexto geral: [`../../../CLAUDE.md`](../../../CLAUDE.md). **Do not modify code** when only
> instruction updates are requested.

## Endpoints (`/api/v1/auth` e `/api/v1/users`, grupos `Auth` e `Users`)

| Rota | Método | Auth | Contrato |
| :--- | :--- | :--- | :--- |
| `/signup` | POST | anônima | 201 `AuthResponse` (Location `/api/v1/auth/me`) · cookie refresh setado |
| `/signin` | POST | anônima | 200 `AuthResponse` · cookie refresh setado |
| `/refresh` | POST | cookie | 200 rotação: lê cookie → revoga antigo (`ReplacedByTokenHash`) → novo par |
| `/signout` | POST | cookie opc. | 204: revoga token atual + limpa cookie |
| `/me` | GET | `RequireAuthorization()` | 200 `UserDto` (roles + permissions + `preferences: { hideValues }`) |
| `/api/v1/users/me` | PATCH | `RequireAuthorization()` | 200 `UserDto` atualizado · body `{ "hideValues": bool }` obrigatório |

Mapeamento de erros no endpoint (`ErrorToResult`): `Auth.Unauthorized` → 401 vazio;
código `Conflict` → 409 `{message}`; demais validações/falhas → 400 `{message}`.

## Fluxo de tokens (regras que NÃO se mudam sem revisão)

- **Access token:** JWT HS256, claims `sub/email/name/jti` + `ClaimTypes.Role` por role +
  claim `"permission"` por permission slug. Vida útil `Jwt:AccessTokenExpirationMinutes` (**default 15**).
- **Refresh token:** 64 bytes aleatórios (Base64Url). **Nunca persistir o valor cru** — só o hash
  SHA256 hex-lower (`ITokenService.ComputeTokenHash`). Expira em `Jwt:RefreshTokenExpirationDays` (**default 7**).
- **Rotação obrigatória:** cada `/refresh` revoga o token antigo e grava o novo com IP/UserAgent.
- **Detecção de reuso:** apresentar um token já revogado revoga **todas** as sessões ativas do usuário
  (comportamento anti-ataque intencional em `AuthService.RefreshTokenAsync` — não "corrigir").
- `ClockSkew = TimeSpan.Zero` no JwtBearer — manter zero.
- Senha mínima 8 chars; email normalizado trim + lowercase antes de qualquer query/duplicate-check.
- Signup exige role default `Name = "User"` existente no banco (seed); ausente → falha explícita.

## Cookie de refresh (`AuthCookieService`)

Nome fixo `refreshToken`; **Path restrito a `/api/v1/auth`**; `HttpOnly=true`; `SameSite=Lax`;
`Secure` = HTTPS ou ambiente fora de Development. Nunca alargar Path nem desligar HttpOnly.

## Hashing de senha (`Argon2idPasswordHasher`)

- Parâmetros: salt 16B, hash 32B, `m=65536` (64 MB), `t=3`, `p=4`; formato PHC
  `$argon2id$v=19$m=...,t=...,p=...$<salt>$<hash>`.
- Verify lê os parâmetros do próprio hash armazenado (permite subir m/t no futuro) e compara com
  `CryptographicOperations.FixedTimeEquals`. Não trocar por BCrypt sem decisão registrada.

## RBAC / autorização

- Modelos: `Users → UserRoles → Roles → RolePermissions → Permissions` (entidades em
  `BuildingBlocks.Persistence/Entities`; permissão identificada por `Slug`).
- `[HasPermission("recurso:acao")]` cria policies on-the-fly via prefixo `PERMISSION:` resolvido pelo
  `PermissionAuthorizationPolicyProvider`; o handler valida a claim `"permission"` do JWT.
- Policies nomeadas prontas: `AdminOnly` (roles Admin|SuperAdmin), `SuperAdminOnly`.
- ⚠️ Permissões viajam **dentro** do access token: mudanças de role só valem após re-issue (15 min).

## Config & segurança

- Chaves: `Jwt:SecretKey` (ou `Jwt:Secret`), `Jwt:Issuer` (`IndexDesk.Auth`),
  `Jwt:Audience` (`IndexDesk.Web`). Há fallback dev hardcoded — **produção exige SecretKey real** via env.
- DI: `IPasswordHasher`/`ITokenService` singletons (stateless); `IAuthService`/`IAuthCookieService` scoped.
- **Nunca logar** senha, token cru ou hash; IP/UserAgent vão apenas para auditoria do refresh token.

## Testes

Novos recursos: teste unitário para hasher/token service (casos: hash PHC malformado, senha errada,
token expirado/revogado) + teste de integração do endpoint no grupo Auth
(`tests/IndexDesk.IntegrationTests`, `WebApplicationFactory`).
